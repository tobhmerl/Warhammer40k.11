using System.Globalization;
using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;
using Warhammer40k._11.Features.CombatSimulator.Dice;
using Warhammer40k._11.Features.CombatSimulator.Domain;

namespace Warhammer40k._11.Features.CombatSimulator.Adapters;

/// <summary>
/// Read-only adapter that maps the app's existing Necron domain (<see cref="BattleUnit"/> / <see cref="BattlePart"/>
/// / <see cref="WeaponProfile"/>) onto the simulator's normalized <see cref="CombatUnit"/>. It only <b>reads</b>
/// the existing types — nothing is added to or mutated on them. Invuln / Feel No Pain come from the same parsed
/// source Play Mode uses (<see cref="BattleUnit.InvulnerableSaves"/> / <see cref="BattleUnit.FeelNoPains"/>), so
/// values match the rest of the app. Part of the removable Combat Simulator feature — see
/// <c>Features/CombatSimulator/DELETE.md</c>.
/// </summary>
public static class NativeNecronSource
{
    /// <summary>Maps every resolved unit in a battle roster to a normalized <see cref="CombatUnit"/>.</summary>
    public static IReadOnlyList<CombatUnit> FromBattleRoster(BattleRoster battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        return battle.Units.Select(u => FromBattleUnit(battle, u)).ToList();
    }

    /// <summary>
    /// Maps one resolved <see cref="BattleUnit"/> (a bodyguard + any attached leaders) to a CombatUnit,
    /// baking in the modifiers Play Mode already resolves — leader conferrals, detachment buffs and
    /// enhancements — so the simulator does not have to be told about them by hand.
    /// </summary>
    public static CombatUnit FromBattleUnit(BattleRoster battle, BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(battle);
        ArgumentNullException.ThrowIfNull(unit);

        // Unit-wide invuln / FNP (the best unit-wide badge), mirroring Play Mode's chips.
        var unitInvuln = ParseTarget(unit.InvulnerableSaves.FirstOrDefault(b => b.UnitWide)?.Value);
        var unitFnp = ParseTarget(unit.FeelNoPains.FirstOrDefault(b => b.UnitWide)?.Value);

        var inherited = new List<string>();
        var groups = new List<CombatModelGroup>();
        foreach (var part in unit.Parts)
        {
            var profile = part.Profile;
            var modelInvuln = unitInvuln
                ?? ParseTarget(unit.InvulnerableSaves.FirstOrDefault(b => !b.UnitWide && b.ModelName == part.Datasheet.Name)?.Value);
            var modelFnp = unitFnp
                ?? ParseTarget(unit.FeelNoPains.FirstOrDefault(b => !b.UnitWide && b.ModelName == part.Datasheet.Name)?.Value);

            // Statline buffs (Toughness / Save) resolved exactly as the unit card shows them.
            var unitMods = battle.UnitStatModifiers(unit, part);
            var toughness = Buffed(profile?.Toughness, unitMods, StatTarget.Toughness);
            var save = Buffed(profile?.Save, unitMods, StatTarget.Save);
            Record(inherited, unitMods, part.Datasheet.Name);

            groups.Add(new CombatModelGroup
            {
                Profile = new CombatModelProfile
                {
                    Name = part.Datasheet.Name,
                    Movement = Buffed(profile?.Move, unitMods, StatTarget.Move),
                    Toughness = ParseInt(toughness, 4),
                    Save = ParseTarget(save) ?? 7,
                    InvulnSave = modelInvuln,
                    Wounds = part.WoundsPerModel ?? ParseInt(profile?.Wounds, 1),
                    Leadership = profile?.Leadership ?? "",
                    ObjectiveControl = ParseInt(profile?.ObjectiveControl, 0),
                    FeelNoPain = modelFnp,
                },
                Count = Math.Max(1, part.ModelCount),
                // Pre-fill how many models carry each weapon from the resolved per-model loadout (e.g. a
                // Lokhust Heavy Destroyers unit split 2 Gauss / 1 Enmitic), falling back to the group size.
                Weapons = part.Weapons
                    .Select(w => MapWeapon(battle, unit, part, w, Math.Max(1, part.ModelsCarrying(w)), inherited))
                    .ToList(),
            });
        }

        var abilities = unit.CombinedAbilities
            .Select(a => new UnitAbility { Name = a.Ability.Name, Description = a.Ability.Text })
            .ToList();

        return new CombatUnit
        {
            Name = unit.Name,
            Faction = "Necrons",
            Keywords = KeywordsOf(unit),
            ModelGroups = groups,
            UnitAbilities = abilities,
            InheritedEffects = inherited,
            Source = CombatSource.Native,
            IsAttachedUnit = unit.Parts.Count > 1,
        };
    }

