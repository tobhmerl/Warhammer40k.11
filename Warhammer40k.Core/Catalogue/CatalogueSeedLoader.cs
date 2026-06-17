using System.Text;
using System.Text.Json;

namespace Warhammer40k.Core.Catalogue;

/// <summary>
/// Parses a <c>*-catalogue-seed.json</c> document into a <see cref="CatalogueData"/> and derives the
/// fields the rules engine relies on (copy caps, Warlord eligibility, Leader targets, …) <b>once</b>, so
/// no ability text is re-parsed at runtime. <see cref="Enrich"/> is idempotent.
/// </summary>
public static class CatalogueSeedLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Deserializes and enriches a catalogue from its JSON text.</summary>
    public static CatalogueData Load(string json)
    {
        var data = JsonSerializer.Deserialize<CatalogueData>(json, JsonOptions)
            ?? throw new InvalidDataException("Catalogue seed JSON could not be parsed.");
        Enrich(data);
        return data;
    }

    /// <summary>Deserializes and enriches a catalogue from a JSON stream (e.g. an embedded resource).</summary>
    public static CatalogueData Load(Stream json)
    {
        var data = JsonSerializer.Deserialize<CatalogueData>(json, JsonOptions)
            ?? throw new InvalidDataException("Catalogue seed JSON could not be parsed.");
        Enrich(data);
        return data;
    }

    /// <summary>Computes every derived <see cref="Datasheet"/> field. Idempotent — safe to call repeatedly.</summary>
    public static void Enrich(CatalogueData data)
    {
        // First pass: id + per-datasheet flags.
        foreach (var d in data.Datasheets)
        {
            d.Id = Slug(d.Name);
            d.IsMonster = d.Keywords.Any(k => k.Equals("Monster", StringComparison.OrdinalIgnoreCase));
            d.IsUnique = d.IsEpicHero;
            d.MaxCopies = d.IsEpicHero ? 1 : (d.IsBattleline || d.IsDedicatedTransport ? 6 : 3);
            d.WarlordEligible = d.IsCharacter && !HasAbilityText(d, "cannot be your warlord");
            d.CanTakeEnhancements = d.IsCharacter && !d.IsEpicHero && !HasAbilityText(d, "cannot be given enhancements");

            var leaderText = GetLeaderText(d);
            d.HasLeaderAbility = leaderText is not null
                || d.FactionRules.Any(r => r.Equals("Leader", StringComparison.OrdinalIgnoreCase));
            d.AllowsCoLeader = leaderText is not null
                && leaderText.Contains("already been attached", StringComparison.OrdinalIgnoreCase);
            d.LeaderTargetIds.Clear();
        }

        // Second pass: resolve Leader targets by matching known unit names inside the leader text.
        foreach (var d in data.Datasheets)
        {
            var leaderText = GetLeaderText(d);
            if (leaderText is null)
                continue;

            var cleaned = CleanText(leaderText);
            foreach (var other in data.Datasheets)
            {
                if (ReferenceEquals(other, d) || other.Name.Length == 0)
                    continue;

                if (cleaned.Contains(other.Name, StringComparison.OrdinalIgnoreCase)
                    && !d.LeaderTargetIds.Contains(other.Id))
                {
                    d.LeaderTargetIds.Add(other.Id);
                }
            }
        }
    }

    /// <summary>Returns the Leader ability text for a datasheet, or <c>null</c> when it has no Leader ability.</summary>
    private static string? GetLeaderText(Datasheet d)
    {
        var ability = d.Abilities.FirstOrDefault(a =>
            a.Name.Equals("Leader", StringComparison.OrdinalIgnoreCase)
            || a.Text.Contains("can be attached to the following", StringComparison.OrdinalIgnoreCase));
        return ability?.Text;
    }

    private static bool HasAbilityText(Datasheet d, string needle) =>
        d.Abilities.Any(a => a.Text.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static string CleanText(string text) =>
        text.Replace("^^**", " ", StringComparison.Ordinal)
            .Replace("^^", " ", StringComparison.Ordinal)
            .Replace("**", " ", StringComparison.Ordinal)
            .Replace("■", " ", StringComparison.Ordinal);

    /// <summary>Turns a unit name into a stable lowercase slug ("C'tan Shard of the Deceiver" → "ctan-shard-of-the-deceiver").</summary>
    private static string Slug(string name)
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
