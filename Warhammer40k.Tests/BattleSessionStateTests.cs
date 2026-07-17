using System.Text.Json;
using Warhammer40k.Core.Play;

namespace Warhammer40k.Tests;

public class BattleSessionStateTests
{
    [Theory]
    [InlineData(BattlePhase.Command, BattlePhase.Movement)]
    [InlineData(BattlePhase.Movement, BattlePhase.Shooting)]
    [InlineData(BattlePhase.Shooting, BattlePhase.Charge)]
    [InlineData(BattlePhase.Charge, BattlePhase.Fight)]
    [InlineData(BattlePhase.Fight, BattlePhase.Command)]
    public void Next_phase_follows_battle_order(BattlePhase current, BattlePhase expected) =>
        Assert.Equal(expected, BattlePhases.Next(current));

    [Fact]
    public void Sequence_moves_to_second_player_then_next_round()
    {
        var secondTurn = BattleSequence.Next(2, BattleTurn.Player, BattlePhase.Fight);
        var nextRound = BattleSequence.Next(secondTurn.Round, secondTurn.Turn, BattlePhase.Fight);

        Assert.Equal(new BattlePosition(2, BattleTurn.Opponent, BattlePhase.Command), secondTurn);
        Assert.Equal(new BattlePosition(3, BattleTurn.Player, BattlePhase.Command), nextRound);
    }

    [Fact]
    public void Sequence_respects_opponent_going_first()
    {
        var secondTurn = BattleSequence.Next(1, BattleTurn.Opponent, BattlePhase.Fight, BattleTurn.Opponent);
        var nextRound = BattleSequence.Next(1, BattleTurn.Player, BattlePhase.Fight, BattleTurn.Opponent);

        Assert.Equal(new BattlePosition(1, BattleTurn.Player, BattlePhase.Command), secondTurn);
        Assert.Equal(new BattlePosition(2, BattleTurn.Opponent, BattlePhase.Command), nextRound);
    }

    [Fact]
    public void State_round_trips_all_live_battle_values()
    {
        var state = new BattleSessionState
        {
            Round = 3,
            FirstTurn = BattleTurn.Opponent,
            Turn = BattleTurn.Player,
            Phase = BattlePhase.Shooting,
            CommandPoints = 4,
            YourPrimaryVp = 12,
            YourSecondaryVp = 7,
            OpponentPrimaryVp = 9,
            OpponentSecondaryVp = 3,
            FocusMode = true,
            ActiveUnitId = "warriors-1",
            PartTracks = new() { ["warriors-1"] = 14 },
            WeaponKills = new() { ["warriors-1|gauss flayer"] = 2 },
            ActiveEffects = ["warriors-1|relentless"],
            ShootingChoices = new() { ["warriors-1"] = "Lethal Hits" },
        };

        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<BattleSessionState>(json);

        Assert.NotNull(restored);
        Assert.Equal(3, restored.Round);
        Assert.Equal(BattleTurn.Opponent, restored.FirstTurn);
        Assert.Equal(BattlePhase.Shooting, restored.Phase);
        Assert.Equal(4, restored.CommandPoints);
        Assert.Equal(12, restored.YourPrimaryVp);
        Assert.Equal("warriors-1", restored.ActiveUnitId);
        Assert.Equal(14, restored.PartTracks["warriors-1"]);
        Assert.Equal(2, restored.WeaponKills["warriors-1|gauss flayer"]);
        Assert.Contains("warriors-1|relentless", restored.ActiveEffects);
        Assert.Equal("Lethal Hits", restored.ShootingChoices["warriors-1"]);
    }

    [Fact]
    public void Normalize_clamps_untrusted_browser_values()
    {
        var state = new BattleSessionState
        {
            Version = -1,
            Round = -2,
            Turn = (BattleTurn)99,
            FirstTurn = (BattleTurn)99,
            Phase = BattlePhase.Any,
            CommandPoints = -4,
            YourPrimaryVp = 2000,
        };

        state.Normalize();

        Assert.Equal(BattleSessionState.CurrentVersion, state.Version);
        Assert.Equal(1, state.Round);
        Assert.Equal(BattleTurn.Player, state.Turn);
        Assert.Equal(BattleTurn.Player, state.FirstTurn);
        Assert.Equal(BattlePhase.Command, state.Phase);
        Assert.Equal(0, state.CommandPoints);
        Assert.Equal(999, state.YourPrimaryVp);
    }
}
