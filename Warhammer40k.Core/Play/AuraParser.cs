using System.Text.RegularExpressions;
using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Play;

/// <summary>
/// An <b>Aura</b>: an ability that affects friendly units standing within a distance of its bearer,
/// rather than the bearer's own unit alone (e.g. Nekrosor Ammentar's "Infectious Murder-madness (Aura)").
/// </summary>
/// <remarks>
/// The app has no board state, so it cannot know who is inside the bubble. It therefore does two things:
/// the bearer's own unit is <i>always</i> within its own aura, so an aura it qualifies for is applied
/// automatically; every other unit that could legally be affected is offered the aura as a manual toggle.
/// </remarks>
public sealed record AuraEffect
{
    /// <summary>The ability this aura was parsed from.</summary>
    public string SourceAbility { get; init; } = string.Empty;

    /// <summary>The aura's radius in inches (0 when the text states none).</summary>
    public int RangeInches { get; init; }

    /// <summary>Keywords a unit must have to be inside the aura's scope (e.g. <c>Necrons</c>).</summary>
    public IReadOnlyList<string> RequiredKeywords { get; init; } = [];

    /// <summary>Keywords that put a unit outside the aura's scope (e.g. <c>Monster</c>, <c>Titanic</c>).</summary>
    public IReadOnlyList<string> ExcludedKeywords { get; init; } = [];

    /// <summary>
    /// A narrowing keyword condition inside the effect ("if that model has the DESTROYER CULT keyword").
    /// Empty when the aura affects every unit in scope.
    /// </summary>
    public IReadOnlyList<string> NarrowingKeywords { get; init; } = [];

    /// <summary>
    /// True when <see cref="NarrowingKeywords"/> is only one of several alternatives ("…keyword <b>or</b> that
    /// enemy unit is the closest eligible target"). The other alternative depends on board state, so every unit
    /// in scope is offered the aura and the player decides.
    /// </summary>
    public bool HasOpenCondition { get; init; }

    /// <summary>The bracketed weapon abilities the aura grants (e.g. <c>SUSTAINED HITS 1</c>).</summary>
    public IReadOnlyList<string> GrantedKeywords { get; init; } = [];

    /// <summary>True when this aura grants nothing an attack profile can show.</summary>
    public bool IsEmpty => GrantedKeywords.Count == 0;

    /// <summary>
    /// True when a unit carrying <paramref name="keywords"/> could be affected by this aura: it matches every
    /// required keyword, none of the excluded ones, and either satisfies the narrowing keyword condition or the
    /// aura has a second, board-state alternative (<see cref="HasOpenCondition"/>).
    /// </summary>
    public bool AppliesTo(IEnumerable<string> keywords)
    {
        var owned = keywords as IReadOnlyList<string> ?? keywords.ToList();

        bool Has(string wanted) => owned.Any(k => KeywordMatch(k, wanted));

        if (RequiredKeywords.Any(k => !Has(k)))
            return false;
        if (ExcludedKeywords.Any(Has))
            return false;
        if (NarrowingKeywords.Count == 0 || HasOpenCondition)
            return true;
        return NarrowingKeywords.All(Has);
    }

    // A datasheet keyword matches when it equals the wanted one, or when it is the qualified "Prefix: Value"
    // form and the value matches — the seed stores the faction keyword as "Faction: Necrons", not "Necrons".
    private static bool KeywordMatch(string owned, string wanted)
    {
        if (string.Equals(owned, wanted, StringComparison.OrdinalIgnoreCase))
            return true;
        var colon = owned.IndexOf(':');
        return colon >= 0
            && string.Equals(owned[(colon + 1)..].Trim(), wanted, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Reads an <see cref="Ability"/>'s printed text into an <see cref="AuraEffect"/>. Auras are written to a
/// fixed pattern ("While a friendly X unit (excluding Y and Z units) is within N" of this model, …"), so the
/// scope can be derived rather than hand-authored per datasheet.
/// </summary>
public static class AuraParser
{
    private static readonly Regex RangeRx = new(@"within\s+(\d+)\s*""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FriendlyRx = new(@"friendly\s+(.+?)\s+(?:unit|model)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ExcludingRx = new(@"\(\s*excluding\s+([^)]+?)\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NarrowingRx = new(@"has\s+the\s+(.+?)\s+keyword", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GrantRx = new(@"\[([^\]]+)\]", RegexOptions.Compiled);

    /// <summary>True when this ability is written as an Aura.</summary>
    public static bool IsAura(Ability ability) =>
        ability is not null
        && (Clean(ability.Name).Contains("(aura)", StringComparison.OrdinalIgnoreCase)
            || Clean(ability.Text).Contains("aura.", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Parses <paramref name="ability"/> into an <see cref="AuraEffect"/>, or returns null when it is not an
    /// aura or grants nothing a weapon profile can carry.
    /// </summary>
    public static AuraEffect? Parse(Ability ability)
    {
        if (ability is null || !IsAura(ability))
            return null;

        var text = Clean(ability.Text);
        var granted = GrantRx.Matches(text)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (granted.Count == 0)
            return null;

        var aura = new AuraEffect
        {
            SourceAbility = ability.Name,
            RangeInches = RangeRx.Match(text) is { Success: true } r ? int.Parse(r.Groups[1].Value) : 0,
            RequiredKeywords = FriendlyRx.Match(text) is { Success: true } f ? Keywords(f.Groups[1].Value) : [],
            ExcludedKeywords = ExcludingRx.Match(text) is { Success: true } e ? Keywords(e.Groups[1].Value) : [],
            NarrowingKeywords = NarrowingRx.Match(text) is { Success: true } n ? Keywords(n.Groups[1].Value) : [],
            HasOpenCondition = NarrowingRx.Match(text) is { Success: true } o && OpensAlternative(text, o),
            GrantedKeywords = granted,
        };
        return aura;
    }

    // "…has the DESTROYER CULT keyword or that enemy unit is the closest eligible target…" — the "or" right
    // after the keyword clause means the keyword is only one of two ways in, so the aura stays open to all.
    private static bool OpensAlternative(string text, Match narrowing)
    {
        var rest = text[(narrowing.Index + narrowing.Length)..].TrimStart();
        return rest.StartsWith("or ", StringComparison.OrdinalIgnoreCase);
    }

    // Splits a keyword clause ("Monster and Titanic units", "Necrons") into single keywords, dropping the
    // trailing noun and any list punctuation the printed text uses.
    private static List<string> Keywords(string clause) =>
        clause
            .Replace(" and ", ",", StringComparison.OrdinalIgnoreCase)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part
                .TrimEnd('.')
                .Replace(" units", "", StringComparison.OrdinalIgnoreCase)
                .Replace(" models", "", StringComparison.OrdinalIgnoreCase)
                .Trim())
            .Where(part => part.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    // The seed marks emphasised keywords with "^^" / "**" wrappers; they are noise for parsing.
    private static string Clean(string? text) =>
        (text ?? string.Empty)
            .Replace("^^", " ", StringComparison.Ordinal)
            .Replace("**", " ", StringComparison.Ordinal)
            .Replace("\u00a0", " ", StringComparison.Ordinal)
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();
}
