using System.Text.Json.Serialization;

namespace Warhammer40k.Core.RulesAssistant;

/// <summary>
/// The deterministic result of a rules query: the best-matching cards (verbatim), any rules they reference,
/// and an aggregated citation list. No text here is generated — every string comes straight from the corpus.
/// </summary>
public sealed class RulesAnswer
{
    [JsonPropertyName("query")] public string Query { get; set; } = "";

    /// <summary>Cards that matched the query, best first.</summary>
    [JsonPropertyName("matches")] public List<RuleMatch> Matches { get; set; } = [];

    /// <summary>Cards referenced by the matches (de-duplicated, not already in <see cref="Matches"/>).</summary>
    [JsonPropertyName("related")] public List<RuleCard> Related { get; set; } = [];

    /// <summary>Citation list aggregated from matches + related, for the "Sources" line.</summary>
    [JsonPropertyName("sources")] public List<RuleSource> Sources { get; set; } = [];

    /// <summary>True when the corpus is empty (rules JSON not yet provided).</summary>
    [JsonPropertyName("corpusEmpty")] public bool CorpusEmpty { get; set; }
}

/// <summary>A matched <see cref="RuleCard"/> plus its relevance <see cref="Score"/> (higher = better).</summary>
public sealed class RuleMatch
{
    [JsonPropertyName("card")] public RuleCard Card { get; set; } = new();

    [JsonPropertyName("score")] public int Score { get; set; }
}

/// <summary>A compact citation: rule id, title and page, e.g. <c>Charge 11.02 (p36)</c>.</summary>
public sealed class RuleSource
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    [JsonPropertyName("title")] public string Title { get; set; } = "";

    [JsonPropertyName("page")] public int Page { get; set; }
}
