using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins <see cref="SelfAbilityParser"/>: a model's own ability that states a fixed characteristic or grants
/// its own weapons a keyword becomes a structured effect (absolute stat-set / weapon grant), while conditional
/// or leader abilities are left as prose.
/// </summary>
public class SelfAbilityParserTests
{
    private static Ability Ab(string name, string text) => new() { Name = name, Text = text };

    [Fact]
    public void Parses_absolute_stat_set_for_save_and_move()
    {
        var effect = SelfAbilityParser.ParseAbility(
            Ab("Shieldvanes", "The bearer has a Save characteristic of 3+ and a Move characteristic of 8\"."));

        Assert.NotNull(effect);
        var save = effect!.StatModifiers.Single(m => m.Target == StatTarget.Save);
        Assert.Equal("3+", save.SetValue);
        var move = effect.StatModifiers.Single(m => m.Target == StatTarget.Move);
        Assert.Equal("8\"", move.SetValue);
        Assert.Empty(effect.WeaponAbilities);
    }

    [Fact]
    public void Parses_self_ranged_weapon_grant()
    {
        var effect = SelfAbilityParser.ParseAbility(
            Ab("Nebuloscope", "Ranged weapons equipped by the bearer have the [IGNORES COVER] ability."));

        Assert.NotNull(effect);
        Assert.Equal(WeaponClass.Ranged, effect!.WeaponClass);
        Assert.Contains("Ignores Cover", effect.WeaponAbilities);
        Assert.Empty(effect.StatModifiers);
    }

    [Fact]
    public void Ignores_leader_conferrals()
    {
        var effect = SelfAbilityParser.ParseAbility(
            Ab("Lord", "While this model is leading a unit, melee weapons equipped by models in that unit have the [LETHAL HITS] ability."));
        Assert.Null(effect);
    }

    [Theory]
    [InlineData("Plasmacyte", "Once per battle, when this unit is selected to fight, melee weapons equipped by models in this unit have the [LANCE] ability until the end of the phase.")]
    [InlineData("Evasion Engrams", "In your Shooting phase, after this unit has shot, it can make a Normal Move of up to 6\".")]
    public void Ignores_conditional_or_temporary_abilities(string name, string text) =>
        Assert.Null(SelfAbilityParser.ParseAbility(Ab(name, text)));

    [Fact]
    public void Plain_unit_ability_yields_no_self_effect()
    {
        // "The bearer has the Stealth ability" is a unit ability, not a stat-set or weapon grant.
        Assert.Null(SelfAbilityParser.ParseAbility(Ab("Shadowloom", "The bearer has the Stealth ability.")));
    }
}
