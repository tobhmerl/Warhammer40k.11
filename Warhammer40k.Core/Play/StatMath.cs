using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Play;

/// <summary>
/// Pure display math that applies a numeric buff directly to a characteristic string, so a battle card can
/// show the modified value instead of ability prose. The notation drives the direction:
/// <list type="bullet">
/// <item>A roll characteristic (<c>"4+"</c> — Hit/Save/Leadership): a positive delta <b>improves</b> it, so
/// <c>4+</c> with <c>+1</c> becomes <c>3+</c>, clamped to the 2+..6+ range.</item>
/// <item>A Move value (<c>8"</c>): the delta is added to the inches.</item>
/// <item>A plain integer (Toughness, Attacks, …): the delta is added.</item>
/// <item>A random value (<c>D6</c>, <c>D3+1</c>): the delta is folded into a formula rather than guessed.</item>
/// </list>
/// </summary>
public static class StatMath
{
    /// <summary>The best (lowest) and worst (highest) target a roll characteristic can display.</summary>
    private const int BestRoll = 2;
    private const int WorstRoll = 6;

    /// <summary>
    /// Applies a single <paramref name="delta"/> to the characteristic <paramref name="raw"/>, returning the
    /// adjusted display string. A zero delta (or blank input) returns the value unchanged.
    /// </summary>
    public static string Apply(string raw, int delta)
    {
        var value = (raw ?? "").Trim();
        if (value.Length == 0 || delta == 0)
            return value;

        // Roll characteristic "N+" (Hit / Save / Leadership): a positive delta improves it (lower target).
        if (value.EndsWith('+') && int.TryParse(value[..^1], out var roll))
        {
            var improved = Math.Clamp(roll - delta, BestRoll, WorstRoll);
            return improved + "+";
        }

        // Move "N\"": add the delta to the inches.
        if (value.EndsWith('"') && int.TryParse(value[..^1], out var inches))
            return Math.Max(0, inches + delta) + "\"";

        // Pure integer (Toughness, Wounds, OC, Attacks, Strength, Damage): add the delta.
        if (int.TryParse(value, out var number))
            return Math.Max(0, number + delta).ToString();

        // Value ending in a constant (e.g. "D3+1"): fold the delta into that constant.
        var plus = value.LastIndexOf('+');
        if (plus > 0 && plus < value.Length - 1 && int.TryParse(value[(plus + 1)..], out var trailing))
        {
            var head = value[..plus];
            var combined = trailing + delta;
            return combined == 0 ? head : combined > 0 ? $"{head}+{combined}" : $"{head}{combined}";
        }

        // Random value with no constant (e.g. "D6", "2D6"): keep it honest as a formula.
        return delta > 0 ? $"{value}+{delta}" : $"{value}{delta}";
    }

    /// <summary>
    /// Applies all of <paramref name="modifiers"/> to <paramref name="raw"/> (their deltas are summed, then
    /// applied once). The caller is expected to pass only modifiers that target this characteristic.
    /// </summary>
    public static string ApplyAll(string raw, IEnumerable<StatModifier> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        var delta = modifiers.Sum(m => m.Delta);
        return Apply(raw, delta);
    }

    /// <summary>True when applying <paramref name="modifiers"/> would change <paramref name="raw"/>'s display.</summary>
    public static bool Changes(string raw, IEnumerable<StatModifier> modifiers)
    {
        var value = (raw ?? "").Trim();
        return !string.Equals(value, ApplyAll(value, modifiers), StringComparison.Ordinal);
    }
}
