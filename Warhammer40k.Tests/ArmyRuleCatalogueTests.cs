using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins the faction-wide Army Rules reference data in <see cref="ArmyRuleCatalogue"/>: faction filtering,
/// the phase mapping that drives the Play-Mode "now" emphasis, example/lore availability flags, and that the
/// authored content (Reanimation Protocols) is well-formed.
/// </summary>
public class ArmyRuleCatalogueTests
{
    private static ArmyRule Reanimation =>
        ArmyRuleCatalogue.All.Single(r => r.Name == "Reanimation Protocols");

    [Fact]
    public void Necrons_have_reanimation_protocols()
    {
        var rules = ArmyRuleCatalogue.ForFaction(Roster.NecronsFaction);
        Assert.Contains(rules, r => r.Name == "Reanimation Protocols");
    }

    [Theory]
    [InlineData("Necrons")]
    [InlineData("necrons")]
    [InlineData("NECRONS")]
    public void For_faction_is_case_insensitive(string faction)
    {
        Assert.NotEmpty(ArmyRuleCatalogue.ForFaction(faction));
    }

    [Fact]
    public void For_faction_filters_out_other_factions()
    {
        Assert.Empty(ArmyRuleCatalogue.ForFaction("Orks"));
    }

    [Fact]
    public void Reanimation_keys_off_the_command_phase_only()
    {
        var rule = Reanimation;
        Assert.False(rule.AppliesInAnyPhase);
        Assert.True(rule.AppliesInPhase(BattlePhase.Command));
        Assert.False(rule.AppliesInPhase(BattlePhase.Shooting));
        Assert.False(rule.AppliesInPhase(BattlePhase.Fight));
    }

    [Fact]
    public void Reanimation_has_an_example_but_no_lore_yet()
    {
        var rule = Reanimation;
        Assert.True(rule.HasExample);
        Assert.False(rule.HasLore);
    }

    [Fact]
    public void Reanimation_text_and_example_are_authored()
    {
        var rule = Reanimation;
        Assert.Contains("Reanimation Protocols", rule.Text);
        Assert.Contains("D3 wounds", rule.Text);
        Assert.Contains("Lokhust Destroyers", rule.Example);
    }

    [Fact]
    public void Every_rule_is_well_formed()
    {
        Assert.NotEmpty(ArmyRuleCatalogue.All);
        foreach (var rule in ArmyRuleCatalogue.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.Name));
            Assert.False(string.IsNullOrWhiteSpace(rule.Faction));
            Assert.False(string.IsNullOrWhiteSpace(rule.Text), $"{rule.Name} should have body text");
        }
    }

    [Fact]
    public void Rule_names_are_unique_within_a_faction()
    {
        var names = ArmyRuleCatalogue.ForFaction(Roster.NecronsFaction).Select(r => r.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }
}
