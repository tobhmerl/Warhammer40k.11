using System.Text.RegularExpressions;
using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Play;

/// <summary>
/// Pure parsing helpers for catalogue content used by Play Mode: the exact weapon→phase mapping
/// (Ranged → Shooting, Melee → Fight) and the invulnerable / Feel No Pain save parsing shown as chips.
/// Ability phase/turn timing is configured manually per ability (see <see cref="AbilitySchedule"/>), not
/// guessed from text.
/// </summary>
public static class PhaseClassifier
{
    private static readonly Regex InvulnRegex =
        new(@"(\d\+)\s+invulnerable save", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FeelNoPainRegex =
        new(@"feel no pain\s*\(?\s*(\d\+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The phase a weapon is used in: Melee weapons fight, everything else shoots.</summary>
    public static BattlePhase PhaseForWeapon(WeaponProfile weapon) =>
        weapon.Type.Equals("Melee", StringComparison.OrdinalIgnoreCase)
            ? BattlePhase.Fight
            : BattlePhase.Shooting;

    /// <summary>Parses the unit's invulnerable save (e.g. "4+") from its abilities, or null when it has none.</summary>
    public static string? InvulnerableSave(IEnumerable<Ability> abilities)
    {
        foreach (var ability in abilities)
        {
            var match = InvulnRegex.Match(ability.Text);
            if (match.Success)
                return match.Groups[1].Value;
        }
        return null;
    }

    /// <summary>
    /// Parses a single ability's invulnerable save with its <see cref="SaveScope"/> (whole unit vs a single
    /// model), or null when the ability grants none. A leader's "while leading a unit, models in that unit
    /// have a N+ invulnerable save" reads as <see cref="SaveScope.Unit"/>.
    /// </summary>
    public static (string Value, SaveScope Scope)? InvulnerableSaveScoped(Ability ability)
    {
        var match = InvulnRegex.Match(ability.Text);
        return match.Success ? (match.Groups[1].Value, ScopeOf(ability.Text)) : null;
    }

    /// <summary>Parses a single ability's Feel No Pain value with its <see cref="SaveScope"/>, or null when none.</summary>
    public static (string Value, SaveScope Scope)? FeelNoPainScoped(Ability ability)
    {
        var match = FeelNoPainRegex.Match(ability.Name + " " + ability.Text);
        return match.Success ? (match.Groups[1].Value, ScopeOf(ability.Text)) : null;
    }

    /// <summary>
    /// True when an ability is a model's / unit's <b>own</b> always-on save rule (e.g. "This model has a 4+
    /// invulnerable save" or "Models in this unit have a 5+ invulnerable save") rather than a conditional
    /// ability that <i>confers</i> a save (e.g. a Leader's "While this model is leading a unit, …"). Own save
    /// rules are surfaced as an always-on chip and are not schedulable; conferral abilities are listed in setup
    /// and applied manually.
    /// </summary>
    public static bool IsOwnSaveRule(Ability ability)
    {
        if (InvulnerableSaveScoped(ability) is null && FeelNoPainScoped(ability) is null)
            return false;
        return !(ability.Text ?? "").Contains("leading a unit", StringComparison.OrdinalIgnoreCase);
    }

    // A save reads as unit-wide when its text talks about "(models in) this/that unit"; otherwise it's a
    // single model's save (e.g. a Character's own "this model has a 4+ invulnerable save").
    private static SaveScope ScopeOf(string text) =>
        text.Contains("this unit", StringComparison.OrdinalIgnoreCase)
        || text.Contains("that unit", StringComparison.OrdinalIgnoreCase)
        || text.Contains("models in", StringComparison.OrdinalIgnoreCase)
            ? SaveScope.Unit
            : SaveScope.Model;

    /// <summary>Parses the unit's Feel No Pain value (e.g. "5+") from its ability name/text, or null when it has none.</summary>
    public static string? FeelNoPain(IEnumerable<Ability> abilities)
    {
        foreach (var ability in abilities)
        {
            var match = FeelNoPainRegex.Match(ability.Name + " " + ability.Text);
            if (match.Success)
                return match.Groups[1].Value;
        }
        return null;
    }
}

/// <summary>Whether a parsed save (invulnerable / Feel No Pain) applies to a whole unit or a single model.</summary>
public enum SaveScope
{
    /// <summary>Applies to every model in the unit (incl. a leader's conferral on the led unit).</summary>
    Unit = 0,

    /// <summary>Applies to a single model only (e.g. a Character's own invulnerable save).</summary>
    Model = 1,
}
