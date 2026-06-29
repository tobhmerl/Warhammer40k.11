using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Warhammer40k._11.Features.CombatSimulator.Dice;
using Warhammer40k._11.Features.CombatSimulator.Domain;

namespace Warhammer40k._11.Features.CombatSimulator.Import;

/// <summary>The outcome of an import: the units parsed plus any non-fatal warnings to surface.</summary>
public sealed record ImportResult(IReadOnlyList<CombatUnit> Units, IReadOnlyList<string> Warnings);

/// <summary>
/// Parses a New Recruit / BattleScribe 11th-edition JSON export into <see cref="CombatUnit"/>s per the §6b
/// contract: a unit is any direct child of <c>force.selections</c> whose subtree contains a <c>"Unit"</c>
/// profile; model-groups come from <c>"Unit"</c> profiles (count = selection <c>number</c>); weapons from
/// <c>"Ranged/Melee Weapons"</c> profiles; multiple weapon profiles inside one selection are firing modes.
/// Part of the removable Combat Simulator feature — see <c>Features/CombatSimulator/DELETE.md</c>.
/// </summary>
public static class NewRecruitImporter
{
    public static ImportResult Import(string json)
    {
        var warnings = new List<string>();
        var units = new List<CombatUnit>();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return new ImportResult([], [$"Could not parse JSON: {ex.Message}"]);
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("roster", out var roster))
                return new ImportResult([], ["No 'roster' object found at the JSON root."]);

            if (roster.TryGetProperty("gameSystemName", out var gsn)
                && gsn.ValueKind == JsonValueKind.String
                && gsn.GetString()?.Contains("11th Edition", StringComparison.OrdinalIgnoreCase) != true)
            {
                warnings.Add($"Game system is '{gsn.GetString()}', not 11th Edition — importing anyway.");
            }

