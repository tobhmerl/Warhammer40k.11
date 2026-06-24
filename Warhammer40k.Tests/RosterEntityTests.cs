using Warhammer40k.Api;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Tests;

/// <summary>
/// Pure mapping tests for <see cref="RosterEntity"/> (no live storage, matching the rest of the suite):
/// proves a <see cref="Roster"/> survives a From → entity → ToRoster round-trip, including the units that
/// are persisted as a JSON string column.
/// </summary>
public class RosterEntityTests
{
    private const string UserId = "github|123";

    private static Roster SampleRoster() => new()
    {
        Id = "abc123",
        Name = "Dynasty Vanguard",
        Faction = Roster.NecronsFaction,
        PointsLimit = 2000,
        DetachmentId = "hand-of-the-dynasty",
        CatalogueVersion = "2024.1",
        CreatedUtc = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero),
        Units =
        [
            new RosterUnit
            {
                Id = "u1",
                DatasheetId = "overlord",
                ModelCount = 1,
                IsWarlord = true,
                AssignedEnhancementId = "dynastic-heirloom",
            },
            new RosterUnit
            {
                Id = "u2",
                DatasheetId = "necron-warriors",
                ModelCount = 10,
                AttachedToRosterUnitId = "u1",
                Wargear = [new WargearSelection { GroupId = "g1", OptionIds = ["o1", "o2"] }],
            },
            new RosterUnit
            {
                Id = "u3",
                DatasheetId = "ctan-shard-of-the-nightbringer",
                ModelCount = 1,
                AppliedBindingId = "Quantum Goad",
                BindingSurcharge = 45,
            },
        ],
        AbilitySchedules =
        [
            new AbilitySchedule
            {
                Key = AbilityScheduleKeys.ForUnitAbility("overlord", "My Will Be Done"),
                ApplyToUnit = true,
                Windows = [new AbilityWindow(BattlePhase.Command, BattleTurn.Player)],
            },
        ],
    };

    [Fact]
    public void From_sets_partition_and_row_keys_from_user_and_roster()
    {
        var entity = RosterEntity.From(UserId, SampleRoster());

        Assert.Equal(UserId, entity.PartitionKey);
        Assert.Equal("abc123", entity.RowKey);
    }

    [Fact]
    public void Round_trip_preserves_scalar_header_fields()
    {
        var roster = RosterEntity.From(UserId, SampleRoster()).ToRoster();

        Assert.Equal("abc123", roster.Id);
        Assert.Equal("Dynasty Vanguard", roster.Name);
        Assert.Equal(Roster.NecronsFaction, roster.Faction);
        Assert.Equal(2000, roster.PointsLimit);
        Assert.Equal("hand-of-the-dynasty", roster.DetachmentId);
        Assert.Equal("2024.1", roster.CatalogueVersion);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), roster.CreatedUtc);
    }

    [Fact]
    public void Round_trip_preserves_units_through_json_column()
    {
        var roster = RosterEntity.From(UserId, SampleRoster()).ToRoster();

        Assert.Equal(3, roster.Units.Count);

        var overlord = roster.Units[0];
        Assert.Equal("overlord", overlord.DatasheetId);
        Assert.True(overlord.IsWarlord);
        Assert.Equal("dynastic-heirloom", overlord.AssignedEnhancementId);

        var warriors = roster.Units[1];
        Assert.Equal(10, warriors.ModelCount);
        Assert.Equal("u1", warriors.AttachedToRosterUnitId);
        var group = Assert.Single(warriors.Wargear);
        Assert.Equal("g1", group.GroupId);
        Assert.Equal(["o1", "o2"], group.OptionIds);

        var nightbringer = roster.Units[2];
        Assert.Equal("Quantum Goad", nightbringer.AppliedBindingId);
        Assert.Equal(45, nightbringer.BindingSurcharge);
    }

    [Fact]
    public void To_roster_falls_back_to_empty_units_for_blank_json()
    {
        var entity = new RosterEntity { RowKey = "x", Name = "Empty", Faction = "Necrons", UnitsJson = "" };

        Assert.Empty(entity.ToRoster().Units);
    }

    [Fact]
    public void Modified_utc_uses_timestamp_when_present()
    {
        var stamp = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero);
        var entity = RosterEntity.From(UserId, SampleRoster());
        entity.Timestamp = stamp;

        Assert.Equal(stamp, entity.ToRoster().ModifiedUtc);
    }

    [Fact]
    public void Round_trip_preserves_ability_schedules_through_json_column()
    {
        var roster = RosterEntity.From(UserId, SampleRoster()).ToRoster();

        var schedule = Assert.Single(roster.AbilitySchedules);
        Assert.Equal(AbilityScheduleKeys.ForUnitAbility("overlord", "My Will Be Done"), schedule.Key);
        Assert.True(schedule.ApplyToUnit);
        Assert.True(schedule.Covers(BattlePhase.Command, BattleTurn.Player));
        Assert.False(schedule.Covers(BattlePhase.Command, BattleTurn.Opponent));
    }

    [Fact]
    public void To_roster_falls_back_to_empty_schedules_for_blank_json()
    {
        var entity = new RosterEntity { RowKey = "x", Name = "Empty", Faction = "Necrons", AbilitySchedulesJson = "" };

        Assert.Empty(entity.ToRoster().AbilitySchedules);
    }
}
