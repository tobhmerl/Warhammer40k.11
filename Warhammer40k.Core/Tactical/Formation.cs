namespace Warhammer40k.Core.Tactical;

/// <summary>
/// Pure geometry for moving a whole unit as one rigid formation on the tactical map: translate with the
/// formation clamped to the board, and rotate around a pivot. Working from each model's drag-start
/// position (a <see cref="Placement"/>) keeps the formation exact over a long drag instead of
/// accumulating per-move rounding.
/// </summary>
public static class Formation
{
    /// <summary>A token paired with its position at the start of the current drag.</summary>
    public readonly record struct Placement(MapToken Token, double StartXInches, double StartYInches);

    /// <summary>The centroid (average of model centers) of the given tokens.</summary>
    public static (double X, double Y) Centroid(IReadOnlyCollection<MapToken> tokens)
    {
        if (tokens.Count == 0)
            return (0, 0);
        return (tokens.Average(t => t.XInches), tokens.Average(t => t.YInches));
    }

    /// <summary>
    /// The largest translation no bigger than (<paramref name="dx"/>, <paramref name="dy"/>) that keeps
    /// every member's base fully on the board — so a unit dragged past an edge stops at the edge as one
    /// piece instead of squashing.
    /// </summary>
    public static (double Dx, double Dy) ClampDelta(IReadOnlyList<Placement> members, double dx, double dy, MapDefinition map)
    {
        double minDx = double.MinValue, maxDx = double.MaxValue;
        double minDy = double.MinValue, maxDy = double.MaxValue;
        foreach (var (token, sx, sy) in members)
        {
            var r = Coherency.RadiusInches(token);
            minDx = Math.Max(minDx, r - sx);
            maxDx = Math.Min(maxDx, map.WidthInches - r - sx);
            minDy = Math.Max(minDy, r - sy);
            maxDy = Math.Min(maxDy, map.HeightInches - r - sy);
        }
        if (minDx > maxDx)
            dx = 0;
        else
            dx = Math.Clamp(dx, minDx, maxDx);
        if (minDy > maxDy)
            dy = 0;
        else
            dy = Math.Clamp(dy, minDy, maxDy);
        return (dx, dy);
    }

    /// <summary>
    /// Moves every member to its start position shifted by the (board-clamped) delta, keeping the
    /// formation rigid.
    /// </summary>
    public static void Translate(IReadOnlyList<Placement> members, double dx, double dy, MapDefinition map)
    {
        (dx, dy) = ClampDelta(members, dx, dy, map);
        foreach (var (token, sx, sy) in members)
        {
            token.XInches = sx + dx;
            token.YInches = sy + dy;
        }
    }

    /// <summary>
    /// Rotates every member's start position by <paramref name="angleRadians"/> around the pivot
    /// (<paramref name="originX"/>, <paramref name="originY"/>), clamping each model onto the board.
    /// </summary>
    public static void Rotate(IReadOnlyList<Placement> members, double originX, double originY, double angleRadians, MapDefinition map)
    {
        var cos = Math.Cos(angleRadians);
        var sin = Math.Sin(angleRadians);
        foreach (var (token, sx, sy) in members)
        {
            var relX = sx - originX;
            var relY = sy - originY;
            var r = Coherency.RadiusInches(token);
            token.XInches = Math.Clamp(originX + relX * cos - relY * sin, r, map.WidthInches - r);
            token.YInches = Math.Clamp(originY + relX * sin + relY * cos, r, map.HeightInches - r);
        }
    }
}
