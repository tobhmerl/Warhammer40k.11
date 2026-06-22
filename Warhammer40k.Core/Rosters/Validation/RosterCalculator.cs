using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Rosters.Validation;

/// <summary>
/// Pure points math shared by the rules engine and the UI (§4 note: points are by model count, never per
/// weapon). Unknown references contribute 0 so a partially-built roster never throws.
/// </summary>
public static class RosterCalculator
{
    /// <summary>Points for a unit's chosen size: the datasheet <see cref="PointsOption"/> whose Models == ModelCount.</summary>
    public static int UnitPoints(RosterUnit unit, Datasheet? datasheet)
    {
        if (datasheet is null)
            return 0;

        var option = datasheet.PointsOptions.FirstOrDefault(o => o.Models == unit.ModelCount);
        return option?.Points ?? 0;
    }

    /// <summary>Points for a unit's assigned enhancement, resolved against the selected detachment (0 if unknown).</summary>
    public static int EnhancementPoints(RosterUnit unit, Detachment? detachment) =>
        detachment is null ? 0 : EnhancementPoints(unit, new[] { detachment });

    /// <summary>Points for a unit's assigned enhancement, resolved across the selected detachments (0 if unknown).</summary>
    public static int EnhancementPoints(RosterUnit unit, IReadOnlyList<Detachment> detachments)
    {
        if (string.IsNullOrEmpty(unit.AssignedEnhancementId))
            return 0;

        foreach (var detachment in detachments)
        {
            if (detachment.FindEnhancement(unit.AssignedEnhancementId) is { } enhancement)
                return enhancement.Points;
        }

        return 0;
    }

    /// <summary>Total roster points: Σ unit points + Σ enhancement points + Σ Pantheon surcharges (rule R1).</summary>
    public static int TotalPoints(Roster roster, CatalogueData catalogue, Detachment? detachment) =>
        TotalPoints(roster, catalogue, detachment is null ? Array.Empty<Detachment>() : new[] { detachment });

    /// <summary>Total roster points across one or more selected detachments (rule R1).</summary>
    public static int TotalPoints(Roster roster, CatalogueData catalogue, IReadOnlyList<Detachment> detachments)
    {
        var total = 0;
        foreach (var unit in roster.Units)
        {
            total += UnitPoints(unit, catalogue.FindById(unit.DatasheetId));
            total += EnhancementPoints(unit, detachments);
            total += unit.BindingSurcharge;
        }

        return total;
    }
}