            if (roster.TryGetProperty("forces", out var forces) && forces.ValueKind == JsonValueKind.Array)
            {
                foreach (var force in forces.EnumerateArray())
                {
                    var faction = force.TryGetProperty("catalogueName", out var cn) && cn.ValueKind == JsonValueKind.String
                        ? cn.GetString() ?? ""
                        : "";

                    if (force.TryGetProperty("selections", out var selections) && selections.ValueKind == JsonValueKind.Array)
                        foreach (var sel in selections.EnumerateArray())
                            if (SubtreeHasUnitProfile(sel))
                                units.Add(BuildUnit(sel, faction, warnings));
                }
            }
        }

        return new ImportResult(units, warnings);
    }

    // ---- Unit reconstruction ----

    private static CombatUnit BuildUnit(JsonElement unitSel, string faction, List<string> warnings)
    {
        var name = StringProp(unitSel, "name");
        var groups = new List<CombatModelGroup>();
        var abilities = new List<UnitAbility>();

        CollectModelGroups(unitSel, groups);
        CollectAbilities(unitSel, abilities);

        // Single-model unit: the unit selection itself carries the only "Unit" profile.
        if (groups.Count == 0)
        {
            var profile = FindUnitProfile(unitSel);
            if (profile is not null)
                groups.Add(new CombatModelGroup
                {
                    Profile = profile,
                    Count = Math.Max(1, IntProp(unitSel, "number", 1)),
                    Weapons = CollectWeapons(unitSel),
                });
        }

        return new CombatUnit
        {
            Name = name,
            Faction = faction,
            ModelGroups = groups,
            UnitAbilities = abilities,
            Source = CombatSource.Imported,
        };
    }

    // A model-group is any selection bearing a direct "Unit" profile; its count = that selection's number,
    // its weapons = the weapon profiles in its subtree.
    private static void CollectModelGroups(JsonElement element, List<CombatModelGroup> groups)
    {
        if (TryGetArray(element, "selections", out var children))
        {
            foreach (var child in children.EnumerateArray())
            {
                var profile = DirectUnitProfile(child);
                if (profile is not null)
                {
                    groups.Add(new CombatModelGroup
                    {
                        Profile = profile,
                        Count = Math.Max(1, IntProp(child, "number", 1)),
                        Weapons = CollectWeapons(child),
                    });
                }
                else
                {
                    CollectModelGroups(child, groups);
                }
            }
        }
    }

    // All weapon profiles in a selection's subtree → CombatWeapons; multiple profiles in ONE selection = modes.
    private static List<CombatWeapon> CollectWeapons(JsonElement element)
    {
        var weapons = new List<CombatWeapon>();
        WalkWeapons(element, weapons);
        return weapons;
    }

    private static void WalkWeapons(JsonElement element, List<CombatWeapon> weapons)
    {
        // A weapon selection that holds 2+ weapon profiles = one multi-mode weapon.
        var modeProfiles = new List<JsonElement>();
        if (TryGetArray(element, "profiles", out var profiles))
            foreach (var p in profiles.EnumerateArray())
                if (IsWeaponProfile(p))
                    modeProfiles.Add(p);

        if (modeProfiles.Count > 0)
        {
            var number = Math.Max(1, IntProp(element, "number", 1));
            var built = modeProfiles.Select(p => BuildWeapon(p, number)).ToList();
            if (built.Count >= 2)
            {
                var primary = built[0] with { FiringModes = built };
                weapons.Add(primary);
            }
            else
            {
                weapons.Add(built[0]);
            }
        }

        if (TryGetArray(element, "selections", out var children))
            foreach (var child in children.EnumerateArray())
                WalkWeapons(child, weapons);
    }

    private static CombatWeapon BuildWeapon(JsonElement profile, int carriedBy)
    {
        var typeName = StringProp(profile, "typeName");
        var isMelee = typeName.Equals("Melee Weapons", StringComparison.OrdinalIgnoreCase);
        var ch = Characteristics(profile);

        return new CombatWeapon
        {
            Name = StringProp(profile, "name"),
            IsMelee = isMelee,
            Range = StripInches(ch.GetValueOrDefault("Range", isMelee ? "Melee" : "")),
            Attacks = DiceExpression.Parse(ch.GetValueOrDefault("A", "1")),
            Skill = ParseRollTarget(ch.GetValueOrDefault(isMelee ? "WS" : "BS", "4+"), 4),
            Strength = DiceExpression.Parse(ch.GetValueOrDefault("S", "4")),
            ArmourPenetration = ParseAp(ch.GetValueOrDefault("AP", "0")),
            Damage = DiceExpression.Parse(ch.GetValueOrDefault("D", "1")),
            Abilities = WeaponKeywordParser.Parse(ch.GetValueOrDefault("Keywords", "")),
            CarriedByModels = carriedBy,
        };
    }

    // ---- Profiles ----

    private static CombatModelProfile? FindUnitProfile(JsonElement element)
    {
        var direct = DirectUnitProfile(element);
        if (direct is not null)
            return direct;
        if (TryGetArray(element, "selections", out var children))
            foreach (var child in children.EnumerateArray())
            {
                var found = FindUnitProfile(child);
                if (found is not null)
                    return found;
            }
        return null;
    }

    private static CombatModelProfile? DirectUnitProfile(JsonElement element)
    {
        if (!TryGetArray(element, "profiles", out var profiles))
            return null;
        foreach (var p in profiles.EnumerateArray())
            if (StringProp(p, "typeName").Equals("Unit", StringComparison.OrdinalIgnoreCase))
                return BuildModelProfile(p);
        return null;
    }

    private static CombatModelProfile BuildModelProfile(JsonElement profile)
    {
        var ch = Characteristics(profile);
        var inv = ParseInvuln(ch.GetValueOrDefault("InSv", "-"));
        return new CombatModelProfile
        {
            Name = StringProp(profile, "name"),
            Movement = StripInches(ch.GetValueOrDefault("M", "")),
            Toughness = ParseInt(ch.GetValueOrDefault("T", "4"), 4),
            Save = ParseRollTarget(ch.GetValueOrDefault("Sv", "7"), 7),
            InvulnSave = inv,
            Wounds = ParseInt(ch.GetValueOrDefault("W", "1"), 1),
            Leadership = ch.GetValueOrDefault("LD", ""),
            ObjectiveControl = ParseInt(ch.GetValueOrDefault("OC", "0"), 0),
        };
    }

    private static void CollectAbilities(JsonElement element, List<UnitAbility> abilities)
    {
        if (TryGetArray(element, "profiles", out var profiles))
            foreach (var p in profiles.EnumerateArray())
                if (StringProp(p, "typeName").Equals("Abilities", StringComparison.OrdinalIgnoreCase))
                {
                    var ch = Characteristics(p);
                    abilities.Add(DefensiveAbilityDetector.Detect(StringProp(p, "name"), ch.GetValueOrDefault("Description", "")));
                }

        if (TryGetArray(element, "rules", out var rules))
            foreach (var r in rules.EnumerateArray())
                abilities.Add(DefensiveAbilityDetector.Detect(StringProp(r, "name"), StringProp(r, "description")));

        if (TryGetArray(element, "selections", out var children))
            foreach (var child in children.EnumerateArray())
                CollectAbilities(child, abilities);
    }

    // ---- Subtree predicates ----

    private static bool SubtreeHasUnitProfile(JsonElement element)
    {
        if (DirectUnitProfile(element) is not null)
            return true;
        if (TryGetArray(element, "selections", out var children))
            foreach (var child in children.EnumerateArray())
                if (SubtreeHasUnitProfile(child))
                    return true;
        return false;
    }

    private static bool IsWeaponProfile(JsonElement profile)
    {
        var t = StringProp(profile, "typeName");
        return t.Equals("Ranged Weapons", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Melee Weapons", StringComparison.OrdinalIgnoreCase);
    }

    // ---- Characteristic reading + value normalization ----

    private static Dictionary<string, string> Characteristics(JsonElement profile)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (TryGetArray(profile, "characteristics", out var chars))
            foreach (var c in chars.EnumerateArray())
            {
                var name = StringProp(c, "name");
                var value = c.TryGetProperty("$text", out var t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString() ?? ""
                    : "";
                if (name.Length > 0)
                    map[name] = value;
            }
        return map;
    }

    private static string StripInches(string v) => v.Replace("\"", "").Trim();

    // "3+" -> 3; "-"/empty -> fallback.
    private static int ParseRollTarget(string v, int fallback)
    {
        var s = v.Trim().TrimEnd('+');
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    // "-2" -> -2; "0"/"-"/empty -> 0. AP is stored non-positive.
    private static int ParseAp(string v)
    {
        var s = v.Trim();
        if (s.Length == 0 || s == "-")
            return 0;
        return int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? -Math.Abs(n) : 0;
    }

    // "4+" -> 4; "-"/empty -> null.
    private static int? ParseInvuln(string v)
    {
        var s = v.Trim().TrimEnd('+');
        if (s.Length == 0 || v.Trim() == "-")
            return null;
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static int ParseInt(string v, int fallback)
    {
        var s = new string(v.Trim().TakeWhile(c => char.IsDigit(c) || c == '-').ToArray());
        return int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    // ---- JSON helpers ----

    private static bool TryGetArray(JsonElement element, string name, out JsonElement array)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out array)
            && array.ValueKind == JsonValueKind.Array)
            return true;
        array = default;
        return false;
    }

    private static string StringProp(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    private static int IntProp(JsonElement element, string name, int fallback)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var v))
            return fallback;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetInt32(out var n) ? n : fallback,
            JsonValueKind.String => int.TryParse(v.GetString(), out var n) ? n : fallback,
            _ => fallback,
        };
    }
}

