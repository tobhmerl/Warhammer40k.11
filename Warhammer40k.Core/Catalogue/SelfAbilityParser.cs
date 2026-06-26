using System.Globalization;
using System.Text.RegularExpressions;

namespace Warhammer40k.Core.Catalogue;

/// <summary>
/// Pure parser that turns a model's <b>self-affecting</b> datasheet abilities into structured
/// <see cref="ConferredEffect"/>s, so Play Mode can rewrite the bearer's own statline / weapon chips instead
/// of showing the raw prose. Two patterns are recognised, both permanent and unconditional:
/// <list type="bullet">
/// <item>"The bearer has a Save characteristic of 3+ and a Move characteristic of 8\"" → absolute stat sets
/// (a <see cref="StatModifier.SetValue"/> per characteristic).</item>
/// <item>"Ranged weapons equipped by the bearer have the [IGNORES COVER] ability" → a self weapon-ability
/// grant.</item>
/// </list>
/// Anything conditional ("until the end of …", "once per battle"), a leader conferral ("leading a unit"), or
/// otherwise unrecognised yields no effect, so the original ability text is preserved. Run once at load by
/// <see cref="CatalogueSeedLoader"/>.
/// </summary>
public static partial class SelfAbilityParser
{
    /// <summary>Parses every self-affecting ability in <paramref name="abilities"/> (skips the rest).</summary>
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
    /// Parses a single ability into the permanent effect it has on the bearer, or <c>null</c> when it has no
    /// recognisable permanent self-effect (so it stays as text).
    /// </summary>
    public static ConferredEffect? ParseAbility(Ability ability)
    {
        ArgumentNullException.ThrowIfNull(ability);
        var text = NormalizeWhitespace(ability.Text ?? "");
        if (text.Length == 0)
            return null;

        // Skip leader conferrals (handled elsewhere) and anything temporary/conditional.
        if (text.Contains("leading a unit", StringComparison.OrdinalIgnoreCase))
            return null;
        if (text.Contains("until the end", StringComparison.OrdinalIgnoreCase)
            || text.Contains("once per", StringComparison.OrdinalIgnoreCase)
            || text.Contains("at the start", StringComparison.OrdinalIgnoreCase)
            || text.Contains("when this unit", StringComparison.OrdinalIgnoreCase)
            || text.Contains("in your", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var effect = new ConferredEffect { SourceAbility = ability.Name };

        // 1) Self weapon-ability grant: "[ranged|melee] weapons equipped by the bearer / models in this unit
        //    have the [X] ability".
        var weapon = SelfWeaponGrantRegex().Match(text);
        if (weapon.Success)
        {
            effect.WeaponClass = ParseClass(weapon.Groups["class"].Value);
            effect.WeaponAbilities = ExtractAbilities(weapon.Groups["abil"].Value);
            return effect.IsEmpty ? null : effect;
        }

        // 2) Absolute stat-set: "has a/an <Stat> characteristic of <value>" (one or more, e.g. Save + Move).
        effect.StatModifiers = ParseStatSets(text);
        return effect.IsEmpty ? null : effect;
    }

    private static List<StatModifier> ParseStatSets(string text)
    {
        var mods = new List<StatModifier>();
        foreach (Match m in StatSetRegex().Matches(text))
        {
            var target = MapStat(m.Groups["stat"].Value);
            var value = m.Groups["val"].Value.Trim();
            if (target is { } t && value.Length > 0)
                mods.Add(new StatModifier { Target = t, SetValue = value });
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
        var value = token.Trim().Trim('[', ']').Trim().TrimEnd('.').Trim();
        if (value.Length == 0)
            return;
        var letters = value.Where(char.IsLetter).ToList();
        if (letters.Count > 0 && letters.All(char.IsUpper))
            value = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
            list.Add(value);
    }

    // Collapse non-breaking spaces / newlines so the patterns match regardless of how the seed text wraps.
    private static string NormalizeWhitespace(string text) =>
        Regex.Replace(text.Replace('\u00A0', ' '), @"\s+", " ").Trim();

    [GeneratedRegex(@"(?:(?<class>melee|ranged)\s+)?weapons\s+equipped\s+by\s+(?:the\s+bearer|models\s+in\s+(?:this|the\s+bearer's)\s+unit)\s+have\s+the\s+(?<abil>.+?)\s+abilit(?:y|ies)",
        RegexOptions.IgnoreCase)]
    private static partial Regex SelfWeaponGrantRegex();

    [GeneratedRegex(@"(?:a|an)\s+(?<stat>move|toughness|save|wounds?|leadership|objective\s+control)\s+characteristic\s+of\s+(?<val>\d+\+|\d+""|\d+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex StatSetRegex();

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex BracketRegex();
}
