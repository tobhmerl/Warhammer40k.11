namespace Warhammer40k.Core.Play;

/// <summary>
/// A faction-wide <b>Army Rule</b> — the first-level rule that applies to every army of a given Faction
/// regardless of detachment (e.g. the Necrons' Reanimation Protocols). Pure reference data shown in Play
/// Mode: <see cref="Phases"/> drives a "now" emphasis, the text fields are displayed as-is. Distinct from a
/// per-datasheet <see cref="Catalogue.Ability"/> and from a per-detachment rule.
/// </summary>
public sealed record ArmyRule
{
    /// <summary>Display name, e.g. "Reanimation Protocols".</summary>
    public required string Name { get; init; }

    /// <summary>The Army Faction this rule belongs to (e.g. "Necrons").</summary>
    public string Faction { get; init; } = Rosters.Roster.NecronsFaction;

    /// <summary>The rules body (may contain bullet steps separated by newlines).</summary>
    public required string Text { get; init; }

    /// <summary>The worked example, or empty when the rule has none.</summary>
    public string Example { get; init; } = "";

    /// <summary>Italic lore/flavour text, or empty when not authored yet.</summary>
    public string Lore { get; init; } = "";

    /// <summary>The phase(s) this rule keys off. An empty list means it is always-on / not tied to a phase.</summary>
    public IReadOnlyList<BattlePhase> Phases { get; init; } = [];

    /// <summary>True when the rule is not tied to a single phase.</summary>
    public bool AppliesInAnyPhase => Phases.Count == 0;

    /// <summary>True when this rule is keyed to the given phase (used for the Play-Mode "now" emphasis).</summary>
    public bool AppliesInPhase(BattlePhase phase) => AppliesInAnyPhase || Phases.Contains(phase);

    /// <summary>True when a worked example is available to show.</summary>
    public bool HasExample => !string.IsNullOrWhiteSpace(Example);

    /// <summary>True when lore text is available to show.</summary>
    public bool HasLore => !string.IsNullOrWhiteSpace(Lore);
}
