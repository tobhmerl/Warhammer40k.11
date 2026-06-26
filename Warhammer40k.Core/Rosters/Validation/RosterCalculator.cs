using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Rosters.Validation;

/// <summary>
/// Pure points math shared by the rules engine and the UI (§4 note: points are by model count, never per
/// weapon). Unknown references contribute 0 so a partially-built roster never throws.
/// </summary>
public static class RosterCalculator
{
    /// <summary>Points for a unit's chosen size: the datasheet <see cref="PointsOption"/> whose Models == ModelCount.</summary>
    public static int UnitPoints(RosterUnit unit, Datasheet? datasheet) =>
        UnitPoints(unit, datasheet, copyRank: 1);

    /// <summary>
    /// Points for a unit's chosen size at a given 1-based copy rank. When the datasheet escalates
    /// (<see cref="Datasheet.EscalationRank"/> &gt; 0) and this copy's rank reaches it, the option's
    /// <see cref="PointsOption.EscalatedPoints"/> is used; otherwise the base <see cref="PointsOption.Points"/>.
    /// </summary>
    public static int UnitPoints(RosterUnit unit, Datasheet? datasheet, int copyRank)
    {
        if (datasheet is null)
            return 0;

        var option = datasheet.PointsOptions.FirstOrDefault(o => o.Models == unit.ModelCount);
        if (option is null)
            return 0;

        if (datasheet.EscalationRank > 0 && copyRank >= datasheet.EscalationRank && option.EscalatedPoints is { } escalated)
            return escalated;

        return option.Points;
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
        var seen = new Dictionary<string, int>();
        foreach (var unit in roster.Units)
        {
            seen.TryGetValue(unit.DatasheetId, out var prior);
            var rank = prior + 1;
            seen[unit.DatasheetId] = rank;

            total += UnitPoints(unit, catalogue.FindById(unit.DatasheetId), rank);
            total += EnhancementPoints(unit, detachments);
            total += unit.BindingSurcharge;
        }

        return total;
    }

    /// <summary>
    /// The 1-based copy rank of <paramref name="unit"/> among same-datasheet units in roster order
    /// (its 1st copy = 1, 2nd = 2, …). Drives per-copy escalated pricing in the editor display.
    /// </summary>
    public static int CopyRank(Roster roster, RosterUnit unit)
    {
        var rank = 0;
        foreach (var u in roster.Units)
        {
            if (u.DatasheetId == unit.DatasheetId)
                rank++;
            if (ReferenceEquals(u, unit))
                return rank;
        }

        return rank == 0 ? 1 : rank;
    }
}
