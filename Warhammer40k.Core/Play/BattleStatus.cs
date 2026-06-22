namespace Warhammer40k.Core.Play;

/// <summary>
/// Pure helpers for the in-game status cues shown in Play Mode's Command phase: which units must run
/// <b>Reanimation Protocols</b> (they have lost models) and which must take a <b>Battle-shock</b> test
/// (they are Below Half-strength). Model-count based — single-model units use a wounds threshold that
/// Play Mode does not track numerically, so they are not flagged here.
/// </summary>
public static class BattleStatus
{
    /// <summary>
    /// True when a multi-model unit is <b>Below Half-strength</b>: fewer than half its starting models
    /// remain (e.g. 5 of 11 → below half). A destroyed unit (0 models) is not testing; single-model
    /// units (start ≤ 1) are wounds-based and excluded.
    /// </summary>
    public static bool BelowHalfStrength(int startingModels, int currentModels) =>
        startingModels > 1 && currentModels > 0 && currentModels * 2 < startingModels;

    /// <summary>
    /// True when a unit has lost models but is not destroyed — i.e. it should run Reanimation Protocols
    /// in your Command phase to bring models back.
    /// </summary>
    public static bool HasLosses(int startingModels, int currentModels) =>
        currentModels > 0 && currentModels < startingModels;
}
