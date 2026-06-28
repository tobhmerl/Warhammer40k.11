using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins the 11th-edition detachment data: which detachments are selectable, Cryptek Conclave's DP cost and
/// smart effects, and the Detachment-Points budget by points level.
/// </summary>
public class DetachmentCatalogueTests
{
    [Theory]
    [InlineData(500, 2)]
    [InlineData(1000, 2)]
    [InlineData(1250, 3)]
    [InlineData(1500, 3)]
    [InlineData(2000, 3)]
    [InlineData(3000, 3)]
    public void Budget_is_2_at_or_below_1000_else_3(int points, int expected) =>
        Assert.Equal(expected, DetachmentCatalogue.Budget(points));

    [Fact]
    public void Only_detachments_with_authored_rules_are_selectable()
    {
        Assert.All(DetachmentCatalogue.Selectable, d => Assert.True(d.Enabled));
        Assert.Contains(DetachmentCatalogue.Selectable, d => d.Id == "cryptek-conclave");
        Assert.Contains(DetachmentCatalogue.Selectable, d => d.Id == "hand-of-the-dynasty");
        // The others are still disabled (hidden) until their rules are authored.
        Assert.DoesNotContain(DetachmentCatalogue.Selectable, d => d.Id == "skyshroud-spearhead");
    }

    [Fact]
    public void Cryptek_Conclave_is_2DP_with_a_Cryptek_Assault_grant_and_shooting_choices()
    {
        var cryptek = DetachmentCatalogue.FindById("cryptek-conclave");

        Assert.NotNull(cryptek);
        Assert.True(cryptek!.Enabled);
        Assert.Equal(2, cryptek.DetachmentPoints);

        Assert.Contains(cryptek.WeaponGrants, g =>
            g.Keywords.Contains("Cryptek")
            && g.Scope == GrantScope.Model
            && g.WeaponClass == DetachmentWeaponClass.Ranged
            && g.Abilities.Contains("Assault"));

        var choice = Assert.Single(cryptek.WeaponChoices);
        Assert.Equal("Cryptek", choice.RequiresModelKeyword);
        Assert.Equal(5, choice.Options.Count);
        Assert.Contains("Ignores Cover", choice.Options);
    }

    [Fact]
    public void Hand_of_the_Dynasty_is_1DP_DYNASTY_with_a_unit_scoped_Assault_grant()
    {
        var hotd = DetachmentCatalogue.FindById("hand-of-the-dynasty");

        Assert.NotNull(hotd);
        Assert.True(hotd!.Enabled);
        Assert.Equal(1, hotd.DetachmentPoints);
        Assert.Contains("Dynasty", hotd.Tags);

        var grant = Assert.Single(hotd.WeaponGrants);
        Assert.Equal(GrantScope.Unit, grant.Scope);
        Assert.Contains("Immortals", grant.Keywords);
        Assert.Contains("Necron Warriors", grant.Keywords);
        Assert.Contains("Assault", grant.Abilities);
        Assert.Empty(hotd.WeaponChoices); // no selectable shooting ability
    }

    [Fact]
    public void Cryptek_Conclave_has_six_stratagems_filtered_by_phase_and_turn()
    {
        var cryptek = DetachmentCatalogue.FindById("cryptek-conclave")!;
        Assert.Equal(6, cryptek.Stratagems.Count);
        Assert.All(cryptek.Stratagems, s => Assert.Equal(1, s.CpCost));

        // "Untapped Power": your Shooting phase only.
        var untapped = cryptek.Stratagems.Single(s => s.Name == "Untapped Power");
        Assert.True(untapped.AppliesInPhase(BattlePhase.Shooting));
        Assert.True(untapped.AppliesInTurn(BattleTurn.Player));
        Assert.False(untapped.AppliesInTurn(BattleTurn.Opponent));
        Assert.False(untapped.AppliesInPhase(BattlePhase.Command));

        // "Potentiality Syphon": opponent's Command phase only.
        var syphon = cryptek.Stratagems.Single(s => s.Name == "Potentiality Syphon");
        Assert.True(syphon.AppliesInPhase(BattlePhase.Command));
        Assert.True(syphon.AppliesInTurn(BattleTurn.Opponent));
        Assert.False(syphon.AppliesInTurn(BattleTurn.Player));
    }

