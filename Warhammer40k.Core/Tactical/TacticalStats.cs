using System.Globalization;
using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Tactical;

/// <summary>
/// Extracts the tactical-map stats (Move, longest gun range) from game notation. Stat strings keep their
/// display form (<c>10"</c>, <c>Melee</c>, <c>D6</c>), so these helpers parse the leading number and
/// return <c>null</c> for anything non-numeric — a missing stat simply draws no range ring.
/// </summary>
public static class TacticalStats
{
    /// <summary>The extra inches added to Move for the charge-threat ring (a maximum 2D6 charge).</summary>
    public const double ChargeInches = 12;

    /// <summary>The leading number of a stat string in inches (<c>10"</c> → 10, <c>3.5"</c> → 3.5), or null.</summary>
    public static double? ParseInches(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var s = text.Trim();
        var end = 0;
        while (end < s.Length && (char.IsAsciiDigit(s[end]) || s[end] == '.'))
            end++;
        if (end == 0)
            return null;
        return double.TryParse(s[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>The datasheet's best Move characteristic in inches, or null when unknown.</summary>
    public static double? MoveInchesFor(Datasheet? datasheet) =>
        datasheet?.StatProfiles.Select(p => ParseInches(p.Move)).Max();

    /// <summary>The datasheet's longest ranged-weapon range in inches, or null when it has no ranged weapons.</summary>
    public static double? MaxRangedInchesFor(Datasheet? datasheet) =>
        datasheet?.Weapons.Select(w => ParseInches(w.Range)).Max();
}
