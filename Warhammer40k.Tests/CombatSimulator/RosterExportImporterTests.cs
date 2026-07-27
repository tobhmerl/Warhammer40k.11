using Warhammer40k._11.Features.CombatSimulator.Domain;
using Warhammer40k._11.Features.CombatSimulator.Import;

namespace Warhammer40k.Tests.CombatSimulator;

/// <summary>
/// Pins the roster-export importer against a real export of the app's own format, and the format detection
/// in <see cref="ArmyImporter"/>. The point of this format is that everything is pre-resolved, so the
/// assertions check that resolved values survive the round trip rather than being re-derived.
/// Part of the removable Combat Simulator feature.
/// </summary>
public class RosterExportImporterTests
{
    private static string Json(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CombatSimulator", "Fixtures", file));

    private static ImportResult Imported() => RosterExportImporter.Import(Json("roster-export-sample.json"));

    private static CombatUnit Unit(string name) => Imported().Units.Single(u => u.Name == name);

    [Fact]
    public void Imports_every_unit_from_the_export()
    {
        var units = Imported().Units;

        Assert.Equal(3, units.Count);
        Assert.Contains(units, u => u.Name == "Hellblaster Squad + Azrael");
        Assert.Contains(units, u => u.Name == "Inner Circle Companions + Librarian");
        Assert.Contains(units, u => u.Name == "Ballistus Dreadnought");
    }

    [Fact]
    public void Faction_comes_from_the_roster_header()
    {
        Assert.Equal("Dark Angels", Unit("Ballistus Dreadnought").Faction);
    }

    [Fact]
    public void Attached_leader_stays_one_unit_with_both_model_groups()
    {
        var unit = Unit("Hellblaster Squad + Azrael");

        Assert.True(unit.IsAttachedUnit);
        Assert.Equal(10, unit.TotalModels);
        Assert.Collection(unit.ModelGroups,
            body =>
            {
                Assert.Equal("Hellblaster", body.Profile.Name);
                Assert.Equal(9, body.Count);
                Assert.Equal(2, body.Profile.Wounds);
            },
            leader =>
            {
                Assert.Equal("Azrael", leader.Profile.Name);
                Assert.Equal(1, leader.Count);
                Assert.Equal(6, leader.Profile.Wounds);
                Assert.Equal(2, leader.Profile.Save);
            });
    }

    [Fact]
    public void Unit_wide_invulnerable_save_reaches_every_model_group()
    {
        var unit = Unit("Hellblaster Squad + Azrael");

        Assert.All(unit.ModelGroups, g => Assert.Equal(4, g.Profile.InvulnSave));
    }

    [Fact]
    public void Conditional_feel_no_pain_is_imported_as_restricted_and_warned_about()
    {
        var result = Imported();
        var unit = result.Units.Single(u => u.Name == "Inner Circle Companions + Librarian");

        Assert.All(unit.ModelGroups, g =>
        {
            Assert.Equal(4, g.Profile.FeelNoPain);
            Assert.True(g.Profile.FnpMortalOnly);
        });
        Assert.Contains(result.Warnings, w => w.Contains("conditional Feel No Pain", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Weapon_profiles_carry_stats_abilities_and_carrier_counts()
    {
        var plasma = Unit("Hellblaster Squad + Azrael").AllWeapons
            .Single(w => w.Name == "Plasma Incinerator - Supercharge");

        Assert.False(plasma.IsMelee);
        Assert.Equal("24\"", plasma.Range);
        Assert.Equal(2, plasma.Attacks.ExpectedValue(), 3);
        Assert.Equal(3, plasma.Skill);
        Assert.Equal(8, plasma.Strength.ExpectedValue(), 3);
        Assert.Equal(-3, plasma.ArmourPenetration);
        Assert.Equal(2, plasma.Damage.ExpectedValue(), 3);
        Assert.Equal(9, plasma.CarriedByModels);
        Assert.True(plasma.Has<SustainedHits>());
        Assert.True(plasma.Has<Hazardous>());
    }

    [Fact]
    public void Leader_granted_weapon_abilities_survive_the_import()
    {
        // The export bakes Azrael's Sustained Hits into the bodyguard's profiles; losing it here would
        // silently understate the unit.
        var ccw = Unit("Hellblaster Squad + Azrael").AllWeapons
            .Single(w => w.Name == "Close combat weapon");

        Assert.True(ccw.IsMelee);
        Assert.True(ccw.Has<SustainedHits>());
    }

    [Fact]
    public void Dice_expressions_in_stats_are_parsed()
    {
        var weapons = Unit("Ballistus Dreadnought").AllWeapons.ToList();

        var lascannon = weapons.Single(w => w.Name == "Ballistus Lascannon");
        Assert.Equal(4.5, lascannon.Damage.ExpectedValue(), 2);      // D6+1

        var frag = weapons.Single(w => w.Name == "Ballistus Missile Launcher - Frag");
        Assert.Equal(7, frag.Attacks.ExpectedValue(), 2);            // 2D6
    }

    [Fact]
    public void Keywords_and_applied_modifiers_are_kept_for_display_and_anti_gating()
    {
        var unit = Unit("Hellblaster Squad + Azrael");

        Assert.Contains("Infantry", unit.Keywords);
        Assert.Contains(unit.InheritedEffects, e => e.Contains("Invulnerable Save 4+"));
    }

    [Fact]
    public void Units_without_saves_get_none_rather_than_a_default()
    {
        var dread = Unit("Ballistus Dreadnought").ModelGroups.Single();

        Assert.Null(dread.Profile.InvulnSave);
        Assert.Null(dread.Profile.FeelNoPain);
        Assert.Equal(10, dread.Profile.Toughness);
        Assert.Equal(12, dread.Profile.Wounds);
    }

    [Fact]
    public void Imported_units_are_marked_as_imported()
    {
        Assert.All(Imported().Units, u => Assert.Equal(CombatSource.Imported, u.Source));
    }

    [Fact]
    public void ArmyImporter_routes_a_roster_export_to_this_parser()
    {
        var result = ArmyImporter.Import(Json("roster-export-sample.json"));

        Assert.Equal(3, result.Units.Count);
        Assert.Contains(result.Units, u => u.Name == "Hellblaster Squad + Azrael");
    }

    [Fact]
    public void ArmyImporter_still_routes_a_new_recruit_export_to_the_old_parser()
    {
        var result = ArmyImporter.Import(Json("dark-angels-sample.json"));

        Assert.Contains(result.Units, u => u.Name == "Azrael");
        Assert.Contains(result.Units, u => u.Name == "Heavy Intercessor Squad");
    }

    [Fact]
    public void Unparsable_json_reports_a_warning_rather_than_throwing()
    {
        var result = ArmyImporter.Import("{ not json");

        Assert.Empty(result.Units);
        Assert.NotEmpty(result.Warnings);
    }
}
