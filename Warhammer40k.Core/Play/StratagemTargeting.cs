using System.Text.RegularExpressions;

namespace Warhammer40k.Core.Play;

/// <summary>
/// Matches the capitalized keyword requirements in a stratagem's Target clause. Board conditions such as
/// range, being selected as a target, or having already shot remain checks for the player.
/// </summary>
public static class StratagemTargeting
{
    private static readonly Regex KeywordRun = new(@"[A-Z][A-Z'\u2019-]+(?:[ /][A-Z][A-Z'\u2019-]+)*", RegexOptions.Compiled);
    private static readonly Regex ExcludeClause = new(@"\((?:excluding|excludes|except|but not)\s+([^)]*)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Adjacent keywords are required together; slash-separated keywords are alternatives. The catalogue's
    /// vocabulary preserves multiword keywords such as NECRON WARRIORS and DESTROYER CULT as single terms.
    /// </summary>
    public static bool AppliesTo(string? target, IEnumerable<string> unitKeywords, IEnumerable<string>? catalogueKeywords = null)
    {
        if (string.IsNullOrWhiteSpace(target))
            return true;

        var owned = unitKeywords.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vocabulary = (catalogueKeywords ?? [])
            .Select(Normalize)
            .Concat(owned)
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(k => k.Length)
            .ToArray();
        var text = target.Replace('\u2019', '\'').Replace('\u00a0', ' ');

        foreach (Match clause in ExcludeClause.Matches(text))
            foreach (Match keyword in KeywordRun.Matches(clause.Groups[1].Value))
                if (MatchesExpression(keyword.Value, owned, vocabulary))
                    return false;

        var includes = KeywordRun.Matches(ExcludeClause.Replace(text, " "));
        return includes.Count == 0 || includes.Any(match => MatchesExpression(match.Value, owned, vocabulary));
    }

    private static bool MatchesExpression(string expression, IReadOnlySet<string> owned, IReadOnlyList<string> vocabulary)
    {
        var groups = new List<List<string>>();
        var position = 0;
        var alternative = false;
        while (position < expression.Length)
        {
            if (char.IsWhiteSpace(expression[position]))
            {
                position++;
                continue;
            }
            if (expression[position] == '/')
            {
                alternative = true;
                position++;
                continue;
            }

            // Longest known keyword first: NECRON WARRIORS must not become two unrelated requirements.
            var term = vocabulary.FirstOrDefault(keyword =>
                expression.AsSpan(position).StartsWith(keyword, StringComparison.OrdinalIgnoreCase)
                && (position + keyword.Length == expression.Length
                    || expression[position + keyword.Length] is ' ' or '/'));
            if (term is null)
            {
                var end = position;
                while (end < expression.Length && expression[end] is not (' ' or '/'))
                    end++;
                term = expression[position..end];
            }
            position += term.Length;

            if (alternative && groups.Count > 0)
                groups[^1].Add(term);
            else
                groups.Add([term]);
            alternative = false;
        }

        return groups.Count > 0 && groups.All(group => group.Any(owned.Contains));
    }

    private static string Normalize(string keyword)
    {
        var colon = keyword.IndexOf(':');
        return (colon >= 0 ? keyword[(colon + 1)..] : keyword).Trim().Replace('\u2019', '\'');
    }
}
