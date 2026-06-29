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
        return battle.Units.Select(FromBattleUnit).ToList();
    }

    /// <summary>Maps one resolved <see cref="BattleUnit"/> (a bodyguard + any attached leaders) to a CombatUnit.</summary>
    public static CombatUnit FromBattleUnit(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        // Unit-wide invuln / FNP (the best unit-wide badge), mirroring Play Mode's chips.
        var unitInvuln = ParseTarget(unit.InvulnerableSaves.FirstOrDefault(b => b.UnitWide)?.Value);
        var unitFnp = ParseTarget(unit.FeelNoPains.FirstOrDefault(b => b.UnitWide)?.Value);

        var groups = new List<CombatModelGroup>();
        foreach (var part in unit.Parts)
        {
            var profile = part.Profile;
            var modelInvuln = unitInvuln
                ?? ParseTarget(unit.InvulnerableSaves.FirstOrDefault(b => !b.UnitWide && b.ModelName == part.Datasheet.Name)?.Value);
            var modelFnp = unitFnp
                ?? ParseTarget(unit.FeelNoPains.FirstOrDefault(b => !b.UnitWide && b.ModelName == part.Datasheet.Name)?.Value);

            groups.Add(new CombatModelGroup
            {
                Profile = new CombatModelProfile
                {
                    Name = part.Datasheet.Name,
                    Movement = profile?.Move ?? "",
                    Toughness = ParseInt(profile?.Toughness, 4),
                    Save = ParseTarget(profile?.Save) ?? 7,
                    InvulnSave = modelInvuln,
                    Wounds = part.WoundsPerModel ?? ParseInt(profile?.Wounds, 1),
                    Leadership = profile?.Leadership ?? "",
                    ObjectiveControl = ParseInt(profile?.ObjectiveControl, 0),
                    FeelNoPain = modelFnp,
                },
                Count = Math.Max(1, part.ModelCount),
                // Pre-fill how many models carry each weapon from the group size (matches the imported-army path).
                Weapons = part.Weapons.Select(w => MapWeapon(w, Math.Max(1, part.ModelCount))).ToList(),
            });
        }

        var abilities = unit.CombinedAbilities
            .Select(a => new UnitAbility { Name = a.Ability.Name, Description = a.Ability.Text })
            .ToList();

        return new CombatUnit
        {
            Name = unit.Name,
            Faction = "Necrons",
            ModelGroups = groups,
            UnitAbilities = abilities,
            Source = CombatSource.Native,
            IsAttachedUnit = unit.Parts.Count > 1,
        };
    }

    private static CombatWeapon MapWeapon(WeaponProfile w, int carriedByModels)
    {
        var isMelee = w.Type.Equals("Melee", StringComparison.OrdinalIgnoreCase);
        return new CombatWeapon
        {
            Name = w.Name,
            Range = w.Range,
            IsMelee = isMelee,
            Attacks = DiceExpression.Parse(w.Attacks),
            Skill = ParseTarget(w.Skill) ?? 4,
            Strength = DiceExpression.Parse(w.Strength),
            ArmourPenetration = ParseAp(w.ArmourPenetration),
            Damage = DiceExpression.Parse(w.Damage),
            Abilities = Import.WeaponKeywordParser.Parse(w.Keywords),
            CarriedByModels = carriedByModels,
        };
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
