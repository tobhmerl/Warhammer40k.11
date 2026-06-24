using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins the pure phase heuristics in <see cref="PhaseClassifier"/>: weapon → phase mapping, ability text
/// cue matching (including the word-boundary guard so "remove" is not read as "move"), passive detection,
/// and invulnerable-save parsing.
/// </summary>
public class PhaseClassifierTests
{
    private static Ability Ab(string name, string text) => new() { Name = name, Text = text };

    [Theory]
    [InlineData("This model has a 4+ invulnerable save.", "4+", SaveScope.Model)]
    [InlineData("Models in this unit have a 5+ invulnerable save.", "5+", SaveScope.Unit)]
    [InlineData("While this model is leading a unit, models in that unit have a 4+ invulnerable save.", "4+", SaveScope.Unit)]
    public void Scopes_invulnerable_save(string text, string value, SaveScope scope)
    {
        var parsed = PhaseClassifier.InvulnerableSaveScoped(new Ability { Name = "Inv", Text = text });
        Assert.NotNull(parsed);
        Assert.Equal(value, parsed!.Value.Value);
        Assert.Equal(scope, parsed.Value.Scope);
    }

    [Theory]
    [InlineData("Models in this unit have the Feel No Pain 5+ ability.", "5+", SaveScope.Unit)]
    [InlineData("This model has the Feel No Pain 6+ ability.", "6+", SaveScope.Model)]
    public void Scopes_feel_no_pain(string text, string value, SaveScope scope)
    {
        var parsed = PhaseClassifier.FeelNoPainScoped(new Ability { Name = "FNP", Text = text });
        Assert.NotNull(parsed);
        Assert.Equal(value, parsed!.Value.Value);
        Assert.Equal(scope, parsed.Value.Scope);
    }

    [Theory]
    // A model's / unit's own always-on save rule → own rule (chip, not a schedulable ability).
    [InlineData("This model has a 4+ invulnerable save.", true)]
    [InlineData("Models in this unit have a 5+ invulnerable save.", true)]
    [InlineData("Models in this unit have the Feel No Pain 5+ ability.", true)]
    // A conditional ability that confers a save → not an own rule (real ability, applied manually).
    [InlineData("While this model is leading a unit, models in that unit have a 4+ invulnerable save.", false)]
    public void IsOwnSaveRule_distinguishes_profile_saves_from_conferring_abilities(string text, bool expected) =>
        Assert.Equal(expected, PhaseClassifier.IsOwnSaveRule(new Ability { Name = "X", Text = text }));

    [Fact]
    public void IsOwnSaveRule_is_false_for_a_non_save_ability() =>
        Assert.False(PhaseClassifier.IsOwnSaveRule(new Ability { Name = "Grand Strategist", Text = "Gain 1CP." }));

    [Fact]
    public void Melee_weapon_maps_to_fight_phase()
    {
        var weapon = new WeaponProfile { Name = "Warscythe", Type = "Melee" };
        Assert.Equal(BattlePhase.Fight, PhaseClassifier.PhaseForWeapon(weapon));
    }

    [Theory]
    [InlineData("Ranged")]
    [InlineData("ranged")]
    [InlineData("")]
    public void Non_melee_weapon_maps_to_shooting_phase(string type)
    {
        var weapon = new WeaponProfile { Name = "Gauss flayer", Type = type };
        Assert.Equal(BattlePhase.Shooting, PhaseClassifier.PhaseForWeapon(weapon));
    }

    [Fact]
    public void Parses_invulnerable_save_value()
    {
        var abilities = new[]
        {
            Ab("Other", "Some unrelated rule."),
            Ab("Invulnerable Save", "This model has a 4+ invulnerable save."),
        };
        Assert.Equal("4+", PhaseClassifier.InvulnerableSave(abilities));
    }

    [Fact]
    public void Returns_null_when_no_invulnerable_save()
    {
        var abilities = new[] { Ab("Tough", "This model is hard to kill.") };
        Assert.Null(PhaseClassifier.InvulnerableSave(abilities));
    }

    [Theory]
    [InlineData("This model has Feel No Pain 5+.", "5+")]
    [InlineData("Models in this unit have a Feel No Pain (4+) ability.", "4+")]
    public void Parses_feel_no_pain_value(string text, string expected)
    {
        var abilities = new[] { Ab("Reanimation", text) };
        Assert.Equal(expected, PhaseClassifier.FeelNoPain(abilities));
    }

    [Fact]
    public void Returns_null_when_no_feel_no_pain()
    {
        var abilities = new[] { Ab("Invulnerable Save", "This model has a 4+ invulnerable save.") };
        Assert.Null(PhaseClassifier.FeelNoPain(abilities));
    }
}
