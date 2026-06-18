using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Api;

/// <summary>
/// Per-user persistence for the editable Necron catalogue (AB7). A user who has never edited has no row, so
/// reads fall back to the embedded default; the first save materializes their own copy. Every operation is
/// scoped to <paramref name="userId"/> (the Table Storage partition key).
/// </summary>
public interface ICatalogueRepository
{
    /// <summary>Gets the user's saved catalogue (enriched), or <c>null</c> when they have none.</summary>
    Task<CatalogueData?> GetAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Saves the user's catalogue and returns the enriched copy.</summary>
    Task<CatalogueData> SaveAsync(string userId, CatalogueData catalogue, CancellationToken cancellationToken = default);

    /// <summary>Deletes the user's saved catalogue, reverting them to the embedded default.</summary>
    Task ResetAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>Splits a large JSON document across Table Storage string properties (each ≤ 64 KiB) and rejoins it.</summary>
public static class CatalogueChunking
{
    /// <summary>Characters per chunk; kept under the 64 KiB Table Storage string-property limit (UTF-16).</summary>
    public const int DefaultChunkSize = 30_000;

    public static IReadOnlyList<string> Split(string text, int chunkSize = DefaultChunkSize)
    {
        if (chunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSize));

        var chunks = new List<string>();
        for (var i = 0; i < text.Length; i += chunkSize)
            chunks.Add(text.Substring(i, Math.Min(chunkSize, text.Length - i)));

        if (chunks.Count == 0)
            chunks.Add(string.Empty);

        return chunks;
    }

    public static string Join(IReadOnlyList<string> chunks) => string.Concat(chunks);
}

/// <summary>
/// <see cref="ICatalogueRepository"/> backed by an Azure Table named <c>Catalogue</c>. The whole catalogue is
/// stored as one entity per user (PartitionKey = user id, RowKey = <c>necrons</c>) with its JSON chunked across
/// <c>Json_0..Json_n</c> properties — the document (~122 KB) exceeds the 64 KiB property limit but fits the
/// 1 MiB entity limit. The table is created on first use.
/// </summary>
public sealed class TableCatalogueRepository : ICatalogueRepository
{
    private const string TableName = "Catalogue";
    private const string CatalogueRowKey = "necrons";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TableServiceClient _service;
    private TableClient? _table;

    public TableCatalogueRepository(TableServiceClient service) => _service = service;

    private async Task<TableClient> GetTableAsync(CancellationToken ct)
    {
        if (_table is not null)
            return _table;

        var table = _service.GetTableClient(TableName);
        await table.CreateIfNotExistsAsync(ct).ConfigureAwait(false);
        return _table = table;
    }

    public async Task<CatalogueData?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await table
                .GetEntityAsync<TableEntity>(userId, CatalogueRowKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var json = ExtractJson(response.Value);
            return string.IsNullOrWhiteSpace(json) ? null : CatalogueSeedLoader.Load(json);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<CatalogueData> SaveAsync(string userId, CatalogueData catalogue, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);

        // Re-derive ids/flags before storing so saved data is internally consistent (Enrich is id-preserving).
        CatalogueSeedLoader.Enrich(catalogue);
        var json = JsonSerializer.Serialize(catalogue, JsonOptions);

        var entity = BuildEntity(userId, json);
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);

        return catalogue;
    }

    public async Task ResetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await table.DeleteEntityAsync(userId, CatalogueRowKey, ETag.All, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Already absent — reset is idempotent.
        }
    }

    /// <summary>Builds the chunked Table entity for a user's catalogue JSON (exposed for round-trip testing).</summary>
    public static TableEntity BuildEntity(string userId, string json)
    {
        var chunks = CatalogueChunking.Split(json);
        var entity = new TableEntity(userId, CatalogueRowKey)
        {
            ["ChunkCount"] = chunks.Count,
        };

        for (var i = 0; i < chunks.Count; i++)
            entity[$"Json_{i}"] = chunks[i];

        return entity;
    }

    /// <summary>Reassembles the catalogue JSON from a chunked Table entity (exposed for round-trip testing).</summary>
    public static string ExtractJson(TableEntity entity)
    {
        var count = entity.GetInt32("ChunkCount") ?? 0;
        if (count <= 0)
            return string.Empty;

        var parts = new List<string>(count);
        for (var i = 0; i < count; i++)
            parts.Add(entity.GetString($"Json_{i}") ?? string.Empty);

        return CatalogueChunking.Join(parts);
    }
}