    [Fact]
    public void Hand_of_the_Dynasty_has_three_stratagems_filtered_by_phase_and_turn()
    {
        var hotd = DetachmentCatalogue.FindById("hand-of-the-dynasty")!;
        Assert.Equal(3, hotd.Stratagems.Count);
        Assert.All(hotd.Stratagems, s => Assert.Equal(1, s.CpCost));

        // "Will of the Conqueror": your Movement phase only.
        var will = hotd.Stratagems.Single(s => s.Name == "Will of the Conqueror");
        Assert.True(will.AppliesInPhase(BattlePhase.Movement));
        Assert.True(will.AppliesInTurn(BattleTurn.Player));
        Assert.False(will.AppliesInTurn(BattleTurn.Opponent));

        // "Nanosaturation": opponent's Shooting phase only.
        var nano = hotd.Stratagems.Single(s => s.Name == "Nanosaturation");
        Assert.True(nano.AppliesInPhase(BattlePhase.Shooting));
        Assert.True(nano.AppliesInTurn(BattleTurn.Opponent));
        Assert.False(nano.AppliesInTurn(BattleTurn.Player));
    }

    [Fact]
    public void Cryptek_stratagems_require_the_keyword_they_target()
    {
        var cryptek = DetachmentCatalogue.FindById("cryptek-conclave")!;

        // Cryptek-targeting stratagems are hidden unless the army fields a CRYPTEK unit.
        foreach (var name in new[] { "Microscarab Swarm", "Animus Curse", "Synergistic Empowerment", "Untapped Power" })
            Assert.Equal(["Cryptek"], cryptek.Stratagems.Single(s => s.Name == name).RequiredUnitKeywords);

        // Army-wide NECRONS stratagems are never gated (the whole army is Necrons).
        Assert.Empty(cryptek.Stratagems.Single(s => s.Name == "Molecular Targeting").RequiredUnitKeywords);
        Assert.Empty(cryptek.Stratagems.Single(s => s.Name == "Potentiality Syphon").RequiredUnitKeywords);
    }

    [Fact]
    public void Hand_of_the_Dynasty_stratagems_require_the_keyword_they_target()
    {
        var hotd = DetachmentCatalogue.FindById("hand-of-the-dynasty")!;

        Assert.Equal(["Immortals"], hotd.Stratagems.Single(s => s.Name == "Dominance Protocols").RequiredUnitKeywords);
        Assert.Equal(["Immortals", "Necron Warriors"], hotd.Stratagems.Single(s => s.Name == "Will of the Conqueror").RequiredUnitKeywords);
        Assert.Equal(["Immortals", "Necron Warriors"], hotd.Stratagems.Single(s => s.Name == "Nanosaturation").RequiredUnitKeywords);
    }

    [Fact]
    public void Cryptek_Conclave_enhancements_carry_text_and_eligibility()
    {
        var cryptek = DetachmentCatalogue.FindById("cryptek-conclave")!;

        // "CRYPTEK model only" enhancements require the Cryptek keyword.
        var atomic = cryptek.FindEnhancement("atomic-disintegrators")!;
        Assert.False(string.IsNullOrWhiteSpace(atomic.Text));
        Assert.Equal(EnhancementScope.Character, atomic.Scope);
        Assert.Contains("Cryptek", atomic.Eligibility.RequiredKeywords);
        Assert.Contains("Cryptek", cryptek.FindEnhancement("gravitic-bolas")!.Eligibility.RequiredKeywords);

        // "NECRONS model only" enhancements are unconstrained (any eligible Character).
        var gauntlet = cryptek.FindEnhancement("gauntlet-of-compression")!;
        Assert.False(string.IsNullOrWhiteSpace(gauntlet.Text));
        Assert.True(gauntlet.Eligibility.IsUnconstrained);
        Assert.True(cryptek.FindEnhancement("quantum-abacus")!.Eligibility.IsUnconstrained);
    }

    [Fact]
    public void Hand_of_the_Dynasty_has_unit_scoped_upgrades()
    {
        var hotd = DetachmentCatalogue.FindById("hand-of-the-dynasty")!;

        var sentinels = hotd.FindEnhancement("enlivened-sentinels")!;
        Assert.Equal(EnhancementScope.Unit, sentinels.Scope);
        Assert.Equal(20, sentinels.Points);
        Assert.Contains("Necron Warriors", sentinels.Eligibility.RequiredKeywords);
        Assert.False(string.IsNullOrWhiteSpace(sentinels.Text));

        var tools = hotd.FindEnhancement("tools-of-dominion")!;
        Assert.Equal(EnhancementScope.Unit, tools.Scope);
        Assert.Equal(15, tools.Points);
        Assert.Contains("Immortals", tools.Eligibility.RequiredKeywords);
    }

