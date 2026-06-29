namespace Warhammer40k._11.Features.CombatSimulator.Domain;

/// <summary>How a re-roll policy applies to a roll step. Part of the removable Combat Simulator feature.</summary>
public enum RerollPolicy
{
    None = 0,

    /// <summary>Re-roll natural 1s only.</summary>
    Ones = 1,

    /// <summary>Re-roll all failed rolls.</summary>
    Failures = 2,
}

/// <summary>
/// Attacker-side modifiers, all opt-in toggles in the UI (nothing global). The two hit buckets (§8.0) are
/// kept separate: <see cref="BsWsModifier"/> changes the BS/WS characteristic (uncapped), while
/// <see cref="HitRollModifier"/> is the ±1 on the die (the engine clamps the net to [-1, +1]).
/// </summary>
public sealed record AttackerModifiers
{
    public int HitRollModifier { get; init; }
    public int BsWsModifier { get; init; }
    public int WoundRollModifier { get; init; }

    public RerollPolicy RerollHits { get; init; } = RerollPolicy.None;
    public RerollPolicy RerollWounds { get; init; } = RerollPolicy.None;

    public bool LethalHits { get; init; }
    public bool DevastatingWounds { get; init; }
    public int SustainedHits { get; init; }

    /// <summary>Critical-hit threshold (6 by default; some abilities lower it to 5).</summary>
    public int CritHitThreshold { get; init; } = 6;

    /// <summary>Critical-wound threshold override (6 by default; Anti can lower it per target).</summary>
    public int CritWoundThreshold { get; init; } = 6;

    public int StrengthBonus { get; init; }
    public int AttacksBonus { get; init; }
    public int DamageBonus { get; init; }
    public int ApModifier { get; init; }

    /// <summary>Oath of Moment: re-roll hits and (conditionally) +1 to wound.</summary>
    public bool OathOfMoment { get; init; }

    /// <summary>Drives Rapid Fire and Melta.</summary>
    public bool TargetWithinHalfRange { get; init; }

    /// <summary>Anti override: when set, treat the weapon as Anti-(any) at this threshold.</summary>
    public int? AntiOverrideThreshold { get; init; }

    public bool IgnoresCover { get; init; }
}

/// <summary>
/// Defender-side modifiers. Cover applies the −1 BS penalty to incoming attacks (§8.0), <b>not</b> a save bonus.
/// Save modifiers affect the armour save only; invuln is never modified by AP or save modifiers.
/// </summary>
public sealed record DefenderModifiers
{
    public bool Cover { get; init; }
    public int? InvulnSaveOverride { get; init; }
    public int? FeelNoPainOverride { get; init; }
    public bool FnpMortalOnly { get; init; }
    public int DamageReductionFlat { get; init; }
    public bool DamageHalved { get; init; }
    public int SaveModifier { get; init; }
    public int? SaveOverride { get; init; }
    public int? ToughnessOverride { get; init; }
    public int? WoundsPerModelOverride { get; init; }
    public int? ModelCountOverride { get; init; }
    public bool BelowHalfStrength { get; init; }

    /// <summary>Stealth / "−1 to be hit": worsens the attacker's hit-roll bucket.</summary>
    public bool MinusOneToBeHit { get; init; }

    /// <summary>"−1 to be wounded": worsens the attacker's wound-roll bucket.</summary>
    public bool MinusOneToBeWounded { get; init; }
}
