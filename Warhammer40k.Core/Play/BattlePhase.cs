namespace Warhammer40k.Core.Play;

/// <summary>
/// The five phases of a Warhammer 40,000 battle round, in turn order. <see cref="Any"/> marks
/// always-on / passive content (e.g. an invulnerable save) that is not tied to a single phase.
/// </summary>
public enum BattlePhase
{
    /// <summary>Always-on or passive content not tied to a single phase (shown in the unit header and under Command).</summary>
    Any = 0,
    Command = 1,
    Movement = 2,
    Shooting = 3,
    Charge = 4,
    Fight = 5,
}

/// <summary>Display metadata for <see cref="BattlePhase"/> values (labels + the ordered phase sequence).</summary>
public static class BattlePhases
{
    /// <summary>The five phases in play order (excludes <see cref="BattlePhase.Any"/>).</summary>
    public static readonly IReadOnlyList<BattlePhase> Ordered =
    [
        BattlePhase.Command,
        BattlePhase.Movement,
        BattlePhase.Shooting,
        BattlePhase.Charge,
        BattlePhase.Fight,
    ];

    /// <summary>A short human label for a phase.</summary>
    public static string Label(BattlePhase phase) => phase switch
    {
        BattlePhase.Command => "Command",
        BattlePhase.Movement => "Movement",
        BattlePhase.Shooting => "Shooting",
        BattlePhase.Charge => "Charge",
        BattlePhase.Fight => "Fight",
        _ => "Always",
    };

    /// <summary>A compact 3-letter label for a phase, for tight controls (e.g. the Play-Mode phase bar).</summary>
    public static string ShortLabel(BattlePhase phase) => phase switch
    {
        BattlePhase.Command => "CMD",
        BattlePhase.Movement => "MOV",
        BattlePhase.Shooting => "SHO",
        BattlePhase.Charge => "CHG",
        BattlePhase.Fight => "FGT",
        _ => "ALL",
    };
}
