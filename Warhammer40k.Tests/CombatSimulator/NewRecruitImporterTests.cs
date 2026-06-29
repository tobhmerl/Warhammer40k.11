using Warhammer40k._11.Features.CombatSimulator.Domain;
using Warhammer40k._11.Features.CombatSimulator.Import;

namespace Warhammer40k.Tests.CombatSimulator;

/// <summary>
/// Pins the New Recruit importer (§6b/§12) against a real-shaped 11th-edition export fixture. Part of the
/// removable Combat Simulator feature.
/// </summary>
public class NewRecruitImporterTests
{
    private static ImportResult Imported()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "CombatSimulator", "Fixtures", "dark-angels-sample.json");
        return NewRecruitImporter.Import(File.ReadAllText(path));
    }

    private static CombatUnit Unit(string name) => Imported().Units.Single(u => u.Name == name);

    [Fact]
    public void Configuration_selections_produce_no_units()
    {
        var units = Imported().Units;
        Assert.DoesNotContain(units, u => u.Name == "Battle Size");
        Assert.DoesNotContain(units, u => u.Name == "Detachment");
        // The three real units are present.
        Assert.Contains(units, u => u.Name == "Azrael");
        Assert.Contains(units, u => u.Name == "Heavy Intercessor Squad");
        Assert.Contains(units, u => u.Name == "Inner Circle Companions");
    }

    [Fact]
    public void Faction_comes_from_catalogue_name()
    {
        Assert.Equal("Imperium - Adeptus Astartes - Dark Angels", Unit("Azrael").Faction);
    }

    [Fact]
    public void Azrael_lions_wrath_is_parsed_with_abilities()
    {
        var lw = Unit("Azrael").AllWeapons.Single(w => w.Name == "Lion's Wrath");
        Assert.False(lw.IsMelee);
        Assert.Equal("24", lw.Range);
        Assert.Equal(2, lw.Attacks.ExpectedValue(), 3);
        Assert.Equal(2, lw.Skill);                 // BS 2+
        Assert.Equal(8, lw.Strength.ExpectedValue(), 3);
        Assert.Equal(-3, lw.ArmourPenetration);
        Assert.Equal(2, lw.Damage.ExpectedValue(), 3);
        Assert.Contains(lw.Abilities, a => a is Anti { Keyword: "Infantry", CritThreshold: 4 });
        Assert.Contains(lw.Abilities, a => a is DevastatingWounds);
        Assert.Contains(lw.Abilities, a => a is RapidFire { X: 1 });
    }

    [Fact]
    public void Azrael_sword_of_secrets_is_parsed()
    {
        var sos = Unit("Azrael").AllWeapons.Single(w => w.Name == "The Sword of Secrets");
        Assert.True(sos.IsMelee);
        Assert.Equal(6, sos.Attacks.ExpectedValue(), 3);
        Assert.Equal(2, sos.Skill);                // WS 2+
        Assert.Equal(6, sos.Strength.ExpectedValue(), 3);
        Assert.Equal(-4, sos.ArmourPenetration);
        Assert.Equal(2, sos.Damage.ExpectedValue(), 3);
        Assert.Contains(sos.Abilities, a => a is DevastatingWounds);
    }

    [Fact]
    public void Azrael_lion_helm_detects_a_4plus_invuln()
    {
        var helm = Unit("Azrael").UnitAbilities.Single(a => a.Name == "The Lion Helm");
        Assert.Equal(4, helm.InvulnSave);
    }

    [Fact]
    public void Heavy_intercessor_squad_reconstructs_to_one_sergeant_and_four_bodies()
    {
        var squad = Unit("Heavy Intercessor Squad");
        Assert.Equal(5, squad.TotalModels);

        var sgt = squad.ModelGroups.Single(g => g.Profile.Name == "Heavy Intercessor Sergeant");
        Assert.Equal(1, sgt.Count);
        var bodies = squad.ModelGroups.Single(g => g.Profile.Name == "Heavy Intercessors");
        Assert.Equal(4, bodies.Count);

        // Each model-group is T6 / W3 / Sv3+.
        foreach (var g in squad.ModelGroups)
        {
            Assert.Equal(6, g.Profile.Toughness);
            Assert.Equal(3, g.Profile.Wounds);
            Assert.Equal(3, g.Profile.Save);
        }

        // The sergeant carries the Heavy Bolt Rifle (Assault, Heavy) + Bolt pistol + Close combat weapon.
        var hbr = sgt.Weapons.Single(w => w.Name == "Heavy Bolt Rifle");
        Assert.Contains(hbr.Abilities, a => a is Assault);
        Assert.Contains(hbr.Abilities, a => a is Heavy);
        Assert.Contains(sgt.Weapons, w => w.Name == "Bolt pistol");
        Assert.Contains(sgt.Weapons, w => w.Name == "Close combat weapon");
    }

    [Fact]
    public void Calibanite_greatsword_exposes_two_firing_modes()
    {
        var icc = Unit("Inner Circle Companions");
        var greatsword = icc.AllWeapons.Single(w => w.HasFiringModes);
        Assert.Equal(2, greatsword.FiringModes.Count);

        var strike = greatsword.FiringModes[0];
        Assert.Equal(4, strike.Attacks.ExpectedValue(), 3);
        Assert.Contains(strike.Abilities, a => a is LethalHits);

        var sweep = greatsword.FiringModes[1];
        Assert.Equal(5, sweep.Attacks.ExpectedValue(), 3);
        Assert.Contains(sweep.Abilities, a => a is SustainedHits { X: 2 });
    }

    [Fact]
    public void Inner_circle_detects_minus_one_damage()
    {
        var ability = Unit("Inner Circle Companions").UnitAbilities.Single(a => a.Name == "Honoured Protectors");
        Assert.Equal(1, ability.DamageReductionFlat);
    }
}
