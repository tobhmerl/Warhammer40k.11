using Warhammer40k.Api;

namespace Warhammer40k.Tests;

/// <summary>
/// Proves the catalogue's chunked Table Storage mapping round-trips a large document (the catalogue is ~122 KB,
/// over the 64 KiB property limit) and that <see cref="CatalogueChunking"/> splits/joins losslessly.
/// </summary>
public class CatalogueChunkingTests
{
    [Fact]
    public void Split_then_join_is_lossless_for_a_large_document()
    {
        var text = new string('x', 30_000) + "necron" + new string('y', 95_000); // > 64 KiB, not chunk-aligned

        var chunks = CatalogueChunking.Split(text);
        var joined = CatalogueChunking.Join(chunks);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= CatalogueChunking.DefaultChunkSize));
        Assert.Equal(text, joined);
    }

    [Fact]
    public void Split_of_empty_text_yields_a_single_empty_chunk()
    {
        var chunks = CatalogueChunking.Split("");

        Assert.Single(chunks);
        Assert.Equal("", chunks[0]);
    }

    [Fact]
    public void Entity_round_trip_preserves_json_and_keys()
    {
        const string userId = "github|42";
        var json = "{\"faction\":\"Necrons\",\"datasheets\":[" +
                   string.Join(",", Enumerable.Range(0, 3000).Select(i => $"{{\"name\":\"Unit {i}\"}}")) +
                   "]}";

        var entity = TableCatalogueRepository.BuildEntity(userId, json);
        var extracted = TableCatalogueRepository.ExtractJson(entity);

        Assert.Equal(userId, entity.PartitionKey);
        Assert.Equal("necrons", entity.RowKey);
        Assert.True(json.Length > CatalogueChunking.DefaultChunkSize); // exercised multi-chunk path
        Assert.Equal(json, extracted);
    }

    [Fact]
    public void Extract_of_entity_without_chunks_is_empty()
    {
        var entity = new Azure.Data.Tables.TableEntity("u", "necrons");

        Assert.Equal("", TableCatalogueRepository.ExtractJson(entity));
    }
}