    [Fact]
    public void Gauntlet_of_Compression_is_a_unit_wide_range_buff()
    {
        var gauntlet = DetachmentCatalogue.FindById("cryptek-conclave")!.FindEnhancement("gauntlet-of-compression")!;

        Assert.True(gauntlet.AffectsWholeUnit);
        var mod = Assert.Single(gauntlet.StatModifiers);
        Assert.Equal(StatTarget.Range, mod.Target);
        Assert.Equal(6, mod.Delta);
        Assert.Equal(WeaponClass.Ranged, mod.WeaponClass);
    }

    [Fact]
    public void Detachment_stratagem_surfaces_only_when_scheduled_and_eligible()
    {
        var cryptek = DetachmentCatalogue.FindById("cryptek-conclave")!;
        var molecular = cryptek.Stratagems.Single(s => s.Name == "Molecular Targeting");

        // A Necrons army (Plasmancer is a CRYPTEK) so keyword eligibility passes for CRYPTEK/NECRONS stratagems.
        var plasmancer = new Datasheet
        {
            Id = "plasmancer", Name = "Plasmancer",
            StatProfiles = [new StatProfile { Name = "Plasmancer", Wounds = "4" }],
            Keywords = ["Necrons", "Character", "Cryptek"],
        };
        var roster = new Roster { Units = [new RosterUnit { Id = "u1", DatasheetId = "plasmancer", ModelCount = 1 }] };
        var catalogue = new CatalogueData { Datasheets = [plasmancer] };

        var battle = BattleRoster.Build(roster, catalogue, [cryptek]);

        // Not scheduled yet ? never surfaces, in any phase or turn.
        Assert.False(battle.DetachmentStratagemUsable(cryptek, molecular, BattlePhase.Shooting, BattleTurn.Player));

        // Schedule it for my Shooting phase ? surfaces there, but not in the Fight phase or the opponent's turn.
        roster.GetOrCreateSchedule(AbilityScheduleKeys.ForDetachmentStratagem(cryptek.Id, molecular.Id))
            .SetWindow(BattlePhase.Shooting, BattleTurn.Player, true);

        Assert.True(battle.DetachmentStratagemUsable(cryptek, molecular, BattlePhase.Shooting, BattleTurn.Player));
        Assert.False(battle.DetachmentStratagemUsable(cryptek, molecular, BattlePhase.Shooting, BattleTurn.Opponent));
        Assert.False(battle.DetachmentStratagemUsable(cryptek, molecular, BattlePhase.Fight, BattleTurn.Player));
    }

    [Fact]
    public void Keyword_gated_detachment_stratagem_is_hidden_without_the_keyword()
    {
        var cryptek = DetachmentCatalogue.FindById("cryptek-conclave")!;
        var swarm = cryptek.Stratagems.Single(s => s.Name == "Microscarab Swarm"); // requires CRYPTEK

        // An army with no CRYPTEK unit (plain Warriors).
        var warriors = new Datasheet
        {
            Id = "necron-warriors", Name = "Necron Warriors",
            StatProfiles = [new StatProfile { Name = "Necron Warriors", Wounds = "1" }],
            Keywords = ["Necrons", "Necron Warriors"],
        };
        var roster = new Roster { Units = [new RosterUnit { Id = "u1", DatasheetId = "necron-warriors", ModelCount = 10 }] };
        var catalogue = new CatalogueData { Datasheets = [warriors] };
        var battle = BattleRoster.Build(roster, catalogue, [cryptek]);

        // Even when scheduled, it stays hidden because the army fields no CRYPTEK unit.
        roster.GetOrCreateSchedule(AbilityScheduleKeys.ForDetachmentStratagem(cryptek.Id, swarm.Id))
            .SetWindow(BattlePhase.Shooting, BattleTurn.Opponent, true);

        Assert.False(battle.DetachmentStratagemUsable(cryptek, swarm, BattlePhase.Shooting, BattleTurn.Opponent));
    }

