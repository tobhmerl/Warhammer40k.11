using System.Globalization;
using System.Text.RegularExpressions;

namespace Warhammer40k.Core.Catalogue;

/// <summary>
/// Pure parser that turns a Leader's "While this model is leading a unit, …" abilities into structured
/// <see cref="ConferredEffect"/>s, so Play Mode can apply them to the led unit's card (a weapon chip, a unit
/// ability, or a recomputed stat) instead of showing the raw prose. Run once at load by
/// <see cref="CatalogueSeedLoader"/>; anything it doesn't confidently recognise yields no effect, so the
/// original ability text is preserved.
/// </summary>
public static partial class LeaderConferralParser
{
    /// <summary>Parses every conferring ability in <paramref name="abilities"/> (skips non-conferring ones).</summary>
    public static List<ConferredEffect> Parse(IEnumerable<Ability> abilities)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        var result = new List<ConferredEffect>();
        foreach (var ability in abilities)
        {
            var effect = ParseAbility(ability);
            if (effect is not null)
                result.Add(effect);
        }
        return result;
    }

    /// <summary>
    /// Parses a single ability into the effect it confers on the led unit, or <c>null</c> when the ability
    /// confers nothing recognisable (e.g. a CP-granting rule, which should stay as text).
    /// </summary>
    public static ConferredEffect? ParseAbility(Ability ability)
    {
        ArgumentNullException.ThrowIfNull(ability);
        var text = ability.Text ?? "";
        if (!text.Contains("leading a unit", StringComparison.OrdinalIgnoreCase))
            return null;

        var effect = new ConferredEffect { SourceAbility = ability.Name };

        // 1) Weapon-ability grant: "[melee|ranged] weapons equipped by models in that unit have the [X] ability".
        var weapon = WeaponGrantRegex().Match(text);
        if (weapon.Success)
        {
            effect.WeaponClass = ParseClass(weapon.Groups["class"].Value);
            effect.WeaponAbilities = ExtractAbilities(weapon.Groups["abil"].Value);
            return effect.IsEmpty ? null : effect;
        }

        // 2) Unit-ability grant: "models in that unit have the X ability" (e.g. Feel No Pain 5+).
        if (!text.Contains("weapons equipped by", StringComparison.OrdinalIgnoreCase))
        {
            var unit = UnitGrantRegex().Match(text);
            if (unit.Success)
            {
                effect.UnitAbilities = ExtractAbilities(unit.Groups["abil"].Value);
                if (!effect.IsEmpty)
                    return effect;
            }
        }

        // 3) Numeric buffs: "add N to the Hit roll" / "add N to the <stat> characteristic".
        effect.StatModifiers = ParseStatModifiers(text);

        // 4) Critical-hit threshold: "each time a model in that unit makes a [ranged|melee] attack, a
        //    successful unmodified Hit roll of N+ scores a Critical Hit" (an absent class = both).
        var crit = CritHitRegex().Match(text);
        if (crit.Success && int.TryParse(crit.Groups["n"].Value, out var critOn) && critOn is >= 2 and <= 6)
        {
            effect.CriticalHitOn = critOn;
            effect.WeaponClass = ParseClass(crit.Groups["class"].Value);
        }

        return effect.IsEmpty ? null : effect;
    }

    private static List<StatModifier> ParseStatModifiers(string text)
    {
        var mods = new List<StatModifier>();

        foreach (Match m in HitRollRegex().Matches(text))
        {
            if (int.TryParse(m.Groups["n"].Value, out var delta) && delta != 0)
                mods.Add(new StatModifier { Target = StatTarget.Skill, Delta = delta, WeaponClass = WeaponClass.Any });
        }

        foreach (Match m in CharacteristicRegex().Matches(text))
        {
            if (!int.TryParse(m.Groups["n"].Value, out var delta) || delta == 0)
                continue;
            var target = MapStat(m.Groups["stat"].Value);
            if (target is { } t)
                mods.Add(new StatModifier { Target = t, Delta = delta });
        }

        return mods;
    }

    private static WeaponClass ParseClass(string token) => token.ToLowerInvariant() switch
    {
        "melee" => WeaponClass.Melee,
        "ranged" => WeaponClass.Ranged,
        _ => WeaponClass.Any,
    };

    private static StatTarget? MapStat(string token)
    {
        var key = Regex.Replace(token.Trim().ToLowerInvariant(), @"\s+", " ");
        return key switch
        {
            "move" => StatTarget.Move,
            "toughness" => StatTarget.Toughness,
            "save" or "saves" => StatTarget.Save,
            "wound" or "wounds" => StatTarget.Wounds,
            "leadership" => StatTarget.Leadership,
            "objective control" => StatTarget.ObjectiveControl,
            "attack" or "attacks" => StatTarget.Attacks,
            "strength" => StatTarget.Strength,
            "damage" => StatTarget.Damage,
            _ => null,
        };
    }

    private static List<string> ExtractAbilities(string raw)
    {
        var result = new List<string>();
        var brackets = BracketRegex().Matches(raw);
        if (brackets.Count > 0)
        {
            foreach (Match b in brackets)
                AddAbility(result, b.Groups[1].Value);
        }
        else
        {
            AddAbility(result, raw);
        }
        return result;
    }

    private static void AddAbility(List<string> list, string token)
    {
        var normalized = NormalizeAbility(token);
        if (normalized.Length > 0 && !list.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            list.Add(normalized);
    }

    // Strips brackets/trailing punctuation and title-cases ALL-CAPS names ("LETHAL HITS" → "Lethal Hits") so a
    // granted ability reads like the weapon keywords it sits beside, while leaving mixed-case names untouched.
    private static string NormalizeAbility(string token)
    {
        var value = token.Trim().Trim('[', ']').Trim().TrimEnd('.').Trim();
        if (value.Length == 0)
            return value;

        var letters = value.Where(char.IsLetter).ToList();
        if (letters.Count > 0 && letters.All(char.IsUpper))
            value = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        return value;
    }

    [GeneratedRegex(@"leading a unit,?\s*(?:(?<class>melee|ranged)\s+)?weapons\s+equipped\s+by\s+models\s+in\s+(?:that|this)\s+unit\s+have\s+the\s+(?<abil>.+?)\s+abilit(?:y|ies)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex WeaponGrantRegex();

    [GeneratedRegex(@"leading a unit,?\s*models\s+in\s+(?:that|this)\s+unit\s+have\s+the\s+(?<abil>.+?)\s+abilit(?:y|ies)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex UnitGrantRegex();

    [GeneratedRegex(@"(?:add\s+|gain\s+|\+)(?<n>\d+)\s+to\s+(?:the\s+)?hit\s+rolls?", RegexOptions.IgnoreCase)]
    private static partial Regex HitRollRegex();

    [GeneratedRegex(@"(?:add\s+|\+)(?<n>\d+)\s+to\s+(?:the\s+|its\s+|their\s+)?(?<stat>move|toughness|strength|attacks?|wounds?|leadership|saves?|damage|objective\s+control)\s+characteristics?",
        RegexOptions.IgnoreCase)]
    private static partial Regex CharacteristicRegex();

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex BracketRegex();

    // "each time a model in that unit makes a [ranged|melee] attack, … Hit roll of N+ scores a Critical Hit".
    // The class token is optional (absent ⇒ both), and the wording between "attack" and "Hit roll" varies
    // (incl. the seed's "unmodifed" typo), so it is matched leniently.
    [GeneratedRegex(@"each\s+time\s+a\s+model\s+in\s+(?:that|this)\s+unit\s+makes\s+(?:a|an)\s+(?:(?<class>ranged|melee)\s+)?attack\b.*?\bhit\s+roll\s+of\s+(?<n>\d)\+\s+scores\s+a\s+critical\s+hit",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CritHitRegex();
}
