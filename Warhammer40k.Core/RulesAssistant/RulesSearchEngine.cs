using System.Text;

namespace Warhammer40k.Core.RulesAssistant;

/// <summary>
/// Pure, deterministic search over the Core-Rules corpus. Given a free-text question it ranks the rule cards
/// by keyword/field relevance and returns them <b>verbatim</b> with their referenced rules and a citation
/// list. There is no language model and no paraphrasing — the output text is exactly the corpus text, so the
/// assistant can never misstate a rule.
/// </summary>
public static class RulesSearchEngine
{
    // Field weights (higher = stronger signal). An exact weapon-ability tag like "[BLAST]" is the strongest.
    private const int TagExactWeight = 50;
    private const int KeywordExactWeight = 14;
    private const int KeywordPartialWeight = 6;
    private const int TitleWeight = 12;
    private const int CategoryWeight = 7;
    private const int SectionTitleWeight = 5;
    private const int TextWeight = 2;
    private const int IdExactWeight = 100; // querying a bare id ("11.02") jumps straight to it

    private static readonly char[] TokenSeparators =
        " \t\r\n.,;:!?/\\()\"'".ToCharArray();

    // Very common words that shouldn't drive relevance.
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "of", "to", "in", "on", "is", "are", "be", "can", "do", "does",
        "i", "my", "it", "its", "and", "or", "if", "for", "with", "how", "what", "when",
        "this", "that", "you", "your", "we",
    };

    /// <summary>
    /// Ranks <paramref name="cards"/> against <paramref name="query"/> and returns the top
    /// <paramref name="maxMatches"/> with their referenced rules and aggregated sources. Returns an empty
    /// (but non-null) answer when the corpus or query is empty.
    /// </summary>
    public static RulesAnswer Search(IReadOnlyList<RuleCard> cards, string query, int maxMatches = 6)
    {
        var answer = new RulesAnswer { Query = query?.Trim() ?? "" };

        if (cards is null || cards.Count == 0)
        {
            answer.CorpusEmpty = true;
            return answer;
        }
        if (string.IsNullOrWhiteSpace(query))
            return answer;

        var tokens = Tokenize(query);
        var tags = ExtractTags(query);
        if (tokens.Count == 0 && tags.Count == 0)
            return answer;

        var scored = new List<RuleMatch>();
        foreach (var card in cards)
        {
            var score = ScoreCard(card, query, tokens, tags);
            if (score > 0)
                scored.Add(new RuleMatch { Card = card, Score = score });
        }

        answer.Matches = scored
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.Card.Id, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxMatches))
            .ToList();

        // Expand references of the matched cards (skip ones already shown as matches).
        var shown = new HashSet<string>(answer.Matches.Select(m => m.Card.Id), StringComparer.OrdinalIgnoreCase);
        var byId = BuildIndex(cards);
        var related = new List<RuleCard>();
        foreach (var match in answer.Matches)
        {
            foreach (var refId in match.Card.References)
            {
                if (string.IsNullOrWhiteSpace(refId))
                    continue;
                if (!shown.Add(refId))
                    continue;
                if (byId.TryGetValue(refId, out var refCard))
                    related.Add(refCard);
            }
        }
        answer.Related = related;

        // Sources = matches first (in rank order), then related, de-duplicated by id.
        var seenSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in answer.Matches.Select(m => m.Card).Concat(answer.Related))
        {
            if (seenSource.Add(card.Id))
                answer.Sources.Add(new RuleSource { Id = card.Id, Title = card.Title, Page = card.Page });
        }

        return answer;
    }

    private static int ScoreCard(RuleCard card, string rawQuery, IReadOnlyList<string> tokens, IReadOnlyList<string> tags)
    {
        var score = 0;

        // A bare id query ("11.02") goes straight to that card.
        if (!string.IsNullOrEmpty(card.Id) && rawQuery.Trim().Equals(card.Id, StringComparison.OrdinalIgnoreCase))
            score += IdExactWeight;

        // Weapon-ability tags: exact match against the card's keywords/text (compared without brackets too).
        foreach (var tag in tags)
        {
            var bare = tag.Trim('[', ']');
            var hasTag = card.Keywords.Any(k => TagsEqual(k, tag, bare))
                || card.Title.Contains(tag, StringComparison.OrdinalIgnoreCase)
                || card.Text.Contains(tag, StringComparison.OrdinalIgnoreCase);
            if (hasTag)
                score += TagExactWeight;
        }

        var titleTokens = TokenSet(card.Title);
        var categoryTokens = TokenSet(card.Category);
        var sectionTokens = TokenSet(card.SectionTitle);
        var textTokens = TokenSet(card.Text);
        var keywordTokens = card.Keywords.Select(k => k.ToLowerInvariant()).ToList();

        foreach (var token in tokens)
        {
            if (titleTokens.Contains(token)) score += TitleWeight;
            if (categoryTokens.Contains(token)) score += CategoryWeight;
            if (sectionTokens.Contains(token)) score += SectionTitleWeight;
            if (textTokens.Contains(token)) score += TextWeight;

            // Keywords: exact token match is strong; substring (e.g. "charge" in "engagement range") is weaker.
            if (keywordTokens.Any(k => k.Equals(token, StringComparison.OrdinalIgnoreCase)))
                score += KeywordExactWeight;
            else if (keywordTokens.Any(k => k.Contains(token, StringComparison.OrdinalIgnoreCase)))
                score += KeywordPartialWeight;
        }

        return score;
    }

    private static bool TagsEqual(string keyword, string tag, string bare) =>
        keyword.Equals(tag, StringComparison.OrdinalIgnoreCase)
        || keyword.Trim('[', ']').Equals(bare, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, RuleCard> BuildIndex(IReadOnlyList<RuleCard> cards)
    {
        var byId = new Dictionary<string, RuleCard>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in cards)
            if (!string.IsNullOrWhiteSpace(card.Id))
                byId[card.Id] = card;
        return byId;
    }

    private static List<string> Tokenize(string text)
    {
        var result = new List<string>();
        foreach (var part in text.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = part.Trim('[', ']').ToLowerInvariant();
            if (token.Length < 2 || StopWords.Contains(token))
                continue;
            result.Add(token);
        }
        return result;
    }

    private static HashSet<string> TokenSet(string text) =>
        new(Tokenize(text), StringComparer.OrdinalIgnoreCase);

    // Pull bracketed weapon-ability tags out of the query, e.g. "does [BLAST] ignore cover" -> ["[BLAST]"].
    private static List<string> ExtractTags(string text)
    {
        var tags = new List<string>();
        var sb = new StringBuilder();
        var inside = false;
        foreach (var ch in text)
        {
            if (ch == '[')
            {
                inside = true;
                sb.Clear();
                sb.Append('[');
            }
            else if (ch == ']' && inside)
            {
                sb.Append(']');
                tags.Add(sb.ToString());
                inside = false;
            }
            else if (inside)
            {
                sb.Append(ch);
            }
        }
        return tags;
    }
}
