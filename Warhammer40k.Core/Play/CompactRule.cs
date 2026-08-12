using System.Text.RegularExpressions;

namespace Warhammer40k.Core.Play;

/// <summary>
/// Turns a rule's printed text into the tabletop shorthand a player needs mid-game ("SH1: Destroyer Cult or
/// closest target", "Re-roll one Hit, Wound, Damage or Save", "+1 to Wound"). It is a <b>presentation</b>
/// layer only: the printed name and text stay the source of truth and are never mutated.
/// </summary>
/// <remarks>
/// Deterministic and local by design — no service call, no guessing. The text is matched against an ordered
/// set of effect patterns drawn from how 40k rules are actually written; whatever matches is rendered in
/// standard shorthand (FNP, SH, LH, Inv, CP, MW, Dmg, Ld, OC) and trimmed to a short word budget. A rule that
/// matches nothing returns <c>null</c>, and the caller falls back to the rule's name: a missing summary is
/// always better than a wrong one.
/// </remarks>
public static class CompactRule
{
    /// <summary>The word budget a summary aims for; a single trailing condition may push it slightly over.</summary>
    private const int MaxWords = 11;

    /// <summary>The width budget: beyond this a summary would wrap past two lines on a NOW card.</summary>
    private const int MaxChars = 46;

    /// <summary>
    /// The compact description of <paramref name="text"/>, or null when nothing could be summarised safely.
    /// </summary>
    public static string? Summarize(string? text)
    {
        var clean = Normalize(text);
        if (clean.Length == 0)
            return null;

        var effects = new List<string>();
        foreach (var matcher in Matchers)
        {
            if (matcher(clean) is not { } fragment || fragment.Length == 0)
                continue;
            if (!effects.Contains(fragment, StringComparer.OrdinalIgnoreCase))
                effects.Add(fragment);
            if (effects.Count == 2)
                break;
        }

        if (effects.Count == 0)
            return null;

        var summary = string.Join("; ", effects);
        if (Condition(clean) is { } condition)
            summary += ": " + condition;
        return Clamp(summary);
    }

    /// <summary>
    /// The compact description of a stratagem, read from its effect first and its restriction second, so the
    /// card shows what it does rather than what it is called.
    /// </summary>
    public static string? SummarizeStratagem(string? effect, string? restrictions) =>
        Summarize(effect) ?? Summarize(restrictions);

    // ---- Effect matchers --------------------------------------------------------------------------
    // Ordered by how decisive the effect is on the tabletop: granted weapon keywords and saves first,
    // then characteristic and roll modifiers, then permissions and prohibitions.
    private static readonly Func<string, string?>[] Matchers =
    [
        GrantedKeywords,
        FeelNoPain,
        InvulnerableSave,
        RollModifier,
        CharacteristicModifier,
        PrintedModifier,
        DamageReduction,
        Reroll,
        CriticalThreshold,
        MortalWounds,
        CommandPoints,
        ReturnModels,
        Movement,
        Prohibition,
        NamedRule,
    ];

