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
    public void Multi_phase_your_turn_stratagem_keeps_the_fight_phase_in_both_turns()
    {
        // "Your Shooting phase or the Fight phase": Shooting is your-turn-only; the Fight phase is either turn.
        var molecular = DetachmentCatalogue.FindById("cryptek-conclave")!.Stratagems.Single(s => s.Name == "Molecular Targeting");

        Assert.True(molecular.UsableNow(BattlePhase.Shooting, BattleTurn.Player));
        Assert.False(molecular.UsableNow(BattlePhase.Shooting, BattleTurn.Opponent));
        Assert.True(molecular.UsableNow(BattlePhase.Fight, BattleTurn.Player));
        Assert.True(molecular.UsableNow(BattlePhase.Fight, BattleTurn.Opponent));
    }

    [Fact]
    public void Multi_phase_opponent_turn_stratagem_keeps_the_fight_phase_in_both_turns()
    {
        // "Your opponent's Shooting phase or the Fight phase": Shooting is opponent-only; the Fight phase is either turn.
        var swarm = DetachmentCatalogue.FindById("cryptek-conclave")!.Stratagems.Single(s => s.Name == "Microscarab Swarm");

        Assert.True(swarm.UsableNow(BattlePhase.Shooting, BattleTurn.Opponent));
        Assert.False(swarm.UsableNow(BattlePhase.Shooting, BattleTurn.Player));
        Assert.True(swarm.UsableNow(BattlePhase.Fight, BattleTurn.Player));
        Assert.True(swarm.UsableNow(BattlePhase.Fight, BattleTurn.Opponent));
    }

    [Fact]
    public void Atomic_Disintegrators_adds_anti_monster_and_anti_vehicle_shooting_options()
    {
        var atomic = DetachmentCatalogue.FindById("cryptek-conclave")!.FindEnhancement("atomic-disintegrators")!;

        Assert.Contains("Anti-MONSTER 5+", atomic.ShootingAbilityOptions);
        Assert.Contains("Anti-VEHICLE 5+", atomic.ShootingAbilityOptions);
    }
}
