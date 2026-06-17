using System.ComponentModel.DataAnnotations;

namespace Warhammer40k.Core;

/// <summary>
/// A user's army roster header. Persisted per user in Azure Table Storage
/// (PartitionKey = user id, RowKey = <see cref="Id"/>). Unit details arrive in a later milestone.
/// </summary>
public sealed class Army
{
    /// <summary>Stable identifier (GUID "n" format). Assigned by the server on create; empty for a new army.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name of the army/roster.</summary>
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(80, MinimumLength = 1, ErrorMessage = "Name must be 1-80 characters.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Faction / army label, e.g. "Necrons" or "Ultramarines".</summary>
    [Required(ErrorMessage = "Faction is required.")]
    [StringLength(60, MinimumLength = 1, ErrorMessage = "Faction must be 1-60 characters.")]
    public string Faction { get; set; } = string.Empty;

    /// <summary>Total points value of the army.</summary>
    [Range(0, 100_000, ErrorMessage = "Points must be between 0 and 100,000.")]
    public int Points { get; set; }

    /// <summary>Last update timestamp (UTC), set by the server on save.</summary>
    public DateTimeOffset? UpdatedUtc { get; set; }
}