    [Fact]
    public void Atomic_Disintegrators_adds_anti_monster_and_anti_vehicle_shooting_options()
    {
        var atomic = DetachmentCatalogue.FindById("cryptek-conclave")!.FindEnhancement("atomic-disintegrators")!;

        Assert.Contains("Anti-MONSTER 5+", atomic.ShootingAbilityOptions);
        Assert.Contains("Anti-VEHICLE 5+", atomic.ShootingAbilityOptions);
    }

    [Theory]
    [InlineData("annihilation-legion", 2)]
    [InlineData("awakened-dynasty", 3)]
    [InlineData("canoptek-court", 3)]
    [InlineData("hypercrypt-legion", 2)]
    [InlineData("obeisance-phalanx", 2)]
    [InlineData("skyshroud-spearhead", 1)]
    [InlineData("the-phaerons-armoury", 1)]
    [InlineData("starshatter-arsenal", 3)]
    [InlineData("cursed-legion", 2)]
    public void All_twelve_detachments_carry_their_detachment_points(string id, int dp)
    {
        var d = DetachmentCatalogue.FindById(id);
        Assert.NotNull(d);
        Assert.Equal(dp, d!.DetachmentPoints);
    }

    [Theory]
    [InlineData("annihilation-legion", "soulless-reaper", 15)]
    [InlineData("awakened-dynasty", "phasal-subjugator", 35)]
    [InlineData("canoptek-court", "metalodermal-tesla-weave", 10)]
    [InlineData("hypercrypt-legion", "arisen-tyrant", 25)]
    [InlineData("obeisance-phalanx", "eternal-conqueror", 25)]
    [InlineData("skyshroud-spearhead", "recursive-reanimation", 5)]
    [InlineData("the-phaerons-armoury", "prelocational-optimiser", 25)]
    public void Newly_costed_detachments_carry_their_enhancement_points(string detachmentId, string enhancementId, int points)
    {
        var enhancement = DetachmentCatalogue.FindById(detachmentId)!.FindEnhancement(enhancementId);
        Assert.NotNull(enhancement);
        Assert.Equal(points, enhancement!.Points);
    }

    [Fact]
    public void Points_only_detachments_stay_disabled_until_rules_are_authored()
    {
        foreach (var id in new[] { "annihilation-legion", "awakened-dynasty", "canoptek-court", "hypercrypt-legion", "obeisance-phalanx", "skyshroud-spearhead", "the-phaerons-armoury" })
            Assert.False(DetachmentCatalogue.FindById(id)!.Enabled);
    }

    [Fact]
    public void Starshatter_Arsenal_is_3DP_enabled_with_Relentless_Onslaught_and_a_non_Titanic_Assault_grant()
    {
        var starshatter = DetachmentCatalogue.FindById("starshatter-arsenal");

        Assert.NotNull(starshatter);
        Assert.True(starshatter!.Enabled);
        Assert.Equal(3, starshatter.DetachmentPoints);

        var rule = Assert.Single(starshatter.Rules);
        Assert.Equal("Relentless Onslaught", rule.Name);

        var grant = Assert.Single(starshatter.WeaponGrants);
        Assert.Equal(GrantScope.Model, grant.Scope);
        Assert.Equal(DetachmentWeaponClass.Ranged, grant.WeaponClass);
        Assert.Contains("Vehicle", grant.Keywords);
        Assert.Contains("Mounted", grant.Keywords);
        Assert.Contains("Titanic", grant.ExcludedKeywords);
        Assert.Contains("Assault", grant.Abilities);
    }

    [Fact]
    public void Relentless_Onslaught_carries_a_schedulable_plus1_hit_conditional_buff()
    {
        var rule = Assert.Single(DetachmentCatalogue.FindById("starshatter-arsenal")!.Rules);

        var buff = Assert.Single(rule.ConditionalBuffs);
        Assert.Equal("Relentless Onslaught", buff.Label);
        Assert.Contains("Necrons", buff.RequiredKeywords);
        Assert.Contains("Monster", buff.ExcludedKeywords);

        var mod = Assert.Single(buff.Modifiers);
        Assert.Equal(StatTarget.Skill, mod.Target);
        Assert.Equal(1, mod.Delta);
    }

