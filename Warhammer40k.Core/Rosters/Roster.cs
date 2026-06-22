using System.ComponentModel.DataAnnotations;
using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Rosters;

/// <summary>
/// A user's army list (§5). Stores <b>references</b> into the catalogue (datasheet/detachment/enhancement
/// ids); points and validity are resolved against the catalogue at display time by the pure validation
/// engine, never persisted as truth. Persisted per user in a later packet (AB5).
/// </summary>
/// <remarks>
/// Deviation from §5: the redundant <c>warlordRosterUnitId</c> is intentionally omitted — the warlord is
/// marked on the unit itself (<see cref="RosterUnit.IsWarlord"/>) so rule R5 can validate "exactly one".
/// </remarks>
public sealed class Roster
{
    /// <summary>Faction is fixed to Necrons for this app (§1).</summary>
    public const string NecronsFaction = "Necrons";

    /// <summary>Default points limit for a new roster (§1).</summary>
    public const int DefaultPointsLimit = 2000;

    /// <summary>The Strike-Force presets offered in the New-Roster wizard; a free custom value is also allowed (§1).</summary>
    public static readonly IReadOnlyList<int> PointsPresets = [1250, 1500, 2000];

    /// <summary>Stable identifier (GUID "n" format). Assigned by the server on create; empty for a new roster.</summary>
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(80, MinimumLength = 1, ErrorMessage = "Name must be 1-80 characters.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Always <see cref="NecronsFaction"/> for this app; stored for forward-compat and R9 safety.</summary>
    public string Faction { get; set; } = NecronsFaction;

    /// <summary>Points cap the roster is built against (rule R1).</summary>
    [Range(0, 100_000, ErrorMessage = "Points limit must be between 0 and 100,000.")]
    public int PointsLimit { get; set; } = DefaultPointsLimit;

    /// <summary>
    /// Legacy single detachment id (pre-11th-edition rosters). Prefer <see cref="DetachmentIds"/>; kept
    /// populated with the primary detachment so older readers keep working.
    /// </summary>
    public string DetachmentId { get; set; } = string.Empty;

    /// <summary>
    /// The detachments chosen for this roster (11th edition: one or more, purchased within the
    /// Detachment-Points budget for the points level).
    /// </summary>
    public List<string> DetachmentIds { get; set; } = [];

    /// <summary>
    /// The effective detachment ids: <see cref="DetachmentIds"/> when set, otherwise the legacy
    /// <see cref="DetachmentId"/> (so existing single-detachment rosters keep working). Not persisted.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> EffectiveDetachmentIds
    {
        get
        {
            if (DetachmentIds.Count > 0)
                return DetachmentIds;
            return string.IsNullOrEmpty(DetachmentId) ? [] : [DetachmentId];
        }
    }

    /// <summary>Catalogue version this roster was built against (so points can be re-resolved after edits).</summary>
    public string? CatalogueVersion { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset ModifiedUtc { get; set; }

    /// <summary>The units in this roster, in any order; rules group them by datasheet/role as needed.</summary>
    public List<RosterUnit> Units { get; set; } = [];

    /// <summary>Finds a unit by its roster-unique <see cref="RosterUnit.Id"/>.</summary>
    public RosterUnit? FindUnit(string rosterUnitId) =>
        Units.FirstOrDefault(u => string.Equals(u.Id, rosterUnitId, StringComparison.Ordinal));
}
