using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Warhammer40k.Core.Tactical;

namespace Warhammer40k.Api;

/// <summary>
/// Per-user persistence for <see cref="TacticalPlan"/>s (PartitionKey = user id, RowKey = plan id), so each
/// account only ever sees its own tactical setups. Tokens are stored as a JSON string so the shape can grow.
/// </summary>
public interface ITacticalPlanRepository
{
    Task<IReadOnlyList<TacticalPlan>> ListAsync(string userId, CancellationToken cancellationToken = default);

    Task<TacticalPlan?> GetAsync(string userId, string id, CancellationToken cancellationToken = default);

    Task<TacticalPlan> UpsertAsync(string userId, TacticalPlan plan, CancellationToken cancellationToken = default);

    Task DeleteAsync(string userId, string id, CancellationToken cancellationToken = default);
}

/// <summary>Azure Table Storage row for a <see cref="TacticalPlan"/>.</summary>
public sealed class TacticalPlanEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Name { get; set; } = string.Empty;
    public string RosterId { get; set; } = string.Empty;
    public string MapId { get; set; } = TacticalMaps.DefaultMapId;
    public string TokensJson { get; set; } = "[]";
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ModifiedUtc { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TacticalPlan ToPlan() => new()
    {
        Id = RowKey,
        Name = Name,
        RosterId = RosterId,
        MapId = MapId,
        Tokens = DeserializeTokens(TokensJson),
        CreatedUtc = CreatedUtc,
        ModifiedUtc = ModifiedUtc,
    };

    public static TacticalPlanEntity From(string userId, TacticalPlan plan) => new()
    {
        PartitionKey = userId,
        RowKey = plan.Id,
        Name = plan.Name,
        RosterId = plan.RosterId,
        MapId = plan.MapId,
        TokensJson = JsonSerializer.Serialize(plan.Tokens, JsonOptions),
        CreatedUtc = plan.CreatedUtc,
        ModifiedUtc = plan.ModifiedUtc,
    };

    private static List<MapToken> DeserializeTokens(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<MapToken>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>
/// <see cref="ITacticalPlanRepository"/> backed by an Azure Table named <c>TacticalPlans</c>, created on
/// first use (no provisioning needed for Azurite or cloud storage).
/// </summary>
public sealed class TableTacticalPlanRepository : ITacticalPlanRepository
{
    private const string TableName = "TacticalPlans";
    private readonly TableServiceClient _service;
    private TableClient? _table;

    public TableTacticalPlanRepository(TableServiceClient service) => _service = service;

    private async Task<TableClient> GetTableAsync(CancellationToken ct)
    {
        if (_table is not null)
            return _table;

        var table = _service.GetTableClient(TableName);
        await table.CreateIfNotExistsAsync(ct).ConfigureAwait(false);
        return _table = table;
    }

    public async Task<IReadOnlyList<TacticalPlan>> ListAsync(string userId, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);

        var plans = new List<TacticalPlan>();
        var query = table.QueryAsync<TacticalPlanEntity>(e => e.PartitionKey == userId, cancellationToken: cancellationToken);
        await foreach (var entity in query.ConfigureAwait(false))
            plans.Add(entity.ToPlan());

        return plans.OrderByDescending(p => p.ModifiedUtc).ToList();
    }

    public async Task<TacticalPlan?> GetAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await table
                .GetEntityAsync<TacticalPlanEntity>(userId, id, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return response.Value.ToPlan();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<TacticalPlan> UpsertAsync(string userId, TacticalPlan plan, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(plan.Id))
            plan.Id = Guid.NewGuid().ToString("n");
        if (plan.CreatedUtc == default)
            plan.CreatedUtc = DateTimeOffset.UtcNow;
        plan.ModifiedUtc = DateTimeOffset.UtcNow;

        var entity = TacticalPlanEntity.From(userId, plan);
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);

        var saved = await GetAsync(userId, plan.Id, cancellationToken).ConfigureAwait(false);
        return saved ?? plan;
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
            // Already gone — idempotent delete.
        }
    }
}
