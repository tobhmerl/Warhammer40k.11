using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins the manual ability/stratagem scheduling model: window set-membership, the "apply to unit" flag, the
/// stable key builder, and the <see cref="Roster"/> get-or-create / query helpers that drive Play Mode.
/// </summary>
public class AbilityScheduleTests
{
    [Fact]
    public void New_schedule_covers_nothing_and_is_not_applied()
    {
        var schedule = new AbilitySchedule { Key = "k" };

        Assert.False(schedule.ApplyToUnit);
        Assert.Empty(schedule.Windows);
        foreach (var phase in BattlePhases.Ordered)
        {
            Assert.False(schedule.Covers(phase, BattleTurn.Player));
            Assert.False(schedule.Covers(phase, BattleTurn.Opponent));
        }
    }

    [Fact]
    public void Set_window_toggles_a_single_phase_turn_cell_without_duplicates()
    {
        var schedule = new AbilitySchedule { Key = "k" };

        schedule.SetWindow(BattlePhase.Shooting, BattleTurn.Player, true);
        schedule.SetWindow(BattlePhase.Shooting, BattleTurn.Player, true); // idempotent
        Assert.Single(schedule.Windows);
        Assert.True(schedule.Covers(BattlePhase.Shooting, BattleTurn.Player));
        Assert.False(schedule.Covers(BattlePhase.Shooting, BattleTurn.Opponent));

        schedule.SetWindow(BattlePhase.Shooting, BattleTurn.Player, false);
        Assert.Empty(schedule.Windows);
        Assert.False(schedule.Covers(BattlePhase.Shooting, BattleTurn.Player));
    }

    [Fact]
    public void Keys_are_distinct_per_kind_and_stable()
    {
        Assert.Equal("unit|overlord|my will be done", AbilityScheduleKeys.ForUnitAbility("overlord", "My Will Be Done"));
        Assert.Equal("armyrule|reanimation protocols", AbilityScheduleKeys.ForArmyRule("Reanimation Protocols"));
        Assert.Equal("enh|gauntlet", AbilityScheduleKeys.ForEnhancement("gauntlet"));
        Assert.Equal("strat|core|15.02", AbilityScheduleKeys.ForCoreStratagem("15.02"));
        Assert.Equal("strat|cryptek-conclave|microscarab-swarm",
            AbilityScheduleKeys.ForDetachmentStratagem("cryptek-conclave", "microscarab-swarm"));
        Assert.Equal("detbuff|starshatter-arsenal|relentless onslaught",
            AbilityScheduleKeys.ForDetachmentBuff("starshatter-arsenal", "Relentless Onslaught"));

        // A unit ability and an army rule of the same name never collide.
        Assert.NotEqual(AbilityScheduleKeys.ForUnitAbility("x", "Protocols"), AbilityScheduleKeys.ForArmyRule("Protocols"));
        // A detachment buff and a detachment stratagem of the same ids never collide.
        Assert.NotEqual(AbilityScheduleKeys.ForDetachmentBuff("d", "x"), AbilityScheduleKeys.ForDetachmentStratagem("d", "x"));
    }

    [Fact]
    public void Get_or_create_schedule_is_idempotent_per_key()
    {
        var roster = new Roster();
        var first = roster.GetOrCreateSchedule("k");
        var second = roster.GetOrCreateSchedule("k");

        Assert.Same(first, second);
        Assert.Single(roster.AbilitySchedules);
    }

    [Fact]
    public void Roster_queries_default_to_unconfigured()
    {
        var roster = new Roster();

        Assert.Null(roster.FindSchedule("missing"));
        Assert.False(roster.IsScheduledNow("missing", BattlePhase.Command, BattleTurn.Player));
        Assert.False(roster.IsApplied("missing"));
    }

    [Fact]
    public void Roster_queries_reflect_configured_windows_and_apply_flag()
    {
        var roster = new Roster();
        var schedule = roster.GetOrCreateSchedule("k");
        schedule.ApplyToUnit = true;
        schedule.SetWindow(BattlePhase.Fight, BattleTurn.Opponent, true);

        Assert.True(roster.IsApplied("k"));
        Assert.True(roster.IsScheduledNow("k", BattlePhase.Fight, BattleTurn.Opponent));
        Assert.False(roster.IsScheduledNow("k", BattlePhase.Fight, BattleTurn.Player));
    }

    // A phase-limited ability (e.g. Grand Strategist, scheduled only in the player's Command phase) must not
    // be treated as always-available: it belongs in the Now ribbon during its window, never as a permanent
    // reference row on the unit card. Only an ability ticked for every phase + turn is "always available".
    [Fact]
    public void Single_phase_ability_is_not_always_available_and_active_only_in_its_window()
    {
        var ability = new BattleAbility(new Ability { Name = "Grand Strategist", Text = "At the start of your Command phase, gain 1CP." }, "Imotekh the Stormlord")
        {
            Windows = [new AbilityWindow(BattlePhase.Command, BattleTurn.Player)],
        };

        Assert.False(ability.IsAlwaysAvailable);
        Assert.True(BattleUnit.IsAbilityActiveInPhase(ability, BattlePhase.Command, BattleTurn.Player));
        Assert.False(BattleUnit.IsAbilityActiveInPhase(ability, BattlePhase.Movement, BattleTurn.Player));
    }

    // An ability the player ticked for all ten phase + turn cells is genuinely always-on, so it stays on the
    // card (as a calm chip) and is excluded from the per-phase "usable now" markers.
    [Fact]
    public void Every_window_ticked_marks_an_ability_always_available()
    {
        var windows = new List<AbilityWindow>();
        foreach (var phase in BattlePhases.Ordered)
        {
            windows.Add(new AbilityWindow(phase, BattleTurn.Player));
            windows.Add(new AbilityWindow(phase, BattleTurn.Opponent));
        }
        var ability = new BattleAbility(new Ability { Name = "Ever Vigilant", Text = "Always." }, "Unit") { Windows = windows };

        Assert.True(ability.IsAlwaysAvailable);
        Assert.False(BattleUnit.IsAbilityActiveInPhase(ability, BattlePhase.Command, BattleTurn.Player));
    }
}