    private static readonly Regex BracketRx = new(@"\[([^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex FnpRx = new(@"Feel No Pain\s*\(?(\d\+)\)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InvRx = new(@"(\d\+)\s+invulnerable save", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RollRx = new(@"(add|subtract)\s+(\d+)\s+(?:to|from)\s+(?:the\s+)?(Hit|Wound|Advance|Charge|Damage|Battle-shock|Saving throw|save)\s*rolls?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CharRx = new(@"(add|subtract)\s+(\d+)(?:""|\u201d)?\s+(?:to|from)\s+the\s+(Strength|Attacks|Move|Toughness|Objective Control|Leadership|Damage|Armour Penetration)\s+characteristic", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CritRx = new(@"Critical (Hit|Wound)s?\s*(?:on(?: an? unmodified)?)?\s*(\d\+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MwRx = new(@"(\d+|D3|D6|D3\+\d|D6\+\d)\s+mortal wounds?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CpRx = new(@"gains?\s+(\d+)\s*CP", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReturnRx = new(@"return\s+(?:up to\s+)?(\d+|D3|D6)\s+(?:destroyed\s+)?(?:models?|wounds?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReanimateRx = new(@"reanimates?\s+(\d+|D3|D6)(?:\+\d)?\s+wounds?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NormalMoveRx = new(@"Normal move of up to\s+(\d+)(?:""|\u201d)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "…that attack has the [SUSTAINED HITS 1] ability" → SH1 — the single most useful thing to see at a glance.
    private static string? GrantedKeywords(string text)
    {
        var keywords = BracketRx.Matches(text)
            .Select(m => Shorthand.Keyword(m.Groups[1].Value.Trim()))
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();
        return keywords.Count == 0 ? null : string.Join(" + ", keywords);
    }

    private static string? FeelNoPain(string text) =>
        FnpRx.Match(text) is { Success: true } m ? $"{m.Groups[1].Value} FNP" : null;

    private static string? InvulnerableSave(string text) =>
        InvRx.Match(text) is { Success: true } m ? $"{m.Groups[1].Value} Inv" : null;

    private static string? RollModifier(string text)
    {
        if (RollRx.Match(text) is not { Success: true } m)
            return null;
        var sign = m.Groups[1].Value.Equals("add", StringComparison.OrdinalIgnoreCase) ? "+" : "-";
        var stat = m.Groups[3].Value.Equals("save", StringComparison.OrdinalIgnoreCase)
                   || m.Groups[3].Value.StartsWith("Saving", StringComparison.OrdinalIgnoreCase)
            ? "Save"
            : Capitalize(m.Groups[3].Value);
        return $"{sign}{m.Groups[2].Value} {stat}";
    }

    private static string? CharacteristicModifier(string text)
    {
        if (CharRx.Match(text) is not { Success: true } m)
            return null;
        var sign = m.Groups[1].Value.Equals("add", StringComparison.OrdinalIgnoreCase) ? "+" : "-";
        var stat = Shorthand.Characteristic(m.Groups[3].Value);
        var inches = stat == "M" ? "\"" : "";
        return $"{sign}{m.Groups[2].Value}{inches} {stat}";
    }

    // Some rules already print the shorthand ("Your unit has +1 OC until the end of the turn").
    private static readonly Regex PrintedRx = new(@"([+-]\d+)\s*(OC|Ld|AP|CP|MW)\b", RegexOptions.Compiled);

    private static string? PrintedModifier(string text) =>
        PrintedRx.Match(text) is { Success: true } m ? $"{m.Groups[1].Value} {m.Groups[2].Value}" : null;

    // "subtract 1 from the Damage characteristic" is caught above; this covers the "reduce … by 1" wording.
    private static string? DamageReduction(string text) =>
        Regex.IsMatch(text, @"(reduce|reducing)[^.]{0,40}Damage[^.]{0,20}by\s+1", RegexOptions.IgnoreCase)
            ? "-1 Dmg"
            : null;

    private static string? Reroll(string text)
    {
        var match = Regex.Match(text, @"re-roll\s+(?:that\s+roll|the\s+)?([^.,;]{0,60})", RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;

        var tail = match.Groups[1].Value.Trim();
        // "re-roll a Hit roll of 1" keeps the qualifier; a bare "re-roll that roll" needs no tail at all.
        var what = Regex.Match(tail, @"^(?:an?\s+)?(Hit|Wound|Damage|Advance|Charge|Saving throw|save)\s*rolls?(\s+of\s+\d)?", RegexOptions.IgnoreCase);
        if (what.Success)
        {
            var stat = what.Groups[1].Value;
            var qualifier = what.Groups[2].Success ? what.Groups[2].Value.Trim() : "";
            return $"Re-roll {Capitalize(stat)}{(qualifier.Length > 0 ? " " + qualifier : "")}".Trim();
        }
        return "Re-roll";
    }

    private static string? CriticalThreshold(string text) =>
        CritRx.Match(text) is { Success: true } m
            ? $"Critical {Capitalize(m.Groups[1].Value)}s on {m.Groups[2].Value}"
            : null;

    private static string? MortalWounds(string text) =>
        MwRx.Match(text) is { Success: true } m ? $"{m.Groups[1].Value.ToUpperInvariant()} MW" : null;

    private static string? CommandPoints(string text) =>
        CpRx.Match(text) is { Success: true } m ? $"+{m.Groups[1].Value} CP" : null;

    private static string? ReturnModels(string text)
    {
        if (ReturnRx.Match(text) is { Success: true } r)
            return $"Return {r.Groups[1].Value.ToUpperInvariant()} models";
        return ReanimateRx.Match(text) is { Success: true } m
            ? $"Reanimate {m.Groups[1].Value.ToUpperInvariant()} wounds"
            : null;
    }

    private static string? Movement(string text)
    {
        if (Regex.IsMatch(text, @"can (?:both )?Fall Back and (?:still )?(?:make a )?[Cc]harge", RegexOptions.IgnoreCase))
            return "Fall Back and Charge";
        if (Regex.IsMatch(text, @"can (?:both )?Advance and (?:still )?(?:make a )?[Cc]harge", RegexOptions.IgnoreCase))
            return "Advance and Charge";
        if (Regex.IsMatch(text, @"can (?:both )?Fall Back and (?:still )?shoot", RegexOptions.IgnoreCase))
            return "Fall Back and shoot";
        if (NormalMoveRx.Match(text) is { Success: true } m)
            return $"Normal move {m.Groups[1].Value}\"";
        return null;
    }

    private static string? Prohibition(string text)
    {
        if (Regex.IsMatch(text, @"cannot (?:be selected to shoot|shoot) in Overwatch|cannot use.{0,20}Fire Overwatch", RegexOptions.IgnoreCase))
            return "Target cannot Overwatch";
        if (Regex.IsMatch(text, @"cannot be selected as the target", RegexOptions.IgnoreCase))
            return "Cannot be targeted";
        if (Regex.IsMatch(text, @"modifiers?[^.]{0,30}(?:cannot|are ignored|is ignored)|ignore(?:s|ing)? (?:all )?modifiers", RegexOptions.IgnoreCase))
            return "Ignore modifiers";
        return null;
    }

    // Rules that simply hand out a named core ability read best as that ability's name.
    private static readonly (string Pattern, string Label)[] NamedRules =
    [
        (@"has the Fights First ability", "Fights First"),
        (@"has the Stealth ability", "Stealth"),
        (@"has the Lone Operative ability", "Lone Operative"),
        (@"automatically (?:successful|passed)", "Auto-pass"),
        (@"is secured", "Secure objective"),
        (@"snap shooting", "Snap shooting"),
    ];

    private static string? NamedRule(string text) =>
        NamedRules.FirstOrDefault(r => Regex.IsMatch(text, r.Pattern, RegexOptions.IgnoreCase)).Label;

    // ---- The decisive condition -------------------------------------------------------------------
    // What the player actually has to check before applying the effect. Range and aura context are already
    // on the card, so only the choice-driving clause is kept.
    private static string? Condition(string text)
    {
        var keyword = Regex.Match(text, @"if that model has the\s+(.+?)\s+keyword", RegexOptions.IgnoreCase);
        var closest = Regex.IsMatch(text, @"closest eligible target", RegexOptions.IgnoreCase);
        if (keyword.Success)
        {
            var name = TitleCase(keyword.Groups[1].Value.Trim());
            return closest ? $"{name} or closest target" : name;
        }
        if (closest)
            return "closest target";
        if (Regex.IsMatch(text, @"within half range", RegexOptions.IgnoreCase))
            return "within half range";
        if (Regex.IsMatch(text, @"range of one or more objective markers", RegexOptions.IgnoreCase))
            return "on an objective";
        if (Regex.IsMatch(text, @"is Battle-shocked", RegexOptions.IgnoreCase))
            return "vs Battle-shocked";
        return null;
    }

    // ---- Text helpers ------------------------------------------------------------------------------

    // The seed emphasises keywords with "^^" / "**" wrappers; they are noise here.
    private static string Normalize(string? text) =>
        (text ?? string.Empty)
            .Replace("^^", " ", StringComparison.Ordinal)
            .Replace("**", " ", StringComparison.Ordinal)
            .Replace("\u00a0", " ", StringComparison.Ordinal)
            .Replace("\u2011", "-", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Trim();

    // Keeps the card to one or two lines. Cutting mid-summary would change the meaning, so an over-long
    // result is dropped in favour of the rule's name.
    private static string? Clamp(string summary)
    {
        var words = summary.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var text = string.Join(' ', words);
        return words.Length <= MaxWords && text.Length <= MaxChars ? text : null;
    }

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();

    private static string TitleCase(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Capitalize));

    /// <summary>The standard tabletop abbreviations, applied centrally so every card reads the same way.</summary>
    private static class Shorthand
    {
        private static readonly (string Prefix, string Short)[] Keywords =
        [
            ("SUSTAINED HITS", "SH"),
            ("LETHAL HITS", "LH"),
            ("DEVASTATING WOUNDS", "DW"),
            ("IGNORES COVER", "Ignores Cover"),
            ("TWIN-LINKED", "Twin-linked"),
            ("RAPID FIRE", "RF"),
            ("ANTI-", "Anti-"),
            ("PRECISION", "Precision"),
            ("ASSAULT", "Assault"),
            ("HEAVY", "Heavy"),
            ("BLAST", "Blast"),
            ("HAZARDOUS", "Hazardous"),
            ("INDIRECT FIRE", "Indirect"),
            ("LANCE", "Lance"),
            ("MELTA", "Melta"),
            ("PISTOL", "Pistol"),
            ("TORRENT", "Torrent"),
        ];

        /// <summary>"SUSTAINED HITS 1" → "SH1"; an unknown keyword keeps its printed form, title-cased.</summary>
        public static string Keyword(string keyword)
        {
            foreach (var (prefix, shortForm) in Keywords)
            {
                if (!keyword.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                var tail = keyword[prefix.Length..].Trim();
                // "SH" + "1" reads as SH1; a spelled-out short form keeps the space ("Anti- Vehicle 4+").
                var glue = shortForm.Length <= 2 || prefix.EndsWith('-') ? "" : " ";
                return (shortForm + glue + tail).Trim();
            }
            return CapitalizeWords(keyword);
        }

        /// <summary>"Objective Control" → "OC", "Strength" → "Str", …</summary>
        public static string Characteristic(string name) => name.ToLowerInvariant() switch
        {
            "strength" => "Str",
            "attacks" => "A",
            "move" => "M",
            "toughness" => "T",
            "objective control" => "OC",
            "leadership" => "Ld",
            "damage" => "Dmg",
            "armour penetration" => "AP",
            _ => name,
        };

        private static string CapitalizeWords(string value) =>
            string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }
}
