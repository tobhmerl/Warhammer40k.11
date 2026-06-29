using System.Globalization;
using System.Text.RegularExpressions;
using Warhammer40k._11.Features.CombatSimulator.Engine;

namespace Warhammer40k._11.Features.CombatSimulator.Dice;

/// <summary>
/// A parsed dice expression of the grammar <c>mDk(+/-c)</c> — e.g. <c>"3"</c>, <c>"D6"</c>, <c>"2D6"</c>,
/// <c>"D6+1"</c>, <c>"D3+3"</c>, <c>"2D6-1"</c>. Immutable; parse once and reuse. An unparseable input falls
/// back to a safe constant (see <see cref="Parse"/>). Part of the removable Combat Simulator feature.
/// </summary>
public sealed record DiceExpression
{
    /// <summary>Number of dice (the <c>m</c> in <c>mDk</c>); 0 for a pure constant.</summary>
    public int Count { get; }

    /// <summary>Faces per die (the <c>k</c>); 0 for a pure constant.</summary>
    public int Sides { get; }

    /// <summary>The flat modifier (the <c>c</c>), may be negative.</summary>
    public int Modifier { get; }

    /// <summary>The original text this was parsed from (for display / diagnostics).</summary>
    public string Raw { get; }

    private DiceExpression(int count, int sides, int modifier, string raw)
    {
        Count = count;
        Sides = sides;
        Modifier = modifier;
        Raw = raw;
    }

    /// <summary>A constant (no dice).</summary>
    public static DiceExpression Constant(int value) => new(0, 0, value, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>True when this expression has no random component.</summary>
    public bool IsFixed => Count == 0 || Sides <= 1;

    // m (optional) 'D' k  then optional +c / -c. Whitespace tolerant; case-insensitive 'D'.
    private static readonly Regex Grammar =
        new(@"^\s*(?<count>\d+)?\s*[dD]\s*(?<sides>\d+)\s*(?<mod>[+-]\s*\d+)?\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Parses a dice expression. Accepts a bare integer (<c>"4"</c>) or the <c>mDk±c</c> grammar. When the input
    /// can't be parsed, returns a constant of <paramref name="fallback"/> and reports it via <paramref name="onError"/>.
    /// </summary>
    public static DiceExpression Parse(string? text, int fallback = 1, Action<string>? onError = null)
    {
        var raw = (text ?? "").Trim();
        if (raw.Length == 0)
            return Constant(fallback);

        // Bare integer, e.g. "2" or "-1".
        if (int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var c))
            return new DiceExpression(0, 0, c, raw);

        var m = Grammar.Match(raw);
        if (!m.Success)
        {
            onError?.Invoke($"Unparseable dice expression '{raw}', defaulting to {fallback}.");
            return Constant(fallback);
        }

        var count = m.Groups["count"].Success ? int.Parse(m.Groups["count"].Value, CultureInfo.InvariantCulture) : 1;
        var sides = int.Parse(m.Groups["sides"].Value, CultureInfo.InvariantCulture);
        var mod = 0;
        if (m.Groups["mod"].Success)
            mod = int.Parse(m.Groups["mod"].Value.Replace(" ", ""), CultureInfo.InvariantCulture);

        return new DiceExpression(count, sides, mod, raw);
    }

    /// <summary>A single random evaluation (sum of the dice + modifier). Never returns below 0.</summary>
    public int Roll(DiceRoller rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var total = Modifier;
        for (var i = 0; i < Count; i++)
            total += rng.Die(Sides);
        return Math.Max(0, total);
    }

    /// <summary>The mean value of this expression. Mean of a dK is (k+1)/2.</summary>
    public double ExpectedValue()
    {
        var dicePart = Count * (Sides + 1) / 2.0;
        return Math.Max(0.0, dicePart + Modifier);
    }

    public override string ToString() => Raw;
}
