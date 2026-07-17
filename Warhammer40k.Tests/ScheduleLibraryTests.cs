using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins the cross-roster <see cref="ScheduleLibrary"/>: the single per-user source of truth for Play-Mode
/// timing. Covers get-or-create, migrating legacy per-roster schedules, and the "library wins" merge that
/// seeds every roster.
/// </summary>
public class ScheduleLibraryTests
{
    [Fact]
    public void Get_or_create_is_idempotent_per_key()
    {
        var library = new ScheduleLibrary();

        var first = library.GetOrCreate("unit|warriors|their number is legion");
        var second = library.GetOrCreate("unit|warriors|their number is legion");

        Assert.Same(first, second);
        Assert.Single(library.Schedules);
    }

    [Fact]
    public void Seed_missing_from_copies_only_unknown_keys()
    {
        var library = new ScheduleLibrary();
        library.GetOrCreate("keep").SetWindow(BattlePhase.Command, BattleTurn.Player, true);

        var roster = new Roster();
        // A key the library already has (must not be overwritten) and a new one (must be migrated).
        roster.GetOrCreateSchedule("keep").SetWindow(BattlePhase.Fight, BattleTurn.Opponent, true);
        roster.GetOrCreateSchedule("new").SetWindow(BattlePhase.Shooting, BattleTurn.Player, true);

        var added = library.SeedMissingFrom(roster);

        Assert.True(added);
        Assert.Equal(2, library.Schedules.Count);
        // The pre-existing library entry is untouched (library wins over the roster's copy of the same key).
        Assert.True(library.Find("keep")!.Covers(BattlePhase.Command, BattleTurn.Player));
        Assert.False(library.Find("keep")!.Covers(BattlePhase.Fight, BattleTurn.Opponent));
        Assert.True(library.Find("new")!.Covers(BattlePhase.Shooting, BattleTurn.Player));
    }

    [Fact]
    public void Seed_missing_from_reports_no_change_when_everything_is_known()
    {
        var library = new ScheduleLibrary();
        library.GetOrCreate("k");

        var roster = new Roster();
        roster.GetOrCreateSchedule("k");

        Assert.False(library.SeedMissingFrom(roster));
        Assert.Single(library.Schedules);
    }

    [Fact]
    public void Effective_for_lets_the_library_win_and_preserves_roster_only_keys()
    {
        var library = new ScheduleLibrary();
        library.GetOrCreate("shared").SetWindow(BattlePhase.Command, BattleTurn.Player, true);

        var roster = new Roster();
        // Same key, different window on the roster — the library's value must win.
        roster.GetOrCreateSchedule("shared").SetWindow(BattlePhase.Movement, BattleTurn.Player, true);
        // A key only the roster has — preserved as a fallback.
        roster.GetOrCreateSchedule("roster-only").SetWindow(BattlePhase.Fight, BattleTurn.Opponent, true);

        var effective = library.EffectiveFor(roster);

        Assert.Equal(2, effective.Count);
        var shared = effective.Single(s => s.Key == "shared");
        Assert.True(shared.Covers(BattlePhase.Command, BattleTurn.Player));
        Assert.False(shared.Covers(BattlePhase.Movement, BattleTurn.Player));
        var rosterOnly = effective.Single(s => s.Key == "roster-only");
        Assert.True(rosterOnly.Covers(BattlePhase.Fight, BattleTurn.Opponent));
    }

    [Fact]
    public void Empty_library_leaves_a_roster_schedules_intact()
    {
        var roster = new Roster();
        roster.GetOrCreateSchedule("k").SetWindow(BattlePhase.Charge, BattleTurn.Player, true);

        var effective = ScheduleLibrary.Empty.EffectiveFor(roster);

        Assert.Single(effective);
        Assert.True(effective[0].Covers(BattlePhase.Charge, BattleTurn.Player));
    }
}
