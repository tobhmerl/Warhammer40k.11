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
