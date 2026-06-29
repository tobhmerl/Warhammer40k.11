namespace Warhammer40k._11.Features.CombatSimulator.Domain;

/// <summary>
/// A resolved model stat line the engine consumes. Saves are integers (the target number; lower is better;
/// <c>null</c> = none). Part of the removable Combat Simulator feature — see
/// <c>Features/CombatSimulator/DELETE.md</c>.
/// </summary>
public sealed record CombatModelProfile
{
    public string Name { get; init; } = "";

    /// <summary>Movement, kept as a display string (not used by the math).</summary>
    public string Movement { get; init; } = "";

    public int Toughness { get; init; } = 4;

    /// <summary>Armour save target, e.g. 3 for a 3+. 7 = unsaveable.</summary>
    public int Save { get; init; } = 7;

    /// <summary>Invulnerable save target, or null when the model has none.</summary>
    public int? InvulnSave { get; init; }

    public int Wounds { get; init; } = 1;

    public string Leadership { get; init; } = "";

    public int ObjectiveControl { get; init; }

    /// <summary>Feel No Pain target, or null when the model has none.</summary>
    public int? FeelNoPain { get; init; }

    /// <summary>True when Feel No Pain only applies against mortal wounds (and/or a restricted condition).</summary>
    public bool FnpMortalOnly { get; init; }

    /// <summary>Flat damage reduction applied to each incoming wound before the floor of 1.</summary>
    public int DamageReductionFlat { get; init; }

    /// <summary>True when incoming damage is halved (round up) before the floor of 1.</summary>
    public bool DamageHalved { get; init; }
}
