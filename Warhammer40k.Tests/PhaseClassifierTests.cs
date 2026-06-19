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
    public void Classifies_shooting_ability_from_text()
    {
        var phases = PhaseClassifier.Classify(Ab("Deadly", "In your Shooting phase this model can shoot twice."));
        Assert.Contains(BattlePhase.Shooting, phases);
    }

    [Fact]
    public void Classifies_command_phase_ability()
    {
        var phases = PhaseClassifier.Classify(Ab("Protocols", "At the start of your Command phase, choose a protocol."));
        Assert.Contains(BattlePhase.Command, phases);
    }

    [Fact]
    public void Classifies_fights_first_into_fight_phase()
    {
        var phases = PhaseClassifier.Classify(Ab("Vicious", "This unit has the Fights First ability."));
        Assert.Contains(BattlePhase.Fight, phases);
    }

    [Fact]
    public void Word_boundary_guard_does_not_match_move_inside_remove()
    {
        // "remove" must not be read as the Movement cue "move".
        var phases = PhaseClassifier.Classify(Ab("Reanimation", "Return models that were removed from play."));
        Assert.DoesNotContain(BattlePhase.Movement, phases);
    }

    [Fact]
    public void Word_boundary_guard_does_not_match_charge_inside_discharge()
    {
        var phases = PhaseClassifier.Classify(Ab("Capacitor", "Discharge stored energy into the enemy."));
        Assert.DoesNotContain(BattlePhase.Charge, phases);
    }

    [Fact]
    public void Passive_ability_classifies_to_no_phase()
    {
        var ability = Ab("Resilient", "Each time an attack is allocated to this model, subtract 1 from the Damage.");
        Assert.Empty(PhaseClassifier.Classify(ability));
        Assert.True(PhaseClassifier.IsPassive(ability));
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
}