/// <summary>
/// Best-effort detection of defensive abilities (§6b.7) from ability text, pre-filling invuln / FNP / damage
/// reduction (all user-editable). Anything not matched is surfaced as a toggle, never silently applied.
/// </summary>
public static class DefensiveAbilityDetector
{
    private static readonly Regex InvulnRx = new(@"(\d)\+\s*invulnerable save", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FnpRx = new(@"feel no pain\s*(\d)\+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReduceFlatRx = new(@"(subtract 1 from the Damage|reduce the Damage[^.]*by 1)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HalveRx = new(@"halve the Damage", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static UnitAbility Detect(string name, string description)
    {
        var text = description ?? "";
        int? invuln = InvulnRx.Match(text) is { Success: true } im ? int.Parse(im.Groups[1].Value) : null;
        int? fnp = FnpRx.Match(text) is { Success: true } fm ? int.Parse(fm.Groups[1].Value) : null;
        var fnpMortalOnly = fnp is not null
            && (text.Contains("against mortal wounds", StringComparison.OrdinalIgnoreCase)
                || text.Contains("against Psychic", StringComparison.OrdinalIgnoreCase));
        var reduceFlat = ReduceFlatRx.IsMatch(text) ? 1 : 0;
        var halved = HalveRx.IsMatch(text);

        return new UnitAbility
        {
            Name = name,
            Description = text,
            InvulnSave = invuln,
            FeelNoPain = fnp,
            FnpMortalOnly = fnpMortalOnly,
            DamageReductionFlat = reduceFlat,
            DamageHalved = halved,
        };
    }
}
