using Warhammer40k.Core.Play;

namespace Warhammer40k.Tests;

/// <summary>
/// Compact Rules mode replaces a card's rule NAME with what the rule actually does. The summaries are derived
/// from the printed text with no service call, so they are asserted here against the real wordings the
/// catalogue uses — and, just as importantly, so is the refusal to summarise anything ambiguous.
/// </summary>
public class CompactRuleTests
{
    [Fact]
    public void An_aura_keeps_the_granted_keyword_and_the_decisive_condition()
    {
        // The 6" range and the aura context are already on the card, so only the choice matters here.
        var summary = CompactRule.Summarize(
            "While a friendly ^^**Necrons^^** unit (excluding ^^**Monster**^^ and ^^**Titanic^^** units) is "
            + "within 6\" of this model, each time a model in that unit makes an attack, if that model has the "
            + "^^**Destroyer Cult^^** keyword or that enemy unit is the closest eligible target, that attack "
            + "has the [SUSTAINED HITS 1] ability.");

        Assert.Equal("SH1: Destroyer Cult or closest target", summary);
    }

    [Theory]
    // Core stratagems.
    [InlineData("Select one CHARACTER model in your unit. Until the end of the phase, that model's melee "
              + "weapons have the [PRECISION] ability.", "Precision")]
    [InlineData("Until the end of the phase, your unit has the Fights First ability and it must be the next "
              + "unit you select to fight (12.04).", "Fights First")]
    [InlineData("That battle-shock roll is automatically successful.", "Auto-pass")]
    // Detachment stratagems.
    [InlineData("Your unit has +1 OC until the end of the turn.", "+1 OC")]
    [InlineData("Until the end of the phase, add 1 to the Strength characteristic of melee weapons equipped "
              + "by models in your unit.", "+1 Str")]
    [InlineData("Your unit can make a Normal move of up to 6\".", "Normal move 6\"")]
    [InlineData("Until the end of the phase, each time a model in your unit makes an attack that targets a "
              + "unit within half range, re-roll a Hit roll of 1.", "Re-roll Hit of 1: within half range")]
    // Abilities and detachment rules.
    [InlineData("Each time a model in the bearer's unit makes an attack, add 1 to the Hit roll.", "+1 Hit")]
    [InlineData("Models in that unit have the Feel No Pain 5+ ability.", "5+ FNP")]
    [InlineData("While this model is leading a unit, models in that unit have a 4+ invulnerable save.", "4+ Inv")]
    [InlineData("Each time an attack is allocated to this model, subtract 1 from the Damage characteristic "
              + "of that attack.", "-1 Dmg")]
    [InlineData("Your unit activates its Reanimation Protocols and reanimates D3 wounds.", "Reanimate D3 wounds")]
    public void Summarises_the_effect_in_tabletop_shorthand(string text, string expected) =>
        Assert.Equal(expected, CompactRule.Summarize(text));

    [Fact]
    public void Keeps_the_objective_condition_that_drives_the_decision()
    {
        var summary = CompactRule.Summarize(
            "Each time a NECRONS model (excluding MONSTER models) from your army makes an attack that targets "
            + "a unit within range of one or more objective markers, add 1 to the Hit roll.");

        Assert.Equal("+1 Hit: on an objective", summary);
    }

    [Fact]
    public void A_stratagem_falls_back_to_its_restrictions_when_the_effect_says_nothing_quantifiable()
    {
        var summary = CompactRule.SummarizeStratagem(
            "Resolve the following sequence with your unit.",
            "Each time a model in that unit makes an attack, add 1 to the Wound roll.");

        Assert.Equal("+1 Wound", summary);
    }

    [Fact]
    public void Rules_that_cannot_be_shortened_safely_return_null()
    {
        // Nothing quantifiable to state — the card must keep showing the rule's name instead of a guess.
        Assert.Null(CompactRule.Summarize(
            "After the attacking unit has resolved its attacks, your unit can shoot as if it were your "
            + "Shooting phase, but it must target only that enemy unit."));
        Assert.Null(CompactRule.Summarize(""));
        Assert.Null(CompactRule.Summarize(null));
    }

    [Fact]
    public void A_summary_that_would_not_fit_the_card_is_dropped_rather_than_truncated()
    {
        // Truncating mid-sentence would change the meaning, so an over-long result yields null.
        var summary = CompactRule.Summarize(
            "Add 1 to the Strength characteristic and add 2 to the Attacks characteristic of melee weapons "
            + "equipped by models in your unit, if that model has the Destroyer Cult Praetorian Vanguard "
            + "Reanimation keyword or that enemy unit is the closest eligible target.");

        Assert.Null(summary);
    }
}
