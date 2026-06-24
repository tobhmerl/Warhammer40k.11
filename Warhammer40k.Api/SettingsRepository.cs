using Azure;
using Azure.Data.Tables;
using Warhammer40k.Core;

namespace Warhammer40k.Api;

/// <summary>
/// Per-user settings persistence (AB8). One small entity per user (PartitionKey = user id,
/// RowKey = <c>settings</c>); reads return <c>null</c> when the user has never saved settings.
/// </summary>
public interface ISettingsRepository
{
    Task<UserSettings?> GetAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserSettings> SaveAsync(string userId, UserSettings settings, CancellationToken cancellationToken = default);
}

/// <summary>Azure Table Storage row for a user's <see cref="UserSettings"/>.</summary>
public sealed class SettingsEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = "settings";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public int DefaultPointsLimit { get; set; } = 2000;
    public string Theme { get; set; } = AppThemes.Default;
    public bool PlayHudSticky { get; set; }
    public bool PlayCardSwipe { get; set; }

    public UserSettings ToSettings() => new()
    {
        DefaultPointsLimit = DefaultPointsLimit,
        Theme = AppThemes.Normalize(Theme),
        PlayHudSticky = PlayHudSticky,
        PlayCardSwipe = PlayCardSwipe,
    };

    public static SettingsEntity From(string userId, UserSettings settings) => new()
    {
        PartitionKey = userId,
        RowKey = "settings",
        DefaultPointsLimit = settings.DefaultPointsLimit,
        Theme = AppThemes.Normalize(settings.Theme),
        PlayHudSticky = settings.PlayHudSticky,
        PlayCardSwipe = settings.PlayCardSwipe,
    };
}

/// <summary>
/// <see cref="ISettingsRepository"/> backed by an Azure Table named <c>Settings</c>, created on first use.
/// </summary>
public sealed class TableSettingsRepository : ISettingsRepository
{
    private const string TableName = "Settings";
    private const string SettingsRowKey = "settings";
    private readonly TableServiceClient _service;
    private TableClient? _table;

    public TableSettingsRepository(TableServiceClient service) => _service = service;

    private async Task<TableClient> GetTableAsync(CancellationToken ct)
    {
        if (_table is not null)
            return _table;

        var table = _service.GetTableClient(TableName);
        await table.CreateIfNotExistsAsync(ct).ConfigureAwait(false);
        return _table = table;
    }

    public async Task<UserSettings?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await table
                .GetEntityAsync<SettingsEntity>(userId, SettingsRowKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return response.Value.ToSettings();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<UserSettings> SaveAsync(string userId, UserSettings settings, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        var entity = SettingsEntity.From(userId, settings);
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return entity.ToSettings();
    }
}
