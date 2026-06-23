using System.Text.RegularExpressions;
using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Play;

/// <summary>
/// Pure heuristics that map catalogue content to the battle phase(s) it matters in, so Play Mode can
/// surface the right weapons and abilities at the right time. Weapon mapping is exact (Ranged → Shooting,
/// Melee → Fight); ability mapping scans the rules text for phase cues. An ability that matches no cue is
/// treated as passive/always-on (<see cref="Classify"/> returns an empty set).
/// </summary>
public static class PhaseClassifier
{
    // Cues are matched at a left word boundary (suffixes allowed), so "move" never matches inside
    // "remove" and "charge" never matches inside "discharge", while "shoot" still matches "shooting".
    private static readonly (BattlePhase Phase, string[] Cues)[] AbilityCues =
    {
        (BattlePhase.Command, new[] { "command phase", "battle-shock", "battle shock", "reanimation protocol" }),
        (BattlePhase.Movement, new[] { "movement phase", "fall back", "falls back", "advance", "remain stationary", "normal move" }),
        (BattlePhase.Shooting, new[] { "shooting phase", "shoot", "ranged" }),
        (BattlePhase.Charge, new[] { "charge" }),
        (BattlePhase.Fight, new[] { "fight phase", "fights first", "fight first", "pile in", "consolidate", "melee" }),
    };

    private static readonly Regex InvulnRegex =
        new(@"(\d\+)\s+invulnerable save", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FeelNoPainRegex =
        new(@"feel no pain\s*\(?\s*(\d\+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The phase a weapon is used in: Melee weapons fight, everything else shoots.</summary>
    public static BattlePhase PhaseForWeapon(WeaponProfile weapon) =>
        weapon.Type.Equals("Melee", StringComparison.OrdinalIgnoreCase)
            ? BattlePhase.Fight
            : BattlePhase.Shooting;

    /// <summary>
    /// Returns the phases an ability is relevant to (by name + text). An empty set means the ability is
    /// passive/always-on (e.g. an invulnerable save or a damage-reduction rule).
    /// </summary>
    public static IReadOnlySet<BattlePhase> Classify(Ability ability)
    {
        var haystack = (ability.Name + " " + ability.Text).ToLowerInvariant();
        var phases = new HashSet<BattlePhase>();
        foreach (var (phase, cues) in AbilityCues)
        {
            foreach (var cue in cues)
            {
                if (HasCue(haystack, cue))
                {
                    phases.Add(phase);
                    break;
                }
            }
        }
        return phases;
    }

    /// <summary>True when an ability matches no phase cue and is therefore always-on / passive.</summary>
    public static bool IsPassive(Ability ability) => Classify(ability).Count == 0;

    /// <summary>
    /// Reads the turn a "<c>… phase</c>" mention is scoped to from the qualifier right before it: "your
    /// opponent's <i>X</i> phase" → <see cref="StratagemTurn.Opponent"/>, "your <i>X</i> phase" →
    /// <see cref="StratagemTurn.Your"/>, an unqualified "the <i>X</i> phase" → <see cref="StratagemTurn.Either"/>
    /// (it happens in both turns — the Fight phase being the classic case). Returns <c>null</c> when the phase
    /// is not named in the text (so callers can fall back to an authored value).
    /// </summary>
    public static StratagemTurn? TurnForPhase(string text, BattlePhase phase)
    {
        var needle = phase switch
        {
            BattlePhase.Command => "command phase",
            BattlePhase.Movement => "movement phase",
            BattlePhase.Shooting => "shooting phase",
            BattlePhase.Charge => "charge phase",
            BattlePhase.Fight => "fight phase",
            _ => null,
        };
        if (needle is null || string.IsNullOrEmpty(text))
            return null;

        var lower = text.ToLowerInvariant();
        var idx = lower.IndexOf(needle, StringComparison.Ordinal);
        if (idx < 0)
            return null;

        // Look only at the words immediately before the phase name, so "or the Fight phase" stays Either even
        // when an earlier clause said "your opponent's Shooting phase".
        var start = Math.Max(0, idx - 24);
        var prefix = lower.Substring(start, idx - start);
        if (prefix.Contains("opponent", StringComparison.Ordinal))
            return StratagemTurn.Opponent;
        if (prefix.Contains("your", StringComparison.Ordinal))
            return StratagemTurn.Your;
        return StratagemTurn.Either;
    }

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

    // Matches a cue only where it begins at a word boundary; trailing letters are allowed so a single
    // stem ("shoot") covers its inflections ("shoots", "shooting") without matching unrelated words.
    private static bool HasCue(string text, string cue)
    {
        var idx = 0;
        while ((idx = text.IndexOf(cue, idx, StringComparison.Ordinal)) >= 0)
        {
            var before = idx > 0 ? text[idx - 1] : ' ';
            if (!char.IsLetterOrDigit(before))
                return true;
            idx += cue.Length;
        }
        return false;
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
