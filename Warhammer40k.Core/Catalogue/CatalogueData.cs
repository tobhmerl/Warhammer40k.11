using System.Text.Json.Serialization;

namespace Warhammer40k.Core.Catalogue;

/// <summary>
/// The root catalogue document: one faction's datasheets plus any Pantheon bindings. Deserialized from
/// a <c>*-catalogue-seed.json</c> file and enriched by <see cref="CatalogueSeedLoader"/>.
/// </summary>
public sealed class CatalogueData
{
    [JsonPropertyName("faction")] public string Faction { get; set; } = "";

    /// <summary>Optional version/stamp the seed may carry; rosters record the catalogue version they were built against.</summary>
    [JsonPropertyName("version")] public string? Version { get; set; }

    [JsonPropertyName("datasheets")] public List<Datasheet> Datasheets { get; set; } = [];

    [JsonPropertyName("pantheonBindings")] public List<PantheonBinding> PantheonBindings { get; set; } = [];

    /// <summary>Finds a datasheet by its derived <see cref="Datasheet.Id"/>.</summary>
    public Datasheet? FindById(string id) =>
        Datasheets.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Finds the Pantheon binding that applies to the given datasheet name, if any.</summary>
    public PantheonBinding? FindBindingForUnit(string datasheetName) =>
        PantheonBindings.FirstOrDefault(b => string.Equals(b.Unit, datasheetName, StringComparison.OrdinalIgnoreCase));
}
