using Warhammer40k.Core.Play;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins <see cref="BattleStatus"/>: the Command-phase cues for Reanimation Protocols (lost models) and
/// Battle-shock (Below Half-strength), including the worked 11→5 example.
/// </summary>
public class BattleStatusTests
{
    [Theory]
    [InlineData(11, 5, true)]   // 5 of 11 → below half (worked example)
    [InlineData(11, 6, false)]  // 6 of 11 → exactly above half
    [InlineData(10, 5, false)]  // exactly half is NOT below half
    [InlineData(10, 4, true)]   // 4 of 10 → below half
    [InlineData(11, 0, false)]  // destroyed unit does not test
    [InlineData(1, 0, false)]   // single-model unit: wounds-based, not flagged
    [InlineData(1, 1, false)]
    [InlineData(20, 20, false)] // full strength
    public void BelowHalfStrength_flags_units_under_half_their_starting_models(int start, int current, bool expected) =>
        Assert.Equal(expected, BattleStatus.BelowHalfStrength(start, current));

    [Theory]
    [InlineData(11, 5, true)]   // lost models, still alive → reanimate
    [InlineData(11, 11, false)] // full strength → nothing to reanimate
    [InlineData(11, 0, false)]  // destroyed → cannot reanimate
    [InlineData(1, 1, false)]
    public void HasLosses_flags_units_that_lost_models_but_are_not_destroyed(int start, int current, bool expected) =>
        Assert.Equal(expected, BattleStatus.HasLosses(start, current));
}
