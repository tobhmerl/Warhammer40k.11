namespace Warhammer40k.Core.Tactical;

/// <summary>
/// Visual styling for map tokens: a short unit abbreviation and a per-unit color drawn from a green family
/// for the player and a red family for the opponent, so distinct units read apart at a glance.
/// </summary>
public static class TokenStyle
{
    /// <summary>Green shades for the player's units, indexed per distinct unit.</summary>
    public static IReadOnlyList<string> PlayerColors { get; } =
    [
        "#2bd47a", "#8bd450", "#3fbfa0", "#5bb85b", "#a7d94e",
        "#1f9e5a", "#57c7a0", "#7bd88f", "#2f8f5b", "#93c74a",
    ];

    /// <summary>Red/amber shades for the opponent's units, indexed per distinct unit.</summary>
    public static IReadOnlyList<string> OpponentColors { get; } =
    [
        "#e0563b", "#c0392b", "#e08a3b", "#d24d6a", "#b5462f",
        "#e76f51", "#cf5c5c", "#a3372a", "#e0a24b", "#d96038",
    ];

    /// <summary>The hex color for a token, from its side's palette wrapped by <paramref name="colorIndex"/>.</summary>
    public static string Color(MapSide side, int colorIndex)
    {
        var palette = side == MapSide.Player ? PlayerColors : OpponentColors;
        var i = ((colorIndex % palette.Count) + palette.Count) % palette.Count;
        return palette[i];
    }

    /// <summary>
    /// A short (default 2-letter) uppercase abbreviation for a unit name: the initials of the first two
    /// significant words (skipping a leading faction/adjective like "Necron"), or the first two letters of a
    /// single-word name. Empty input yields an empty string.
    /// </summary>
    public static string Abbreviate(string? name, int maxLength = 3)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var words = name
            .Split([' ', '-', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Any(char.IsLetterOrDigit))
            .ToList();

        if (words.Count == 0)
            return string.Empty;

        // Drop a common leading faction/adjective so "Necron Warriors" abbreviates from "Warriors".
        if (words.Count > 1 && LeadingNoise.Contains(words[0]))
            words.RemoveAt(0);

        string abbrev;
        if (words.Count == 1)
        {
            var letters = new string(words[0].Where(char.IsLetterOrDigit).ToArray());
            abbrev = letters.Length <= 2 ? letters : letters[..2];
        }
        else
        {
            abbrev = new string(words.Take(maxLength).Select(w => w[0]).ToArray());
        }

        return abbrev.ToUpperInvariant();
    }

    private static readonly HashSet<string> LeadingNoise = new(StringComparer.OrdinalIgnoreCase)
    {
        "Necron", "Necrons",
    };
}
