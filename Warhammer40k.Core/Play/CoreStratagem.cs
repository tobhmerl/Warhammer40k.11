namespace Warhammer40k.Core.Play;

/// <summary>
/// Whose turn it currently is in the battle round. Play Mode toggles this alongside the phase so the
/// right stratagems surface "in the actual second of the game".
/// </summary>
public enum BattleTurn
{
    /// <summary>The commanding player's own turn ("your turn").</summary>
    Player = 0,

    /// <summary>The opponent's turn.</summary>
    Opponent = 1,
}

/// <summary>
/// When a stratagem may be used relative to whose turn it is — mirrors the rulebook's coloured "Used in"
/// markers (green = either player's turn, blue = your turn, red = opponent's turn).
/// </summary>
public enum StratagemTurn
{
    /// <summary>Usable in either player's turn (green marker).</summary>
    Either = 0,

    /// <summary>Usable only in your own turn (blue marker).</summary>
    Your = 1,

    /// <summary>Usable only in your opponent's turn (red marker).</summary>
    Opponent = 2,
}

/// <summary>
/// A universal Core Stratagem (§15) shown contextually in Play Mode. Pure reference data: which phase(s)
/// and whose turn it is usable in drive the "need to know now" filter; the text fields are displayed as-is.
/// Distinct from a per-detachment <see cref="Rosters.Stratagem"/> (§11).
/// </summary>
public sealed record CoreStratagem
{
    /// <summary>Rulebook reference, e.g. "15.02".</summary>
    public required string Id { get; init; }

    /// <summary>Display name, e.g. "Command Re-roll".</summary>
    public required string Name { get; init; }

    /// <summary>Command Point cost.</summary>
    public required int Cost { get; init; }

    /// <summary>Whose turn this may be used in (the coloured "Used in" marker).</summary>
    public required StratagemTurn Turn { get; init; }

    /// <summary>The phase(s) this stratagem is used in. An empty list means it applies in any phase.</summary>
    public IReadOnlyList<BattlePhase> Phases { get; init; } = [];

    /// <summary>Italic flavour text.</summary>
    public string Flavour { get; init; } = "";

    /// <summary>The "WHEN" clause — the precise moment the stratagem can be used.</summary>
    public string When { get; init; } = "";

    /// <summary>The "TARGET" clause.</summary>
    public string Target { get; init; } = "";

    /// <summary>The "EFFECT" clause (may contain numbered steps separated by newlines).</summary>
    public string Effect { get; init; } = "";

    /// <summary>The "RESTRICTIONS" clause, or null when the stratagem has none.</summary>
    public string? Restrictions { get; init; }

    /// <summary>True when the stratagem is not tied to a single phase (e.g. Command Re-roll).</summary>
    public bool AppliesInAnyPhase => Phases.Count == 0;

    /// <summary>True when this stratagem is relevant in the given phase.</summary>
    public bool AppliesInPhase(BattlePhase phase) => AppliesInAnyPhase || Phases.Contains(phase);

    /// <summary>True when this stratagem can be used during the given turn.</summary>
    public bool AppliesInTurn(BattleTurn turn) => Turn switch
    {
        StratagemTurn.Either => true,
        StratagemTurn.Your => turn == BattleTurn.Player,
        StratagemTurn.Opponent => turn == BattleTurn.Opponent,
        _ => false,
    };
}
