using Warhammer40k.Core.RulesAssistant;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins the deterministic rules search: relevance ranking, bracket weapon-ability tags, exact-id lookup,
/// reference expansion, and citation aggregation. Uses an inline fixture so it is independent of the embedded
/// <c>core-rules.json</c> corpus. (This whole feature is removable — see docs/rules-assistant-REMOVE.md.)
/// </summary>
public class RulesSearchTests
{
    private static List<RuleCard> Corpus() =>
    [
        new RuleCard
        {
            Id = "11.02", Section = "11", SectionTitle = "Charge Phase", Category = "Movement",
            Title = "Charge", Page = 36,
            Text = "In your Charge phase you can select eligible units to declare a charge. Make a Charge roll of 2D6.",
            References = ["03.04"],
            Keywords = ["charge", "charge roll", "engagement range"],
        },
        new RuleCard
        {
            Id = "03.04", Section = "03", SectionTitle = "Core Concepts", Category = "Core",
            Title = "Engagement Range", Page = 14,
            Text = "Engagement Range represents the area of the battlefield within 1\" horizontally and 5\" vertically of a model.",
            References = [],
            Keywords = ["engagement range", "1 inch"],
        },
        new RuleCard
        {
            Id = "24.10", Section = "24", SectionTitle = "Weapon Abilities", Category = "Weapon ability",
            Title = "Blast", Page = 30,
            Text = "[BLAST] weapons make a number of additional attacks against larger units and can never be used against a unit within Engagement Range.",
            References = ["03.04"],
            Keywords = ["[BLAST]", "blast", "attacks"],
        },
        new RuleCard
        {
            Id = "20.01", Section = "20", SectionTitle = "Terrain", Category = "Terrain",
            Title = "Benefit of Cover", Page = 44,
            Text = "While a model has the Benefit of Cover, add 1 to armour saving throws made for that model.",
            References = [],
            Keywords = ["cover", "benefit of cover", "saving throw"],
        },
    ];

    [Fact]
    public void Empty_corpus_flags_corpus_empty()
    {
        var answer = RulesSearchEngine.Search([], "charge");
        Assert.True(answer.CorpusEmpty);
        Assert.Empty(answer.Matches);
    }

    [Fact]
    public void Blank_query_returns_no_matches()
    {
        var answer = RulesSearchEngine.Search(Corpus(), "   ");
        Assert.False(answer.CorpusEmpty);
        Assert.Empty(answer.Matches);
    }

    [Fact]
    public void Ranks_the_most_relevant_card_first()
    {
        var answer = RulesSearchEngine.Search(Corpus(), "how does a charge roll work");
        Assert.NotEmpty(answer.Matches);
        Assert.Equal("11.02", answer.Matches[0].Card.Id);
    }

    [Fact]
    public void Bracket_weapon_ability_tag_jumps_to_its_card()
    {
        var answer = RulesSearchEngine.Search(Corpus(), "can [BLAST] hit a unit in engagement range");
        Assert.Equal("24.10", answer.Matches[0].Card.Id);
        Assert.True(answer.Matches[0].Score >= 50); // tag-exact weight dominates
    }

    [Fact]
    public void Bare_id_query_returns_that_exact_rule_first()
    {
        var answer = RulesSearchEngine.Search(Corpus(), "11.02");
        Assert.Equal("11.02", answer.Matches[0].Card.Id);
    }

    [Fact]
    public void Match_text_is_verbatim_from_the_corpus()
    {
        var corpus = Corpus();
        var answer = RulesSearchEngine.Search(corpus, "engagement range");
        var top = answer.Matches[0].Card;
        var source = corpus.Single(c => c.Id == top.Id);
        Assert.Equal(source.Text, top.Text); // no paraphrasing — exact corpus text
    }

    [Fact]
    public void Expands_referenced_rules_as_related()
    {
        var answer = RulesSearchEngine.Search(Corpus(), "charge");
        // Charge (11.02) references Engagement Range (03.04) → surfaced as related, not duplicated as a match.
        Assert.Contains(answer.Related, c => c.Id == "03.04");
        Assert.DoesNotContain(answer.Matches, m => m.Card.Id == "03.04");
    }

    [Fact]
    public void Sources_aggregate_matches_then_related_without_duplicates()
    {
        var answer = RulesSearchEngine.Search(Corpus(), "charge");
        var ids = answer.Sources.Select(s => s.Id).ToList();
        Assert.Equal(ids.Distinct().ToList(), ids);        // no dupes
        Assert.Contains("11.02", ids);                      // the match
        Assert.Contains("03.04", ids);                      // its reference
        var source = answer.Sources.Single(s => s.Id == "11.02");
        Assert.Equal("Charge", source.Title);
        Assert.Equal(36, source.Page);
    }

    [Fact]
    public void Respects_the_max_matches_cap()
    {
        var answer = RulesSearchEngine.Search(Corpus(), "charge engagement cover blast", maxMatches: 2);
        Assert.True(answer.Matches.Count <= 2);
    }
}
