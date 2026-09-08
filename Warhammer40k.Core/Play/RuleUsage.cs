using System.Text.RegularExpressions;

namespace Warhammer40k.Core.Play;

/// <summary>Reads explicit usage periods without confusing a battle round with the whole battle.</summary>
public static class RuleUsage
{
    private static readonly Regex PeriodPattern = new(@"\bonce\s+per\s+(?<period>battle(?:[\s-]+round)?|turn|phase)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsOncePerBattle(string? text) => Periods(text).Contains("battle");

    /// <summary>Periods the app does not track automatically, kept visible rather than consumed for the whole battle.</summary>
    public static string? ManualLimitHint(string? text)
    {
        var periods = Periods(text).Where(period => period != "battle").ToList();
        return periods.Count == 0 ? null : string.Join(" · ", periods.Select(period => "Once per " + period)) + " · check usage";
    }

    private static IEnumerable<string> Periods(string? text)
    {
        var clean = (text ?? "").Replace("^^", "", StringComparison.Ordinal)
            .Replace("**", "", StringComparison.Ordinal).Replace("__", "", StringComparison.Ordinal)
            .Replace('\u2010', '-').Replace('\u2011', '-');
        return PeriodPattern.Matches(clean)
            .Select(match => Regex.Replace(match.Groups["period"].Value, @"[\s-]+", " ").ToLowerInvariant())
            .Distinct(StringComparer.Ordinal);
    }
}
