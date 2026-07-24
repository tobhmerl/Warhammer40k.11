namespace Warhammer40k.Core.Play;

/// <summary>A position in the battle sequence after advancing one phase.</summary>
public readonly record struct BattlePosition(int Round, BattleTurn Turn, BattlePhase Phase);

/// <summary>Pure helpers for advancing through both players' turns in a battle round.</summary>
public static class BattleSequence
{
    /// <summary>
    /// Advances one phase. After the first player's Fight phase, play moves to the other player's Command
    /// phase in the same round; after the second player's Fight phase, a new round begins.
    /// </summary>
    public static BattlePosition Next(
        int round,
        BattleTurn turn,
        BattlePhase phase,
        BattleTurn firstTurn = BattleTurn.Player)
    {
        round = Math.Max(1, round);
        if (phase != BattlePhase.Fight)
            return new BattlePosition(round, turn, BattlePhases.Next(phase));

        if (turn == firstTurn)
            return new BattlePosition(round, Other(turn), BattlePhase.Command);

        return new BattlePosition(round + 1, firstTurn, BattlePhase.Command);
    }

    private static BattleTurn Other(BattleTurn turn) =>
        turn == BattleTurn.Player ? BattleTurn.Opponent : BattleTurn.Player;
}

/// <summary>
/// Versioned transient state for one roster's Play Mode session. It is stored only in the current browser;
/// roster setup and reference data remain server-backed.
/// </summary>
public sealed class BattleSessionState
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public int Round { get; set; } = 1;

    public BattleTurn FirstTurn { get; set; } = BattleTurn.Player;

    public BattleTurn Turn { get; set; } = BattleTurn.Player;

    public BattlePhase Phase { get; set; } = BattlePhase.Command;

    public int CommandPoints { get; set; }

    public int YourPrimaryVp { get; set; }

    public int YourSecondaryVp { get; set; }

    public int OpponentPrimaryVp { get; set; }

    public int OpponentSecondaryVp { get; set; }

    public bool FocusMode { get; set; } = true;

    public string? ActiveUnitId { get; set; }

    public Dictionary<string, int> PartTracks { get; set; } = [];

    public Dictionary<string, int> WeaponKills { get; set; } = [];

    public HashSet<string> ActiveEffects { get; set; } = [];

    public Dictionary<string, string> ShootingChoices { get; set; } = [];

    /// <summary>
    /// Keys of "once per battle" abilities/enhancements/stratagems the player has marked as used this game.
    /// Such items drop out of the currently-usable actions until the game is reset.
    /// </summary>
    public HashSet<string> UsedOncePerBattle { get; set; } = [];

    /// <summary>Clamps untrusted or stale browser values to safe play-mode ranges.</summary>
    public void Normalize()
    {
        Version = CurrentVersion;
        Round = Math.Clamp(Round, 1, 99);
        if (!Enum.IsDefined(Turn)) Turn = BattleTurn.Player;
        if (!Enum.IsDefined(FirstTurn)) FirstTurn = BattleTurn.Player;
        if (!BattlePhases.Ordered.Contains(Phase)) Phase = BattlePhase.Command;
        CommandPoints = Math.Clamp(CommandPoints, 0, 99);
        YourPrimaryVp = Math.Clamp(YourPrimaryVp, 0, 999);
        YourSecondaryVp = Math.Clamp(YourSecondaryVp, 0, 999);
        OpponentPrimaryVp = Math.Clamp(OpponentPrimaryVp, 0, 999);
        OpponentSecondaryVp = Math.Clamp(OpponentSecondaryVp, 0, 999);
        PartTracks ??= [];
        WeaponKills ??= [];
        ActiveEffects ??= [];
        ShootingChoices ??= [];
        UsedOncePerBattle ??= [];
    }
}