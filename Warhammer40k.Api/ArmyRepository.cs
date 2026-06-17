using Azure;
using Azure.Data.Tables;
using Warhammer40k.Core;

namespace Warhammer40k.Api;

/// <summary>
/// Per-user army persistence. Every operation is scoped to <paramref name="userId"/>, which is the
/// Table Storage partition key, so one user can never read or modify another user's data.
/// </summary>
public interface IArmyRepository
{
    /// <summary>Lists a user's armies, newest first.</summary>
    Task<IReadOnlyList<Army>> ListAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Gets one army in the user's partition, or <c>null</c> if it does not exist.</summary>
    Task<Army?> GetAsync(string userId, string id, CancellationToken cancellationToken = default);

    /// <summary>Creates (assigns a new id when <see cref="Army.Id"/> is empty) or replaces an army.</summary>
    Task<Army> UpsertAsync(string userId, Army army, CancellationToken cancellationToken = default);

    /// <summary>Deletes an army; a no-op when it does not exist.</summary>
    Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default);
}

/// <summary>Azure Table Storage row for an <see cref="Army"/> (PartitionKey = user id, RowKey = army id).</summary>
public sealed class ArmyEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Faction { get; set; } = string.Empty;
    public int Points { get; set; }

    public Army ToArmy() => new()
    {
        Id = RowKey,
        Name = Name,
        Faction = Faction,
        Points = Points,
        UpdatedUtc = Timestamp,
    };

    public static ArmyEntity From(string userId, Army army) => new()
    {
        PartitionKey = userId,
        RowKey = army.Id,
        Name = army.Name,
        Faction = army.Faction,
        Points = army.Points,
    };
}

/// <summary>
/// <see cref="IArmyRepository"/> backed by an Azure Table named <c>Armies</c>. The table is created
/// on first use, so no provisioning step is required for local (Azurite) or cloud storage.
/// </summary>
public sealed class TableArmyRepository : IArmyRepository
{
    private const string TableName = "Armies";
    private readonly TableServiceClient _service;
    private TableClient? _table;

    public TableArmyRepository(TableServiceClient service) => _service = service;

    private async Task<TableClient> GetTableAsync(CancellationToken ct)
    {
        if (_table is not null)
            return _table;

        var table = _service.GetTableClient(TableName);
        await table.CreateIfNotExistsAsync(ct).ConfigureAwait(false);
        return _table = table;
    }

    public async Task<IReadOnlyList<Army>> ListAsync(string userId, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);

        var armies = new List<Army>();
        var query = table.QueryAsync<ArmyEntity>(e => e.PartitionKey == userId, cancellationToken: cancellationToken);
        await foreach (var entity in query.ConfigureAwait(false))
            armies.Add(entity.ToArmy());

        return armies
            .OrderByDescending(a => a.UpdatedUtc ?? DateTimeOffset.MinValue)
            .ToList();
    }

    public async Task<Army?> GetAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await table
                .GetEntityAsync<ArmyEntity>(userId, id, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return response.Value.ToArmy();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<Army> UpsertAsync(string userId, Army army, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(army.Id))
            army.Id = Guid.NewGuid().ToString("n");

        var entity = ArmyEntity.From(userId, army);
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);

        // Re-read so the caller gets the server-assigned Timestamp (reads-after-write are consistent).
        var saved = await GetAsync(userId, army.Id, cancellationToken).ConfigureAwait(false);
        if (saved is not null)
            return saved;

        army.UpdatedUtc = DateTimeOffset.UtcNow;
        return army;
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
