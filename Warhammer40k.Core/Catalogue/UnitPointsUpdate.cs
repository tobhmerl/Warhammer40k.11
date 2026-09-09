using System.Globalization;
using Warhammer40k.Core.Text;

namespace Warhammer40k.Core.Catalogue;

/// <summary>Applies a dated unit-price table without replacing a user's catalogue or repeatedly erasing manual price edits.</summary>
public static class UnitPointsUpdate
{
    public static int Apply(CatalogueData catalogue, CatalogueData reference)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(reference);
        if (!string.Equals(catalogue.Faction, reference.Faction, StringComparison.OrdinalIgnoreCase)
            || !TryVersion(reference.UnitPointsVersion, out var revision)
            || (TryVersion(catalogue.UnitPointsVersion, out var current) && current >= revision))
            return 0;

        var matched = 0;
        var changed = 0;
        foreach (var sheet in catalogue.Datasheets)
        {
            var candidates = reference.Datasheets.Where(item => string.Equals(item.Name, sheet.Name, StringComparison.Ordinal)).ToList();
            if (candidates.Count == 0)
            {
                var id = NormalizedId(sheet);
                candidates = reference.Datasheets.Where(item => NormalizedId(item) == id).ToList();
            }
            if (candidates.Count != 1)
                continue;

            var source = candidates[0];
            if (source.PointsOptions.Count == 0)
                continue;
            matched++;
            var unitChanged = sheet.Points != source.Points || sheet.EscalationRank != source.EscalationRank;
            sheet.Points = source.Points;
            sheet.EscalationRank = source.EscalationRank;
            foreach (var sourceTier in source.PointsOptions)
            {
                var existing = sheet.PointsOptions.Where(tier => tier.Models == sourceTier.Models).ToList();
                if (existing.Count == 0)
                {
                    sheet.PointsOptions.Add(new PointsOption
                    {
                        Models = sourceTier.Models, Points = sourceTier.Points, EscalatedPoints = sourceTier.EscalatedPoints,
                    });
                    unitChanged = true;
                    continue;
                }
                foreach (var tier in existing)
                {
                    unitChanged |= tier.Points != sourceTier.Points || tier.EscalatedPoints != sourceTier.EscalatedPoints;
                    tier.Points = sourceTier.Points;
                    tier.EscalatedPoints = sourceTier.EscalatedPoints;
                }
            }
            // Extra custom model-count tiers have no authoritative replacement and are retained as entered.
            if (unitChanged)
                changed++;
        }
        if (matched > 0)
            catalogue.UnitPointsVersion = reference.UnitPointsVersion;
        return changed;
    }

    private static string NormalizedId(Datasheet sheet) => Slugger.Slug(string.IsNullOrWhiteSpace(sheet.Id) ? sheet.Name : sheet.Id);

    private static bool TryVersion(string? version, out DateOnly date) =>
        DateOnly.TryParseExact(version, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
