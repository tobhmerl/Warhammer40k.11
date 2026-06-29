using Warhammer40k._11.Features.CombatSimulator.Dice;

namespace Warhammer40k._11.Features.CombatSimulator.Domain;

/// <summary>
/// A resolved weapon profile the engine consumes. AP is stored as a non-positive integer (e.g. <c>-2</c>).
/// A multi-mode weapon carries its alternative profiles in <see cref="FiringModes"/>; the user picks one mode
/// per activation. Part of the removable Combat Simulator feature.
/// </summary>
public sealed record CombatWeapon
{
    public string Name { get; init; } = "";

    /// <summary>Range string for display, e.g. <c>"24\""</c> or <c>"Melee"</c>.</summary>
    public string Range { get; init; } = "";

    public bool IsMelee { get; init; }

    public DiceExpression Attacks { get; init; } = DiceExpression.Constant(1);

    /// <summary>To-hit target: BS for ranged, WS for melee. 7 = cannot hit by roll (auto-hit weapons aside).</summary>
    public int Skill { get; init; } = 4;

    public DiceExpression Strength { get; init; } = DiceExpression.Constant(4);

    /// <summary>Armour penetration, stored non-positive (e.g. -2).</summary>
    public int ArmourPenetration { get; init; }

    public DiceExpression Damage { get; init; } = DiceExpression.Constant(1);

    public List<WeaponAbility> Abilities { get; init; } = [];

    /// <summary>Alternative firing modes (multi-profile weapons). Empty for a single-profile weapon.</summary>
    public List<CombatWeapon> FiringModes { get; init; } = [];

    /// <summary>How many models in the unit fire this weapon (user-editable; defaults from the data).</summary>
    public int CarriedByModels { get; init; } = 1;

    /// <summary>True when this weapon offers more than one firing mode.</summary>
    public bool HasFiringModes => FiringModes.Count > 1;

    public bool Has<T>() where T : WeaponAbility => Abilities.OfType<T>().Any();

    public T? Get<T>() where T : WeaponAbility => Abilities.OfType<T>().FirstOrDefault();
}
