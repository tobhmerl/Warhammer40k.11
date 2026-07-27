using Warhammer40k.Api;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;
using Warhammer40k._11.Features.CombatSimulator.Adapters;
using Warhammer40k._11.Features.CombatSimulator.Domain;

namespace Warhammer40k.Tests.CombatSimulator;

/// <summary>
/// The native adapter must carry a unit's datasheet keywords into the simulator, otherwise Anti-[keyword]
/// can never match its target. Part of the removable Combat Simulator feature.
/// </summary>
public class NativeNecronSourceTests
{
    private static CombatUnit Map(string datasheetId, int models)
    {
        var catalogue = CatalogueProvider.LoadEmbedded();
        var roster = new Roster
        {
            Units = [new RosterUnit { Id = "r1", DatasheetId = datasheetId, ModelCount = models }],
        };
        var battle = BattleRoster.Build(roster, catalogue);
        return NativeNecronSource.FromBattleUnit(battle, battle.Units.Single());
    }

    [Fact]
    public void Real_seed_warriors_carry_their_datasheet_keywords()
    {
        var combat = Map("necron-warriors", 10);

        Assert.Contains("Infantry", combat.Keywords, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Qualified_faction_keyword_also_contributes_its_bare_value()
    {
        var combat = Map("necron-warriors", 10);

        // The datasheet carries "Faction: Necrons"; both spellings are offered so Anti-X matches either way.
        Assert.Contains("Faction: Necrons", combat.Keywords, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Necrons", combat.Keywords, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Keywords_are_de_duplicated()
    {
        var combat = Map("necron-warriors", 10);

        Assert.Equal(combat.Keywords.Distinct(StringComparer.OrdinalIgnoreCase).Count(), combat.Keywords.Count);
    }

    // ---- Automatic inheritance of the modifiers Play Mode already resolves ----

    private static CombatUnit MapWithDetachment(string datasheetId, int models, string detachmentId)
    {
        var catalogue = CatalogueProvider.LoadEmbedded();
        var roster = new Roster
        {
            DetachmentIds = [detachmentId],
            Units = [new RosterUnit { Id = "r1", DatasheetId = datasheetId, ModelCount = models }],
        };
        var detachments = roster.DetachmentIds
            .Select(DetachmentCatalogue.FindById)
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList();
        var battle = BattleRoster.Build(roster, catalogue, detachments);
        return NativeNecronSource.FromBattleUnit(battle, battle.Units.Single());
    }

    [Fact]
    public void Detachment_stat_buff_is_baked_into_the_weapon_profile()
    {
        // Cursed Legion's Cold Fervour: +2 Strength on DESTROYER CULT models' weapons. Skorpekh Destroyers
        // carry that keyword, so the simulator must see the buffed Strength without the user typing it.
        var plain = Map("skorpekh-destroyers", 3);
        var buffed = MapWithDetachment("skorpekh-destroyers", 3, "cursed-legion");

        var plainStrength = plain.AllWeapons.First().Strength.ExpectedValue();
        var buffedStrength = buffed.AllWeapons.First().Strength.ExpectedValue();

        Assert.Equal(plainStrength + 2, buffedStrength);
    }

    [Fact]
    public void Inherited_effects_are_reported_for_display()
    {
        var buffed = MapWithDetachment("skorpekh-destroyers", 3, "cursed-legion");

        // The user must be able to see what was applied, otherwise they cannot tell it apart from "nothing".
        Assert.NotEmpty(buffed.InheritedEffects);
        Assert.Contains(buffed.InheritedEffects, e => e.Contains("Str", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_unit_without_roster_modifiers_reports_none()
    {
        var plain = Map("necron-warriors", 10);

        Assert.Empty(plain.InheritedEffects);
    }

    // Immortals led by a Plasmancer: "Harbinger of Destruction" makes a ranged Hit roll of 5+ a critical hit.
    private static CombatUnit MapImmortalsWithPlasmancer(bool applied)
    {
        var catalogue = CatalogueProvider.LoadEmbedded();
        var roster = new Roster
        {
            Units =
            [
                new RosterUnit { Id = "bodyguard", DatasheetId = "immortals", ModelCount = 10 },
                new RosterUnit { Id = "leader", DatasheetId = "plasmancer", ModelCount = 1, AttachedToRosterUnitId = "bodyguard" },
            ],
        };
        if (applied)
            roster.GetOrCreateSchedule(AbilityScheduleKeys.ForUnitAbility("plasmancer", "Harbinger of Destruction")).ApplyToUnit = true;

        var battle = BattleRoster.Build(roster, catalogue);
        return NativeNecronSource.FromBattleUnit(battle, battle.Units.Single());
    }

    [Fact]
    public void Plasmancer_critical_hit_on_five_reaches_ranged_weapons()
    {
        var combat = MapImmortalsWithPlasmancer(applied: true);

        var ranged = combat.AllWeapons.Where(w => !w.IsMelee).ToList();
        Assert.NotEmpty(ranged);
        Assert.All(ranged, w => Assert.Equal(5, w.CriticalHitOn));
        Assert.Contains(combat.InheritedEffects, e => e.Contains("Critical hit 5+", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Plasmancer_critical_hit_does_not_reach_melee_weapons()
    {
        var combat = MapImmortalsWithPlasmancer(applied: true);

        // The ability is worded for ranged attacks only, so melee keeps the unmodified 6+.
        Assert.All(combat.AllWeapons.Where(w => w.IsMelee), w => Assert.Null(w.CriticalHitOn));
    }
}
