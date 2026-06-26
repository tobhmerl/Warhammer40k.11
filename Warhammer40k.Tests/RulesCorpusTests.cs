using Warhammer40k.Api.Rules;
using Warhammer40k.Core.RulesAssistant;

namespace Warhammer40k.Tests;

/// <summary>
/// Exercises the <b>real</b> embedded Core-Rules corpus through <see cref="RulesProvider"/> + the search
/// engine, proving the wrapper-object JSON ({ rules: [...] }) and numeric <c>section</c> load correctly and
/// that representative queries resolve to the right rules. (Removable feature — see
/// docs/rules-assistant-REMOVE.md.) Branch coverage of the engine lives in <see cref="RulesSearchTests"/>.
/// </summary>
public class RulesCorpusTests
{
    private static readonly IReadOnlyList<RuleCard> Corpus = RulesProvider.LoadEmbedded();

    [Fact]
    public void Embedded_corpus_loads_the_full_rule_set()
    {
        Assert.NotEmpty(Corpus);
        Assert.True(Corpus.Count >= 140, $"Expected the full corpus, got {Corpus.Count}.");
        Assert.All(Corpus, c => Assert.False(string.IsNullOrWhiteSpace(c.Id)));
        Assert.All(Corpus, c => Assert.False(string.IsNullOrWhiteSpace(c.Text)));
    }

    [Fact]
    public void Numeric_section_is_parsed_as_a_string()
    {
        // The corpus writes "section" as a JSON number; the StringOrNumberConverter must keep Section usable.
        Assert.All(Corpus, c => Assert.False(string.IsNullOrWhiteSpace(c.Section)));
    }

    [Fact]
    public void Bracket_blast_query_finds_the_blast_rule()
    {
        var answer = RulesSearchEngine.Search(Corpus, "does [BLAST] work against a unit in engagement range");
        Assert.False(answer.CorpusEmpty);
        Assert.Contains(answer.Matches, m => m.Card.Id == "24.05");
    }

    [Fact]
    public void Bare_id_query_returns_that_rule()
    {
        var answer = RulesSearchEngine.Search(Corpus, "11.02");
        Assert.Equal("11.02", answer.Matches[0].Card.Id);
    }

    [Fact]
    public void Answer_text_is_verbatim_from_the_corpus()
    {
        var answer = RulesSearchEngine.Search(Corpus, "[BLAST]");
        var top = answer.Matches[0].Card;
        var source = Corpus.Single(c => c.Id == top.Id);
        Assert.Equal(source.Text, top.Text); // no paraphrasing
    }

    [Fact]
    public void Every_match_produces_a_citation()
    {
        var answer = RulesSearchEngine.Search(Corpus, "charge");
        Assert.NotEmpty(answer.Matches);
        foreach (var match in answer.Matches)
            Assert.Contains(answer.Sources, s => s.Id == match.Card.Id);
    }
}
