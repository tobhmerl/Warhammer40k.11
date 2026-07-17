namespace Warhammer40k.Core.Tactical;

/// <summary>
/// Pure unit-coherency math for the tactical map. Current-edition rule: every model must be within
/// <see cref="ThresholdInches"/> of at least one other model in its unit — two other models for units of
/// <see cref="LargeUnitSize"/>+ models. Single-model units are always coherent. Distances are measured
/// edge-to-edge (closest points of the round bases), matching how the game measures "within".
/// </summary>
public static class Coherency
{
    /// <summary>The coherency distance in inches.</summary>
    public const double ThresholdInches = 2.0;

    /// <summary>Units with this many models (or more) require two coherency neighbours instead of one.</summary>
    public const int LargeUnitSize = 7;

    /// <summary>A model's base radius in inches (base diameter mm -> inches / 2).</summary>
    public static double RadiusInches(MapToken token) => token.BaseMm / 25.4 / 2.0;

    /// <summary>The edge-to-edge distance in inches between two models (0 when their bases overlap).</summary>
    public static double EdgeDistanceInches(MapToken a, MapToken b)
    {
        var dx = a.XInches - b.XInches;
        var dy = a.YInches - b.YInches;
        var centre = Math.Sqrt(dx * dx + dy * dy);
        var edge = centre - RadiusInches(a) - RadiusInches(b);
        return edge < 0 ? 0 : edge;
    }

    /// <summary>
    /// How many coherency neighbours a model needs, given its unit's model count: 0 for a single-model unit,
    /// 2 for a unit of <see cref="LargeUnitSize"/>+ models, otherwise 1 — capped so it never exceeds the
    /// number of other models present.
    /// </summary>
    public static int RequiredNeighbours(int unitModelCount)
    {
        if (unitModelCount <= 1)
            return 0;
        var required = unitModelCount >= LargeUnitSize ? 2 : 1;
        return Math.Min(required, unitModelCount - 1);
    }

    /// <summary>
    /// True when <paramref name="model"/> is in coherency: it is within <see cref="ThresholdInches"/> of at
    /// least the required number of the <paramref name="unitOthers"/> (its unit's other models).
    /// </summary>
    public static bool IsInCoherency(MapToken model, IReadOnlyCollection<MapToken> unitOthers)
    {
        var required = RequiredNeighbours(unitOthers.Count + 1);
        if (required == 0)
            return true;
        var within = unitOthers.Count(o => EdgeDistanceInches(model, o) <= ThresholdInches);
        return within >= required;
    }

    /// <summary>
    /// The ids of every model that is currently out of coherency, across all units in <paramref name="tokens"/>.
    /// Models are grouped by <see cref="MapToken.RosterUnitId"/>; tokens without a unit id (e.g. opponent
    /// markers) are singletons and never flagged.
    /// </summary>
    public static HashSet<string> BrokenTokenIds(IEnumerable<MapToken> tokens)
    {
        var broken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in tokens.Where(t => !string.IsNullOrEmpty(t.RosterUnitId))
                     .GroupBy(t => t.RosterUnitId, StringComparer.Ordinal))
        {
            var members = group.ToList();
            if (members.Count <= 1)
                continue;
            foreach (var model in members)
            {
                var others = members.Where(m => !ReferenceEquals(m, model)).ToList();
                if (!IsInCoherency(model, others))
                    broken.Add(model.Id);
            }
        }
        return broken;
    }
}
