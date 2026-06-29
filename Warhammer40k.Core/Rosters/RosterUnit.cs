using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Rosters;

/// <summary>
/// One unit instance inside a <see cref="Roster"/> (§5). Holds references and the player's choices; the
/// catalogue is resolved at display/validation time. Points come from the datasheet's
/// <see cref="PointsOption"/> selected by <see cref="ModelCount"/> — never per weapon (wargear is free).
/// </summary>
/// <remarks>
/// Deviation from §5: the redundant <c>attachedLeaderIds</c> back-link is omitted — attachment is modelled
/// once on the Leader via <see cref="AttachedToRosterUnitId"/>; the bodyguard's leaders are derived from it,
/// so rule R7 has a single source of truth.
/// </remarks>
public sealed class RosterUnit
{
    /// <summary>Roster-unique identifier (GUID "n"); lets other units reference this one (attachment, warlord).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The catalogue <see cref="Datasheet.Id"/> this unit instances.</summary>
    public string DatasheetId { get; set; } = string.Empty;

    /// <summary>Chosen unit size; must equal a <see cref="PointsOption.Models"/> on the datasheet (rule R8).</summary>
    public int ModelCount { get; set; }

    /// <summary>Selected wargear per option-group. Groups are authored later (AB7); free-of-points by design (§3).</summary>
    public List<WargearSelection> Wargear { get; set; } = [];

    /// <summary>When this unit is a Leader attached to a Bodyguard unit, the target bodyguard's <see cref="Id"/> (rule R7).</summary>
    public string? AttachedToRosterUnitId { get; set; }

    /// <summary>Assigned Enhancement id (Characters only, never Epic Heroes — rule R6). At most one per unit.</summary>
    public string? AssignedEnhancementId { get; set; }

    /// <summary>True for the unit nominated as Warlord (rule R5 enforces exactly one, Warlord-eligible).</summary>
    public bool IsWarlord { get; set; }

    /// <summary>Pantheon of Woe Necrodermal Binding name applied to this Monster (rule R10). Null when none.</summary>
    public string? AppliedBindingId { get; set; }

    /// <summary>Points surcharge for <see cref="AppliedBindingId"/>; defaults to the binding cost but is editable (§8). Counts in R1.</summary>
    public int BindingSurcharge { get; set; }

    /// <summary>Creates a new roster unit for a datasheet with a fresh id and its smallest legal size.</summary>
    public static RosterUnit FromDatasheet(Datasheet datasheet)
    {
        var smallest = datasheet.PointsOptions.OrderBy(o => o.Models).FirstOrDefault();
        return new RosterUnit
        {
            Id = Guid.NewGuid().ToString("n"),
            DatasheetId = datasheet.Id,
            ModelCount = smallest?.Models ?? 0,
        };
    }
}

/// <summary>A player's selection within a single wargear option-group. Option-groups are authored in AB7.</summary>
public sealed class WargearSelection
{
    /// <summary>The option-group this selection belongs to.</summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>The chosen option ids within the group (ordinary toggle groups).</summary>
    public List<string> OptionIds { get; set; } = [];

    /// <summary>
    /// Per-model model counts for a <see cref="Warhammer40k.Core.Catalogue.WargearGroup.PerModel"/> group:
    /// how many models take each option. Only non-default options need an entry — the group's first option
    /// (the default) absorbs any unassigned models. Empty for ordinary toggle groups.
    /// </summary>
    public List<WargearOptionCount> Counts { get; set; } = [];
}

/// <summary>How many models in a unit take a particular per-model wargear option.</summary>
public sealed class WargearOptionCount
{
    /// <summary>The <see cref="Warhammer40k.Core.Catalogue.WargearOption.Id"/> this count applies to.</summary>
    public string OptionId { get; set; } = string.Empty;

    /// <summary>The number of models equipped with this option.</summary>
    public int Models { get; set; }
}