    /// <summary>
    /// Every datasheet keyword across the unit's parts, de-duplicated. A qualified keyword
    /// ("Faction: Necrons") also contributes its bare value ("Necrons"), so Anti-[keyword] matching works
    /// either way. These drive Anti-X when this unit is the target.
    /// </summary>
    private static List<string> KeywordsOf(BattleUnit unit)
    {
        var result = new List<string>();

        void Add(string keyword)
        {
            var value = keyword.Trim();
            if (value.Length > 0 && !result.Contains(value, StringComparer.OrdinalIgnoreCase))
                result.Add(value);
        }

        foreach (var keyword in unit.Parts.SelectMany(p => p.Datasheet.Keywords))
        {
            Add(keyword);
            var colon = keyword.IndexOf(':');
            if (colon >= 0)
                Add(keyword[(colon + 1)..]);
        }

        return result;
    }

    /// <summary>
    /// Maps one weapon, applying the buffs Play Mode resolves for it: numeric stat modifiers (Attacks, Skill,
    /// Strength, Damage) are folded into the profile the same way the unit card displays them, granted weapon
    /// abilities are merged in, and a lowered critical-hit threshold is carried on the weapon.
    /// </summary>
    private static CombatWeapon MapWeapon(
        BattleRoster battle, BattleUnit unit, BattlePart part, WeaponProfile w, int carriedByModels, List<string> inherited)
    {
        var isMelee = w.Type.Equals("Melee", StringComparison.OrdinalIgnoreCase);
        var ranged = !isMelee;
        var mods = battle.WeaponStatModifiers(unit, part, ranged);
        Record(inherited, mods, isMelee ? "melee" : "ranged");

        // Detachment / leader granted abilities ([ASSAULT], [LETHAL HITS], …) on top of the printed keywords.
        var abilities = Import.WeaponKeywordParser.Parse(w.Keywords);
        var granted = battle.GrantedWeaponAbilities(unit, part, ranged);
        foreach (var ability in Import.WeaponKeywordParser.Parse(granted))
        {
            if (!abilities.Any(existing => existing.GetType() == ability.GetType()))
            {
                abilities.Add(ability);
                AddOnce(inherited, $"{ability.Label} ({(isMelee ? "melee" : "ranged")})");
            }
        }

        var critHitOn = battle.CriticalHitOn(unit, part, ranged);
        if (critHitOn is { } crit)
            AddOnce(inherited, $"Critical hit {crit}+ ({(isMelee ? "melee" : "ranged")})");

        return new CombatWeapon
        {
            Name = w.Name,
            Range = w.Range,
            IsMelee = isMelee,
            Attacks = DiceExpression.Parse(Buffed(w.Attacks, mods, StatTarget.Attacks)),
            Skill = ParseTarget(Buffed(w.Skill, mods, StatTarget.Skill)) ?? 4,
            Strength = DiceExpression.Parse(Buffed(w.Strength, mods, StatTarget.Strength)),
            ArmourPenetration = ParseAp(w.ArmourPenetration),
            Damage = DiceExpression.Parse(Buffed(w.Damage, mods, StatTarget.Damage)),
            Abilities = abilities,
            CriticalHitOn = critHitOn,
            CarriedByModels = carriedByModels,
        };
    }

    /// <summary>Applies the modifiers that target <paramref name="target"/> to a raw characteristic string.</summary>
    private static string Buffed(string? raw, IReadOnlyList<StatModifier> mods, StatTarget target)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw ?? "";
        var applicable = mods.Where(m => m.Target == target).ToList();
        return applicable.Count == 0 ? raw : StatMath.ApplyAll(raw, applicable);
    }

    // Notes what was inherited so the simulator can show it; scope is the weapon class or the model name.
    private static void Record(List<string> inherited, IReadOnlyList<StatModifier> mods, string scope)
    {
        foreach (var mod in mods)
            AddOnce(inherited, $"{mod.Describe()} ({scope})");
    }

    private static void AddOnce(List<string> inherited, string label)
    {
        if (!inherited.Contains(label, StringComparer.OrdinalIgnoreCase))
            inherited.Add(label);
    }

    // "3+" / "2+" -> 3 / 2; "N/A"/blank -> null.
    private static int? ParseTarget(string? value)
    {
        var s = (value ?? "").Trim().TrimEnd('+');
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static int ParseInt(string? value, int fallback)
    {
        var s = new string((value ?? "").Trim().TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    // "-2" -> -2; "0"/"-"/blank -> 0. Stored non-positive.
    private static int ParseAp(string? value)
    {
        var s = (value ?? "").Trim();
        if (s.Length == 0 || s == "-")
            return 0;
        return int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? -Math.Abs(n) : 0;
    }
}
