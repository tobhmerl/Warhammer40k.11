using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;

namespace Warhammer40k.Tests;

/// <summary>
/// Auras are read from printed rules text: who is in scope ("friendly NECRONS unit (excluding MONSTER and
/// TITANIC units)"), what narrows them further ("if that model has the DESTROYER CULT keyword"), and whether
/// that narrowing is only one of two ways in ("… or that enemy unit is the closest eligible target").
/// </summary>
public class AuraParserTests
{
    private static Ability InfectiousMurderMadness() => new()
    {
        Name = "Infectious Murder\u2011madness (Aura)",
        Text = "While a friendly ^^**Necrons^^** unit (excluding ^^**Monster**^^ and ^^**Titanic^^** units) is "
             + "within 6\" of this model, each time a model in that unit makes an attack, if that model has the "
             + "^^**Destroyer Cult^^** keyword or that enemy unit is the closest eligible target, that attack has "
             + "the [SUSTAINED HITS 1] ability.",
    };

    private static Ability KeywordOnlyAura() => new()
    {
        Name = "Cryptek Conduit (Aura)",
        Text = "While a friendly Necrons unit is within 6\" of this model, each time a model in that unit makes "
             + "an attack, if that model has the Canoptek keyword, that attack has the [LETHAL HITS] ability.",
    };

    [Fact]
    public void Reads_scope_range_and_granted_keyword()
    {
        var aura = AuraParser.Parse(InfectiousMurderMadness())!;

        Assert.Equal(6, aura.RangeInches);
        Assert.Equal(["Necrons"], aura.RequiredKeywords);
        Assert.Equal(["Monster", "Titanic"], aura.ExcludedKeywords);
        Assert.Equal(["Destroyer Cult"], aura.NarrowingKeywords);
        Assert.True(aura.HasOpenCondition);
        Assert.Equal(["SUSTAINED HITS 1"], aura.GrantedKeywords);
    }

    [Fact]
    public void Open_condition_offers_the_aura_to_every_unit_in_scope()
    {
        var aura = AuraParser.Parse(InfectiousMurderMadness())!;

        // The bearer itself: Destroyer Cult, so squarely in scope.
        Assert.True(aura.AppliesTo(["Faction: Necrons", "Infantry", "Destroyer Cult"]));
        // A plain Necrons unit still qualifies through the "closest eligible target" alternative.
        Assert.True(aura.AppliesTo(["Faction: Necrons", "Infantry"]));
        // Excluded by keyword, and a different faction, respectively.
        Assert.False(aura.AppliesTo(["Faction: Necrons", "Monster"]));
        Assert.False(aura.AppliesTo(["Faction: Aeldari", "Infantry"]));
    }

    [Fact]
    public void Without_an_alternative_only_the_narrowed_units_are_offered_the_aura()
    {
        var aura = AuraParser.Parse(KeywordOnlyAura())!;

        Assert.False(aura.HasOpenCondition);
        Assert.True(aura.AppliesTo(["Faction: Necrons", "Canoptek"]));
        Assert.False(aura.AppliesTo(["Faction: Necrons", "Infantry"]));
    }

    [Fact]
    public void An_ability_that_is_not_an_aura_parses_to_null()
    {
        var ability = new Ability
        {
            Name = "Prophet of Destruction",
            Text = "Each time this model destroys an enemy unit, select one other friendly Destroyer Cult unit "
                 + "within 9\" of it.",
        };

        Assert.False(AuraParser.IsAura(ability));
        Assert.Null(AuraParser.Parse(ability));
    }

    [Fact]
    public void An_aura_that_grants_no_weapon_keyword_parses_to_null()
    {
        var ability = new Ability
        {
            Name = "Nullstone Field Generator (Aura)",
            Text = "While a friendly Necrons unit is within 6\" of the bearer, models in that unit have the "
                 + "Feel No Pain 5+ ability against mortal wounds and Psychic Attacks.",
        };

        Assert.True(AuraParser.IsAura(ability));
        Assert.Null(AuraParser.Parse(ability));
    }
}
