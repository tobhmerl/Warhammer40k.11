namespace Warhammer40k.Core.Play;

/// <summary>
/// A single phase + turn window in which a scheduled ability or stratagem is usable (e.g. "my Shooting
/// phase"). Stored on the <see cref="Rosters.Roster"/> as part of an <see cref="AbilitySchedule"/>.
/// </summary>
public sealed class AbilityWindow
{
    /// <summary>The battle phase this window covers (Command…Fight; never <see cref="BattlePhase.Any"/>).</summary>
    public BattlePhase Phase { get; set; }

    /// <summary>Whose turn this window covers.</summary>
    public BattleTurn Turn { get; set; }

    /// <summary>Parameterless ctor for serialization.</summary>
    public AbilityWindow() { }

    /// <summary>Creates a window for a phase + turn.</summary>
    public AbilityWindow(BattlePhase phase, BattleTurn turn)
    {
        Phase = phase;
        Turn = turn;
    }
}

/// <summary>
/// The player's manual schedule for one ability, army rule, or stratagem: the set of phase + turn
/// <see cref="AbilityWindow"/>s in which it is "usable now", plus whether its conferred effect (a keyword
/// like [LETHAL HITS] or a stat/weapon buff) is applied straight to the unit. Both default to nothing —
/// scheduling is entirely manual, so an unconfigured entry is never surfaced and never applied.
/// </summary>
/// <remarks>
/// Keyed by <see cref="Key"/>, built by <see cref="AbilityScheduleKeys"/> so the same logical thing is
/// configured once: a unit ability per datasheet, an army rule per name, a stratagem per source + id.
/// </remarks>
public sealed class AbilitySchedule
{
    /// <summary>Stable identity built by <see cref="AbilityScheduleKeys"/>; ties this schedule to its ability.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The phase + turn windows in which this is usable. Empty = never surfaced as "usable now".</summary>
    public List<AbilityWindow> Windows { get; set; } = [];

    /// <summary>
    /// When true, the ability's conferred effect (keyword/stat/weapon change) is applied to the unit and its
    /// prose is hidden in Play Mode; when false the ability is shown as ordinary text and nothing is applied.
    /// </summary>
    public bool ApplyToUnit { get; set; }

    /// <summary>
    /// A player-authored short keyword (≤3 words, e.g. "Stealth") for an ability the engine can't summarise
    /// itself. When set, Play Mode shows the ability as a passive keyword chip (alongside Inv/FNP) reading this
    /// label, regardless of the phase/turn windows, with the original ability text available on tap. Null = none.
    /// </summary>
    public string? ManualKeyword { get; set; }

    /// <summary>True when a window for the given phase + turn is ticked.</summary>
    public bool Covers(BattlePhase phase, BattleTurn turn) =>
        Windows.Any(w => w.Phase == phase && w.Turn == turn);

    /// <summary>Adds (on) or removes (off) the window for a phase + turn, keeping the set free of duplicates.</summary>
    public void SetWindow(BattlePhase phase, BattleTurn turn, bool on)
    {
        Windows.RemoveAll(w => w.Phase == phase && w.Turn == turn);
        if (on)
            Windows.Add(new AbilityWindow(phase, turn));
    }
}

/// <summary>
/// Builds the stable <see cref="AbilitySchedule.Key"/> for each kind of scheduled thing, so "configure once"
/// matches the thing's nature: unit abilities are keyed per datasheet, army rules per name, stratagems per
/// source + id. Keys are case-insensitive-safe by lower-casing the free-text parts.
/// </summary>
public static class AbilityScheduleKeys
{
    /// <summary>A datasheet ability (shared across every instance of that datasheet, incl. when attached).</summary>
    public static string ForUnitAbility(string datasheetId, string abilityName) =>
        $"unit|{datasheetId}|{Norm(abilityName)}";

    /// <summary>A faction-wide army rule (shared across all units).</summary>
    public static string ForArmyRule(string ruleName) =>
        $"armyrule|{Norm(ruleName)}";

    /// <summary>A setup-assigned enhancement, keyed by its id (shared wherever that enhancement is assigned).</summary>
    public static string ForEnhancement(string enhancementId) =>
        $"enh|{Norm(enhancementId)}";

    /// <summary>A Core Stratagem, keyed by its rulebook id.</summary>
    public static string ForCoreStratagem(string id) =>
        $"strat|core|{Norm(id)}";

    /// <summary>A detachment stratagem, keyed by detachment id + stratagem id.</summary>
    public static string ForDetachmentStratagem(string detachmentId, string stratagemId) =>
        $"strat|{Norm(detachmentId)}|{Norm(stratagemId)}";

    /// <summary>A detachment conditional buff (e.g. Relentless Onslaught), keyed by detachment id + buff label.</summary>
    public static string ForDetachmentBuff(string detachmentId, string buffLabel) =>
        $"detbuff|{Norm(detachmentId)}|{Norm(buffLabel)}";

    private static string Norm(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
