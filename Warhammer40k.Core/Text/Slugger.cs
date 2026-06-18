using System.Text;

namespace Warhammer40k.Core.Text;

/// <summary>
/// Turns a display name into a stable lowercase slug used as a catalogue/roster key
/// ("C'tan Shard of the Deceiver" → "ctan-shard-of-the-deceiver"). Apostrophes are dropped so
/// "C'tan" → "ctan"; every other run of non-alphanumeric characters collapses to a single dash.
/// </summary>
public static class Slugger
{
    /// <summary>Derives a stable lowercase slug from <paramref name="name"/>.</summary>
    public static string Slug(string name)
    {
        var sb = new StringBuilder(name.Length);
        var lastDash = false;
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastDash = false;
            }
            else if (ch is '\'' or '\u2019')
            {
                // drop apostrophes so "C'tan" -> "ctan"
                continue;
            }
            else if (!lastDash && sb.Length > 0)
            {
                sb.Append('-');
                lastDash = true;
            }
        }

        while (sb.Length > 0 && sb[^1] == '-')
            sb.Length--;

        return sb.ToString();
    }
}
