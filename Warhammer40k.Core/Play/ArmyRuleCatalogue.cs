namespace Warhammer40k.Core.Play;

/// <summary>
/// The faction-wide <b>Army Rules</b> (the first-level rules), authored verbatim from the 11th-edition
/// rulebook and shown in Play Mode. Pure reference data, like <see cref="CoreStratagemCatalogue"/> — no
/// API/storage. <see cref="ForFaction"/> selects the rules for a roster's faction (this app is Necron-only).
/// </summary>
public static class ArmyRuleCatalogue
{
    /// <summary>Every Army Rule across factions (currently Necrons only).</summary>
    public static readonly IReadOnlyList<ArmyRule> All =
    [
        new ArmyRule
        {
            Name = "Reanimation Protocols",
            Faction = Rosters.Roster.NecronsFaction,
            Phases = [BattlePhase.Command],
            Text = "If your Army Faction is NECRONS, at the end of your Command phase, each unit from your army with this ability that is on the battlefield activates its Reanimation Protocols and reanimates D3 wounds. Each time such a unit reanimates a wound:\n" +
                   "• If that unit contains one or more models with fewer than their starting number of wounds remaining, select one of those models; that model regains one lost wound.\n" +
                   "• If all models in that unit have their starting number of wounds, but that unit is not at its Starting Strength, one destroyed model is returned to that unit with one wound remaining.\n" +
                   "\n" +
                   "Once such a unit is at its Starting Strength and all of its models have their starting number of wounds, nothing further happens.",
            Example = "A unit of Lokhust Destroyers (which have a Wounds characteristic of 3) activates its Reanimation Protocols. The unit had a Starting Strength of 3, but currently contains 2 models, and one of those models has lost 1 wound. A 3 is rolled to see how many wounds are reanimated. The first of these reanimated wounds restores the wounded Lokhust Destroyer back to 3 wounds. The second of these reanimated wounds returns the destroyed Lokhust Destroyer to the battlefield with 1 wound remaining. The third of these reanimated wounds restores one of the remaining lost wounds to the same Lokhust Destroyer that was just returned. The unit now contains 3 models, two of which have 3 wounds remaining and one of which has 2 wounds remaining.",
        },
    ];

    /// <summary>The Army Rules for the given faction, in authored order.</summary>
    public static IReadOnlyList<ArmyRule> ForFaction(string faction) =>
        All.Where(r => string.Equals(r.Faction, faction, StringComparison.OrdinalIgnoreCase)).ToList();
}
