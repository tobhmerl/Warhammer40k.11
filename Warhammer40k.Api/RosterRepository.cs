using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Api;

/// <summary>
/// Per-user roster persistence (the AB5 evolution of <see cref="IArmyRepository"/>). Every operation is
/// scoped to <paramref name="userId"/>, which is the Table Storage partition key, so one user can never
/// read or modify another user's data.
/// </summary>
public interface IRosterRepository
{
    /// <summary>Lists a user's rosters, newest first.</summary>
    Task<IReadOnlyList<Roster>> ListAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Gets one roster in the user's partition, or <c>null</c> if it does not exist.</summary>
    Task<Roster?> GetAsync(string userId, string id, CancellationToken cancellationToken = default);

    /// <summary>Creates (assigns a new id when <see cref="Roster.Id"/> is empty) or replaces a roster.</summary>
    Task<Roster> UpsertAsync(string userId, Roster roster, CancellationToken cancellationToken = default);

    /// <summary>Deletes a roster; a no-op when it does not exist.</summary>
    Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Azure Table Storage row for a <see cref="Roster"/> (PartitionKey = user id, RowKey = roster id). Table
/// Storage is flat, so the unit list is persisted as a JSON string in <see cref="UnitsJson"/>; the row's
/// <see cref="Timestamp"/> is surfaced as <see cref="Roster.ModifiedUtc"/>.
/// </summary>
public sealed class RosterEntity : ITableEntity
{
    private static readonly JsonSerializerOptions UnitsJsonOptions = new(JsonSerializerDefaults.Web);

    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Faction { get; set; } = string.Empty;
    public int PointsLimit { get; set; }
    public string DetachmentId { get; set; } = string.Empty;

    /// <summary>The full detachment list as CSV (Table Storage cannot store collections natively).</summary>
    public string DetachmentIdsCsv { get; set; } = string.Empty;
    public string? CatalogueVersion { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>The roster's units serialized as JSON (Table Storage cannot store collections natively).</summary>
    public string UnitsJson { get; set; } = "[]";

    public Roster ToRoster() => new()
    {
        Id = RowKey,
        Name = Name,
        Faction = Faction,
        PointsLimit = PointsLimit,
        DetachmentId = DetachmentId,
        DetachmentIds = SplitCsv(DetachmentIdsCsv),
        CatalogueVersion = CatalogueVersion,
        CreatedUtc = CreatedUtc,
        ModifiedUtc = Timestamp ?? CreatedUtc,
        Units = DeserializeUnits(UnitsJson),
    };

    public static RosterEntity From(string userId, Roster roster) => new()
    {
        PartitionKey = userId,
        RowKey = roster.Id,
        Name = roster.Name,
        Faction = roster.Faction,
        PointsLimit = roster.PointsLimit,
        DetachmentId = roster.EffectiveDetachmentIds.Count > 0 ? roster.EffectiveDetachmentIds[0] : string.Empty,
        DetachmentIdsCsv = string.Join(',', roster.EffectiveDetachmentIds),
        CatalogueVersion = roster.CatalogueVersion,
        CreatedUtc = roster.CreatedUtc,
        UnitsJson = JsonSerializer.Serialize(roster.Units, UnitsJsonOptions),
    };

    private static List<RosterUnit> DeserializeUnits(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<RosterUnit>>(json, UnitsJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<string> SplitCsv(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

/// <summary>
/// <see cref="IRosterRepository"/> backed by an Azure Table named <c>Rosters</c>. The table is created on
/// first use, so no provisioning step is required for local (Azurite) or cloud storage.
/// </summary>
public sealed class TableRosterRepository : IRosterRepository
{
    private const string TableName = "Rosters";
    private readonly TableServiceClient _service;
    private TableClient? _table;

    public TableRosterRepository(TableServiceClient service) => _service = service;

    private async Task<TableClient> GetTableAsync(CancellationToken ct)
    {
        if (_table is not null)
            return _table;

        var table = _service.GetTableClient(TableName);
        await table.CreateIfNotExistsAsync(ct).ConfigureAwait(false);
        return _table = table;
    }

    public async Task<IReadOnlyList<Roster>> ListAsync(string userId, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);

        var rosters = new List<Roster>();
        var query = table.QueryAsync<RosterEntity>(e => e.PartitionKey == userId, cancellationToken: cancellationToken);
        await foreach (var entity in query.ConfigureAwait(false))
            rosters.Add(entity.ToRoster());

        return rosters
            .OrderByDescending(r => r.ModifiedUtc)
            .ToList();
    }

    public async Task<Roster?> GetAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await table
                .GetEntityAsync<RosterEntity>(userId, id, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return response.Value.ToRoster();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<Roster> UpsertAsync(string userId, Roster roster, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(roster.Id))
            roster.Id = Guid.NewGuid().ToString("n");

        if (roster.CreatedUtc == default)
            roster.CreatedUtc = DateTimeOffset.UtcNow;

        var entity = RosterEntity.From(userId, roster);
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);

        // Re-read so the caller gets the server-assigned Timestamp (reads-after-write are consistent).
        var saved = await GetAsync(userId, roster.Id, cancellationToken).ConfigureAwait(false);
        if (saved is not null)
            return saved;

        roster.ModifiedUtc = DateTimeOffset.UtcNow;
        return roster;
    }

    public async Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await table.DeleteEntityAsync(userId, id, ETag.All, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Already gone — treat delete as idempotent.
        }
    }
}
