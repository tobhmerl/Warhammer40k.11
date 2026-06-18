using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Core;

/// <summary>
/// A portable snapshot of a user's data (AB8 backup/restore). Serialized to JSON for export and consumed on
/// import. Includes settings, every roster, and the user's <b>customized</b> catalogue (null when they're on
/// the default, to keep backups lean and to let restore revert to the default).
/// </summary>
public sealed class BackupBundle
{
    /// <summary>Schema/app marker so a future import can detect the format.</summary>
    public string Format { get; set; } = "tombforge-backup-v1";

    public DateTimeOffset CreatedUtc { get; set; }

    public UserSettings Settings { get; set; } = new();

    /// <summary>The user's customized catalogue, or <c>null</c> when they are on the embedded default.</summary>
    public CatalogueData? Catalogue { get; set; }

    public List<Roster> Rosters { get; set; } = [];
}
