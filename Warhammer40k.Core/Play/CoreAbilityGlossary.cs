namespace Warhammer40k.Core.Play;

/// <summary>
/// Concise, built-in descriptions for the unit-level <b>core</b> abilities that appear in a datasheet's
/// <c>factionRules</c> with no rules text of their own (e.g. "Deep Strike", "Stealth"). Used to fill the tap
/// popup for the chips those rules surface. Keyed by the rule's base name; a valued token like "Scouts 8\""
/// or "Deadly Demise D3" resolves on its leading words.
/// </summary>
public static class CoreAbilityGlossary
{
    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Deep Strike"] =
            "During deployment you can place this unit in Reserves instead of on the battlefield. From the " +
            "second battle round it can arrive, set up anywhere wholly within 6\" of a battlefield edge and " +
            "more than 9\" horizontally from all enemy models.",
        ["Deadly Demise"] =
            "When this model is destroyed, roll a D6 (before removing it). On a 6 each unit within 6\" suffers " +
            "the listed number of mortal wounds.",
        ["Fights First"] =
            "This unit fights first in the Fight phase, before units that do not have this ability.",
        ["Infiltrators"] =
            "During deployment this unit can be set up anywhere on the battlefield more than 9\" horizontally " +
            "from the enemy deployment zone and all enemy models.",
        ["Lone Operative"] =
            "This model can only be targeted by an attack if the attacking model is within 12\", unless this " +
            "model is leading a unit.",
        ["Scouts"] =
            "At the start of the first battle round, before the first turn, this unit can make a Normal move of " +
            "up to the listed distance (it must end more than 9\" from all enemy models).",
        ["Stealth"] =
            "If every model in this unit has Stealth, ranged attacks targeting it suffer -1 to the Hit roll.",
        ["Titanic Walker"] =
            "This Titanic model can move over other models and terrain freely, treating them as though they " +
            "were not there.",
    };

    /// <summary>
    /// A concise description for a <c>factionRules</c> core-ability token (matched on its leading words, so
    /// "Scouts 8\"" and "Deadly Demise D3" resolve), or an empty string when none is known.
    /// </summary>
    public static string Describe(string factionRule)
    {
        if (string.IsNullOrWhiteSpace(factionRule))
            return string.Empty;
        foreach (var entry in Descriptions)
            if (factionRule.StartsWith(entry.Key, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        return string.Empty;
    }
}
