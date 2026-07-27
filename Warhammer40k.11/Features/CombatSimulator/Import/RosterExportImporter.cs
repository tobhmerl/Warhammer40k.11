using System.Globalization;
using System.Text.Json;
using Warhammer40k._11.Features.CombatSimulator.Dice;
using Warhammer40k._11.Features.CombatSimulator.Domain;

namespace Warhammer40k._11.Features.CombatSimulator.Import;

/// <summary>
/// Parses a <c>tombworld.roster-export/1</c> document (the app's own JSON roster export) into
/// <see cref="CombatUnit"/>s. Because that format is already fully resolved — attached Leaders merged into
/// their bodyguard, buffs baked into the printed weapon and stat lines — this reads far more directly than
/// the New Recruit format: model groups, weapons and saves map one-to-one and nothing has to be inferred
/// from a selection tree.
/// Part of the removable Combat Simulator feature — see <c>Features/CombatSimulator/DELETE.md</c>.
/// </summary>
public static class RosterExportImporter
{
    /// <summary>The value of the export's <c>schema</c> property this importer understands.</summary>
    public const string SchemaId = "tombworld.roster-export/1";

    /// <summary>
    /// True when the JSON looks like a roster export, so the caller can route to the right parser.
    /// Detection is by schema first, falling back to shape for hand-edited documents that dropped it.
    /// </summary>
    public static bool CanImport(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        if (root.TryGetProperty("schema", out var schema)
            && schema.ValueKind == JsonValueKind.String
            && (schema.GetString() ?? "").StartsWith("tombworld.roster-export", StringComparison.OrdinalIgnoreCase))
            return true;

        // Shape fallback: a top-level "units" array whose entries carry "models".
        return root.TryGetProperty("units", out var units)
            && units.ValueKind == JsonValueKind.Array
            && units.EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.Object } first
            && first.TryGetProperty("models", out var models)
            && models.ValueKind == JsonValueKind.Array;
    }

    public static ImportResult Import(string json)
    {
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
            return Import(doc.RootElement);
    }

    public static ImportResult Import(JsonElement root)
    {
        var warnings = new List<string>();
        var units = new List<CombatUnit>();

        var faction = root.TryGetProperty("roster", out var roster) ? StringProp(roster, "faction") : "";

        if (!root.TryGetProperty("units", out var unitArray) || unitArray.ValueKind != JsonValueKind.Array)
            return new ImportResult([], ["No 'units' array found — this does not look like a roster export."]);

        foreach (var u in unitArray.EnumerateArray())
            units.Add(BuildUnit(u, faction, warnings));

        if (units.Count == 0)
            warnings.Add("The export contained no units.");

        return new ImportResult(units, warnings);
    }

    private static CombatUnit BuildUnit(JsonElement unit, string faction, List<string> warnings)
    {
        var name = StringProp(unit, "name");
        var groups = new List<CombatModelGroup>();

        // The export's unit-wide saves are already resolved, so they are applied to every model group rather
        // than re-derived from ability text. Model-scoped saves are skipped: the engine has no way to model
        // "only this one model gets a 4+", and silently applying it unit-wide would overstate durability.
        var unitInvuln = ResolvedSave(unit, "invulnerableSaves", warnings, name, "invulnerable save");
        var (unitFnp, fnpRestricted) = ResolvedFnp(unit, warnings, name);

        if (unit.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
            foreach (var group in models.EnumerateArray())
                groups.Add(BuildGroup(group, unitInvuln, unitFnp, fnpRestricted));

        if (groups.Count == 0)
            warnings.Add($"'{name}' had no model groups and was imported empty.");

        return new CombatUnit
        {
            Name = name,
            Faction = faction,
            Keywords = StringList(unit, "keywords"),
            ModelGroups = groups,
            UnitAbilities = BuildAbilities(unit),
            IsAttachedUnit = BoolProp(unit, "isAttachedUnit"),
            // The export bakes leader buffs into the profiles, so record them as inherited for display.
            InheritedEffects = StringList(unit, "appliedModifiers"),
            Source = CombatSource.Imported,
        };
    }

    private static CombatModelGroup BuildGroup(JsonElement group, int? unitInvuln, int? unitFnp, bool fnpRestricted)
    {
        var statline = group.TryGetProperty("statline", out var s) && s.ValueKind == JsonValueKind.Object
            ? s
            : default;

        var profile = new CombatModelProfile
        {
            Name = StringProp(group, "name"),
            Movement = StripInches(StringProp(statline, "m")),
            Toughness = ParseInt(StringProp(statline, "t"), 4),
            Save = ParseRollTarget(StringProp(statline, "sv"), 7),
            InvulnSave = unitInvuln,
            Wounds = Math.Max(1, IntProp(group, "woundsPerModel", ParseInt(StringProp(statline, "w"), 1))),
            Leadership = StringProp(statline, "ld"),
            ObjectiveControl = ParseInt(StringProp(statline, "oc"), 0),
            FeelNoPain = unitFnp,
            FnpMortalOnly = fnpRestricted,
        };

        var count = Math.Max(1, IntProp(group, "models", 1));
        var weapons = new List<CombatWeapon>();
        if (group.TryGetProperty("weapons", out var weaponArray) && weaponArray.ValueKind == JsonValueKind.Array)
            foreach (var w in weaponArray.EnumerateArray())
                weapons.Add(BuildWeapon(w, count));

        return new CombatModelGroup { Profile = profile, Count = count, Weapons = weapons };
    }

    private static CombatWeapon BuildWeapon(JsonElement weapon, int groupModels)
    {
        var melee = StringProp(weapon, "type").Equals("Melee", StringComparison.OrdinalIgnoreCase);

        return new CombatWeapon
        {
            Name = StringProp(weapon, "name"),
            Range = StringProp(weapon, "range"),
            IsMelee = melee,
            Attacks = ParseDice(StringProp(weapon, "attacks"), 1),
            Skill = ParseRollTarget(StringProp(weapon, "skill"), 4),
            Strength = ParseDice(StringProp(weapon, "strength"), 4),
            ArmourPenetration = ParseAp(StringProp(weapon, "ap")),
            Damage = ParseDice(StringProp(weapon, "damage"), 1),
            Abilities = WeaponKeywordParser.Parse(StringList(weapon, "abilities")),
            CriticalHitOn = weapon.TryGetProperty("criticalHitOn", out var crit) && crit.ValueKind == JsonValueKind.Number
                ? crit.GetInt32()
                : null,
            CarriedByModels = Math.Max(1, IntProp(weapon, "modelsCarrying", groupModels)),
        };
    }

    // Abilities keep their resolved defensive values out of the detector: the export already states them
    // explicitly, so re-parsing the prose could only disagree with it.
    private static List<UnitAbility> BuildAbilities(JsonElement unit)
    {
        var abilities = new List<UnitAbility>();
        if (unit.TryGetProperty("abilities", out var array) && array.ValueKind == JsonValueKind.Array)
            foreach (var a in array.EnumerateArray())
                abilities.Add(new UnitAbility
                {
                    Name = StringProp(a, "name"),
                    Description = StringProp(a, "text"),
                });
        return abilities;
    }

    // Reads a resolved save badge list, honouring only unit-wide entries.
    private static int? ResolvedSave(JsonElement unit, string property, List<string> warnings, string unitName, string label)
    {
        if (!unit.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
            return null;

        int? best = null;
        var skippedModelScoped = false;

        foreach (var badge in array.EnumerateArray())
        {
            var value = ParseRollTarget(StringProp(badge, "value"), 0);
            if (value <= 0)
                continue;

            if (!StringProp(badge, "appliesTo").Equals("unit", StringComparison.OrdinalIgnoreCase))
            {
                skippedModelScoped = true;
                continue;
            }

            best = best is null ? value : Math.Min(best.Value, value);
        }

        if (skippedModelScoped)
            warnings.Add($"'{unitName}' has a single-model {label} — not applied, since the simulator treats the unit as one profile.");

        return best;
    }

    // Feel No Pain additionally carries a restriction in its value text ("4+ vs Psychic Attacks"), which maps
    // onto the engine's mortal-only flag — the closest available approximation of a conditional FNP.
    private static (int? Value, bool Restricted) ResolvedFnp(JsonElement unit, List<string> warnings, string unitName)
    {
        if (!unit.TryGetProperty("feelNoPains", out var array) || array.ValueKind != JsonValueKind.Array)
            return (null, false);

        int? best = null;
        var restricted = false;

        foreach (var badge in array.EnumerateArray())
        {
            var raw = StringProp(badge, "value");
            var value = ParseRollTarget(raw, 0);
            if (value <= 0 || !StringProp(badge, "appliesTo").Equals("unit", StringComparison.OrdinalIgnoreCase))
                continue;

            if (best is null || value < best.Value)
            {
                best = value;
                restricted = raw.Contains("vs", StringComparison.OrdinalIgnoreCase)
                    || raw.Contains("against", StringComparison.OrdinalIgnoreCase);
            }
        }

        if (restricted)
            warnings.Add($"'{unitName}' has a conditional Feel No Pain — imported as mortal-wounds-only; adjust it in the defender modifiers if that is wrong.");

        return (best, restricted);
    }

    // ---- Value normalisation ----

    private static DiceExpression ParseDice(string value, int fallback)
    {
        var s = value.Trim();
        if (s.Length == 0)
            return DiceExpression.Constant(fallback);
        try
        {
            return DiceExpression.Parse(s);
        }
        catch (FormatException)
        {
            return DiceExpression.Constant(fallback);
        }
    }

    private static string StripInches(string v) => v.Replace("\"", "").Trim();

    // "3+" -> 3; "-"/empty -> fallback. Also tolerates trailing prose ("4+ vs Psychic Attacks").
    private static int ParseRollTarget(string v, int fallback)
    {
        var digits = new string(v.Trim().TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    // "-2" -> -2; "0"/"-"/empty -> 0. AP is stored non-positive.
    private static int ParseAp(string v)
    {
        var s = v.Trim();
        if (s.Length == 0 || s == "-")
            return 0;
        return int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? -Math.Abs(n) : 0;
    }

    private static int ParseInt(string v, int fallback)
    {
        var s = new string(v.Trim().TakeWhile(c => char.IsDigit(c) || c == '-').ToArray());
        return int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    // ---- JSON helpers ----

    private static string StringProp(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    private static bool BoolProp(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.True;

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

    private static List<string> StringList(JsonElement element, string name)
    {
        var list = new List<string>();
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var array)
            && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s)
                    list.Add(s);
        }
        return list;
    }
}
