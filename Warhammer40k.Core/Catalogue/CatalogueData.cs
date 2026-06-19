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

    /// <summary>
    /// The bodyguard units a Leader can attach to (forward direction: the Leader's
    /// <see cref="Datasheet.LeaderTargetIds"/>), in catalogue order.
    /// </summary>
    public IReadOnlyList<Datasheet> UnitsLedBy(Datasheet leader)
    {
        ArgumentNullException.ThrowIfNull(leader);
        if (leader.LeaderTargetIds.Count == 0)
            return [];
        return Datasheets
            .Where(d => leader.LeaderTargetIds.Contains(d.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// The Leaders that can attach to a given bodyguard unit (reverse direction: any datasheet whose
    /// <see cref="Datasheet.LeaderTargetIds"/> contains this unit's id), in catalogue order.
    /// </summary>
    public IReadOnlyList<Datasheet> LeadersOf(Datasheet unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (string.IsNullOrEmpty(unit.Id))
            return [];
        return Datasheets
            .Where(d => d.LeaderTargetIds.Contains(unit.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Every datasheet this one can be fielded attached to, in either direction: the units it can lead
    /// <b>and</b> the Leaders that can join it. Distinct, self-excluded, in catalogue order. Used by the
    /// Catalogue's attachment filter so selecting a unit reveals exactly who it can be attached to/with.
    /// </summary>
    public IReadOnlyList<Datasheet> AttachmentPartners(Datasheet datasheet)
    {
        ArgumentNullException.ThrowIfNull(datasheet);
        var partnerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var led in UnitsLedBy(datasheet))
            partnerIds.Add(led.Id);
        foreach (var leader in LeadersOf(datasheet))
            partnerIds.Add(leader.Id);
        partnerIds.Remove(datasheet.Id);
        return Datasheets.Where(d => partnerIds.Contains(d.Id)).ToList();
    }
}
