using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Warhammer40k.Core.Play;

namespace Warhammer40k.Api;

/// <summary>
/// Per-user persistence for the cross-roster <see cref="ScheduleLibrary"/>. One small entity per user
/// (PartitionKey = user id, RowKey = <c>schedules</c>); reads return <c>null</c> when the user has never
/// saved a library. The schedules themselves are stored as a JSON string so the shape can grow freely.
/// </summary>
public interface IScheduleLibraryRepository
{
    Task<ScheduleLibrary?> GetAsync(string userId, CancellationToken cancellationToken = default);

    Task<ScheduleLibrary> SaveAsync(string userId, ScheduleLibrary library, CancellationToken cancellationToken = default);
}

/// <summary>Azure Table Storage row for a user's <see cref="ScheduleLibrary"/>.</summary>
public sealed class ScheduleLibraryEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = "schedules";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    /// <summary>The library's schedules serialized as JSON (defaults to an empty array).</summary>
    public string SchedulesJson { get; set; } = "[]";
}

/// <summary>
/// <see cref="IScheduleLibraryRepository"/> backed by an Azure Table named <c>ScheduleLibrary</c>,
/// created on first use.
/// </summary>
public sealed class TableScheduleLibraryRepository : IScheduleLibraryRepository
{
    private const string TableName = "ScheduleLibrary";
    private const string LibraryRowKey = "schedules";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TableServiceClient _service;
    private TableClient? _table;

    public TableScheduleLibraryRepository(TableServiceClient service) => _service = service;

    private async Task<TableClient> GetTableAsync(CancellationToken ct)
    {
        if (_table is not null)
            return _table;

        var table = _service.GetTableClient(TableName);
        await table.CreateIfNotExistsAsync(ct).ConfigureAwait(false);
        return _table = table;
    }

    public async Task<ScheduleLibrary?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await table
                .GetEntityAsync<ScheduleLibraryEntity>(userId, LibraryRowKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return new ScheduleLibrary { Schedules = Deserialize(response.Value.SchedulesJson) };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<ScheduleLibrary> SaveAsync(string userId, ScheduleLibrary library, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        var entity = new ScheduleLibraryEntity
        {
            PartitionKey = userId,
            RowKey = LibraryRowKey,
            SchedulesJson = JsonSerializer.Serialize(library.Schedules, JsonOptions),
        };
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return new ScheduleLibrary { Schedules = library.Schedules };
    }

    private static List<AbilitySchedule> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<AbilitySchedule>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