    [Fact]
    public void Starshatter_Arsenal_enhancements_carry_text_and_eligibility()
    {
        var starshatter = DetachmentCatalogue.FindById("starshatter-arsenal")!;

        // Dread Majesty is OVERLORD-or-CATACOMB-COMMAND-BARGE only (any-of keywords).
        var dread = starshatter.FindEnhancement("dread-majesty")!;
        Assert.False(string.IsNullOrWhiteSpace(dread.Text));
        Assert.Contains("Overlord", dread.Eligibility.AnyOfKeywords);
        Assert.Contains("Catacomb Command Barge", dread.Eligibility.AnyOfKeywords);

        // The other three are NECRONS-unconstrained but carry their rules text.
        foreach (var id in new[] { "miniaturised-nebuloscope", "demanding-leader", "chrono-impedance-fields" })
        {
            var enh = starshatter.FindEnhancement(id)!;
            Assert.False(string.IsNullOrWhiteSpace(enh.Text));
            Assert.True(enh.Eligibility.IsUnconstrained);
        }
    }

    [Fact]
    public void Starshatter_Arsenal_has_six_stratagems_filtered_by_phase_turn_and_keyword()
    {
        var starshatter = DetachmentCatalogue.FindById("starshatter-arsenal")!;
        Assert.Equal(6, starshatter.Stratagems.Count);
        Assert.All(starshatter.Stratagems, s => Assert.False(string.IsNullOrWhiteSpace(s.When)));
        Assert.All(starshatter.Stratagems, s => Assert.False(string.IsNullOrWhiteSpace(s.Effect)));

        // Merciless Reclamation: 2CP, your Shooting OR Fight phase, no keyword gate.
        var merciless = starshatter.Stratagems.Single(s => s.Name == "Merciless Reclamation");
        Assert.Equal(2, merciless.CpCost);
        Assert.True(merciless.AppliesInPhase(BattlePhase.Shooting));
        Assert.True(merciless.AppliesInPhase(BattlePhase.Fight));
        Assert.False(merciless.AppliesInPhase(BattlePhase.Movement));
        Assert.True(merciless.AppliesInTurn(BattleTurn.Player));
        Assert.False(merciless.AppliesInTurn(BattleTurn.Opponent));
        Assert.Empty(merciless.RequiredUnitKeywords);

        // Unyielding Forms: 2CP, opponent's Shooting OR Fight phase, VEHICLE/MOUNTED only.
        var unyielding = starshatter.Stratagems.Single(s => s.Name == "Unyielding Forms");
        Assert.Equal(2, unyielding.CpCost);
        Assert.True(unyielding.AppliesInTurn(BattleTurn.Opponent));
        Assert.False(unyielding.AppliesInTurn(BattleTurn.Player));
        Assert.Equal(["Vehicle", "Mounted"], unyielding.RequiredUnitKeywords);

        // Movement-phase Strategic Ploys are VEHICLE/MOUNTED-gated.
        foreach (var name in new[] { "Chronoshift", "Dimensional Tunnel" })
        {
            var s = starshatter.Stratagems.Single(x => x.Name == name);
            Assert.Equal(1, s.CpCost);
            Assert.True(s.AppliesInPhase(BattlePhase.Movement));
            Assert.True(s.AppliesInTurn(BattleTurn.Player));
            Assert.Equal(["Vehicle", "Mounted"], s.RequiredUnitKeywords);
        }

        // Endless Servitude: 1CP, your Fight phase, any NECRONS unit (no keyword gate).
        var endless = starshatter.Stratagems.Single(s => s.Name == "Endless Servitude");
        Assert.Equal(1, endless.CpCost);
        Assert.True(endless.AppliesInPhase(BattlePhase.Fight));
        Assert.True(endless.AppliesInTurn(BattleTurn.Player));
        Assert.Empty(endless.RequiredUnitKeywords);

        // Reactive Reposition: 1CP, opponent's Shooting phase, any NECRONS unit.
        var reactive = starshatter.Stratagems.Single(s => s.Name == "Reactive Reposition");
        Assert.Equal(1, reactive.CpCost);
        Assert.True(reactive.AppliesInPhase(BattlePhase.Shooting));
        Assert.True(reactive.AppliesInTurn(BattleTurn.Opponent));
        Assert.False(reactive.AppliesInTurn(BattleTurn.Player));
        Assert.Empty(reactive.RequiredUnitKeywords);
    }
}
