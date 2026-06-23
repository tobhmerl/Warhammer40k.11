using Warhammer40k.Core.Play;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins the "need to know now" filtering of <see cref="CoreStratagemCatalogue"/>: a stratagem only surfaces
/// when both its phase set (empty = any phase) and its "Used in" turn marker match the current moment, the
/// rulebook order is preserved, and the authored content is well-formed.
/// </summary>
public class CoreStratagemCatalogueTests
{
    private static readonly BattlePhase[] AllPhases =
        [BattlePhase.Command, BattlePhase.Movement, BattlePhase.Shooting, BattlePhase.Charge, BattlePhase.Fight];

    [Fact]
    public void Command_reroll_is_any_phase()
    {
        var reroll = CoreStratagemCatalogue.All.Single(s => s.Id == "15.02");
        Assert.True(reroll.AppliesInAnyPhase);
    }

    [Theory]
    [InlineData(BattleTurn.Player)]
    [InlineData(BattleTurn.Opponent)]
    public void Command_reroll_surfaces_in_every_phase_for_both_turns(BattleTurn turn)
    {
        foreach (var phase in AllPhases)
            Assert.Contains(CoreStratagemCatalogue.Usable(phase, turn), s => s.Id == "15.02");
    }

    [Fact]
    public void Explosives_only_surfaces_in_your_shooting_phase()
    {
        Assert.Contains(CoreStratagemCatalogue.Usable(BattlePhase.Shooting, BattleTurn.Player), s => s.Id == "15.05");
        // Wrong phase.
        Assert.DoesNotContain(CoreStratagemCatalogue.Usable(BattlePhase.Fight, BattleTurn.Player), s => s.Id == "15.05");
        // Wrong turn (marked "Your turn").
        Assert.DoesNotContain(CoreStratagemCatalogue.Usable(BattlePhase.Shooting, BattleTurn.Opponent), s => s.Id == "15.05");
    }

    [Fact]
    public void Insane_bravery_only_surfaces_in_your_command_phase()
    {
        Assert.Contains(CoreStratagemCatalogue.Usable(BattlePhase.Command, BattleTurn.Player), s => s.Id == "15.04");
        Assert.DoesNotContain(CoreStratagemCatalogue.Usable(BattlePhase.Command, BattleTurn.Opponent), s => s.Id == "15.04");
    }

    [Fact]
    public void Counteroffensive_only_surfaces_in_opponents_fight_phase()
    {
        Assert.Contains(CoreStratagemCatalogue.Usable(BattlePhase.Fight, BattleTurn.Opponent), s => s.Id == "15.12");
        // Wrong turn (marked "Opponent's turn").
        Assert.DoesNotContain(CoreStratagemCatalogue.Usable(BattlePhase.Fight, BattleTurn.Player), s => s.Id == "15.12");
        // Wrong phase.
        Assert.DoesNotContain(CoreStratagemCatalogue.Usable(BattlePhase.Shooting, BattleTurn.Opponent), s => s.Id == "15.12");
    }

    [Theory]
    [InlineData(BattleTurn.Player)]
    [InlineData(BattleTurn.Opponent)]
    public void Epic_challenge_surfaces_in_the_fight_phase_in_either_turn(BattleTurn turn)
    {
        Assert.Contains(CoreStratagemCatalogue.Usable(BattlePhase.Fight, turn), s => s.Id == "15.03");
        Assert.DoesNotContain(CoreStratagemCatalogue.Usable(BattlePhase.Shooting, turn), s => s.Id == "15.03");
    }

    [Fact]
    public void Usable_preserves_rulebook_order()
    {
        var ids = CoreStratagemCatalogue.Usable(BattlePhase.Fight, BattleTurn.Player).Select(s => s.Id).ToList();
        Assert.Equal(ids.OrderBy(id => id, StringComparer.Ordinal), ids);
    }

    [Fact]
    public void Affordability_follows_the_command_point_pool()
    {
        // Mirrors the Play-Mode "Use" gate: a stratagem is usable only when CP on hand covers its cost.
        static bool CanAfford(CoreStratagem s, int cp) => cp >= s.Cost;
        var explosives = CoreStratagemCatalogue.All.Single(s => s.Id == "15.05");

        Assert.False(CanAfford(explosives, 0));
        Assert.True(CanAfford(explosives, explosives.Cost));
    }

    [Fact]
    public void Every_stratagem_is_well_formed()
    {
        Assert.NotEmpty(CoreStratagemCatalogue.All);
        foreach (var s in CoreStratagemCatalogue.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id));
            Assert.False(string.IsNullOrWhiteSpace(s.Name));
            Assert.True(s.Cost > 0, $"{s.Id} should cost at least 1 CP");
            Assert.False(string.IsNullOrWhiteSpace(s.When), $"{s.Id} should have a WHEN clause");
            Assert.False(string.IsNullOrWhiteSpace(s.Target), $"{s.Id} should have a TARGET clause");
            Assert.False(string.IsNullOrWhiteSpace(s.Effect), $"{s.Id} should have an EFFECT clause");
        }
    }

    [Fact]
    public void Stratagem_ids_are_unique()
    {
        var ids = CoreStratagemCatalogue.All.Select(s => s.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("15.03", "Character")]            // Epic Challenge — needs a CHARACTER unit.
    [InlineData("15.05", "Explosives", "Grenades")] // Explosives — needs an EXPLOSIVES/GRENADES unit.
    [InlineData("15.06", "Monster", "Vehicle")]   // Crushing Impact — needs a MONSTER/VEHICLE unit.
    [InlineData("15.10", "Smoke")]                // Smokescreen — needs a SMOKE unit.
    public void Keyword_gated_stratagems_declare_their_required_unit_keywords(string id, params string[] expected)
    {
        var stratagem = CoreStratagemCatalogue.All.Single(s => s.Id == id);
        Assert.Equal(expected, stratagem.RequiredUnitKeywords);
    }

    [Fact]
    public void Universal_stratagems_have_no_unit_keyword_requirement()
    {
        // Command Re-roll (15.02) and Counteroffensive (15.12) apply to "one friendly unit" — never hidden.
        Assert.Empty(CoreStratagemCatalogue.All.Single(s => s.Id == "15.02").RequiredUnitKeywords);
        Assert.Empty(CoreStratagemCatalogue.All.Single(s => s.Id == "15.12").RequiredUnitKeywords);
    }
}
