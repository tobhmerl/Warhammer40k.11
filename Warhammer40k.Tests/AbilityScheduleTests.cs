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

        // A unit ability and an army rule of the same name never collide.
        Assert.NotEqual(AbilityScheduleKeys.ForUnitAbility("x", "Protocols"), AbilityScheduleKeys.ForArmyRule("Protocols"));
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
}
