using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins <see cref="LeaderConferralParser"/> against the real seed phrasings: leader-conferred weapon abilities
/// (Skorpekh Lord → LETHAL HITS), unit abilities (Technomancer → Feel No Pain 5+), numeric buffs, and the
/// no-match cases that must stay as prose.
/// </summary>
public class LeaderConferralParserTests
{
    private static ConferredEffect? Parse(string name, string text) =>
        LeaderConferralParser.ParseAbility(new Ability { Name = name, Text = text });

    [Fact]
    public void Parses_leader_conferred_melee_weapon_ability()
    {
        var effect = Parse("United In Destruction",
            "While this model is leading a unit, melee weapons equipped by models in that unit have the [LETHAL HITS] ability.");

        Assert.NotNull(effect);
        Assert.Equal("United In Destruction", effect!.SourceAbility);
        Assert.Equal(WeaponClass.Melee, effect.WeaponClass);
        Assert.Equal(new[] { "Lethal Hits" }, effect.WeaponAbilities);   // ALL-CAPS normalised for display
        Assert.Empty(effect.UnitAbilities);
        Assert.Empty(effect.StatModifiers);
        Assert.Equal("Lethal Hits on melee weapons", effect.Summary);
    }

    [Fact]
    public void Parses_leader_conferred_ranged_weapon_ability()
    {
        var effect = Parse("Augury",
            "While this model is leading a unit, ranged weapons equipped by models in that unit have the [SUSTAINED HITS 1] ability.");

        Assert.NotNull(effect);
        Assert.Equal(WeaponClass.Ranged, effect!.WeaponClass);
        Assert.Equal(new[] { "Sustained Hits 1" }, effect.WeaponAbilities);
    }

    [Fact]
    public void Parses_leader_conferred_unit_ability()
    {
        var effect = Parse("Rites of Reanimation",
            "While this model is leading a unit, models in that unit have the Feel No Pain 5+ ability.");

        Assert.NotNull(effect);
        Assert.Equal(new[] { "Feel No Pain 5+" }, effect!.UnitAbilities);
        Assert.Empty(effect.WeaponAbilities);
        Assert.Equal("Feel No Pain 5+", effect.Summary);
    }

    [Fact]
    public void Parses_leader_conferred_hit_roll_modifier()
    {
        var effect = Parse("Awakened Command",
            "While this model is leading a unit, each time a model in that unit makes an attack, add 1 to the Hit roll.");

        Assert.NotNull(effect);
        var mod = Assert.Single(effect!.StatModifiers);
        Assert.Equal(StatTarget.Skill, mod.Target);
        Assert.Equal(1, mod.Delta);
        Assert.Equal("+1 to Hit", mod.Describe());
    }

    [Fact]
    public void Parses_leader_conferred_characteristic_modifier()
    {
        var effect = Parse("Empyric Lodestone",
            "While this model is leading a unit, add 1 to the Toughness characteristic of models in that unit.");

        Assert.NotNull(effect);
        var mod = Assert.Single(effect!.StatModifiers);
        Assert.Equal(StatTarget.Toughness, mod.Target);
        Assert.Equal(1, mod.Delta);
    }

    [Fact]
    public void Ignores_a_plain_leader_attach_ability()
    {
        var effect = Parse("Leader", "This model can be attached to the following units:\n■ SKORPEKH DESTROYERS");
        Assert.Null(effect);
    }

    [Fact]
    public void Ignores_a_command_point_ability_so_it_stays_as_text()
    {
        var effect = Parse("Grand Strategist",
            "At the start of your Command phase, if this model is on the battlefield, you gain 1CP.");
        Assert.Null(effect);
    }

    [Fact]
    public void Ignores_a_self_only_weapon_ability_without_leading_clause()
    {
        var effect = Parse("Relentless",
            "Ranged weapons equipped by this model have the [ASSAULT] ability.");
        Assert.Null(effect);
    }

    [Fact]
    public void Parse_collection_returns_only_conferring_abilities()
    {
        var abilities = new[]
        {
            new Ability { Name = "Leader", Text = "This model can be attached to the following units:\n■ SKORPEKH DESTROYERS" },
            new Ability { Name = "United In Destruction", Text = "While this model is leading a unit, melee weapons equipped by models in that unit have the [LETHAL HITS] ability." },
            new Ability { Name = "Invulnerable Save", Text = "This model has a 4+ invulnerable save." },
        };

        var effects = LeaderConferralParser.Parse(abilities);

        var effect = Assert.Single(effects);
        Assert.Equal("United In Destruction", effect.SourceAbility);
    }
}
