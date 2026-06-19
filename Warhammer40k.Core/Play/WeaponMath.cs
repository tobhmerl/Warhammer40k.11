namespace Warhammer40k.Core.Play;

/// <summary>
/// Pure helpers for the in-play attack maths shown on battle cards. A weapon's Attacks characteristic is
/// per model, so a unit's total attacks for that weapon is <c>models × A</c> — but only when A is a fixed
/// number. Random values ("D6", "2D6", "D3+1") can't be multiplied into a single integer, so they are
/// rendered as a formula ("20×D6") instead.
/// </summary>
public static class WeaponMath
{
    /// <summary>
    /// The display string for a weapon's total attacks across <paramref name="models"/> models, given its
    /// per-model Attacks characteristic <paramref name="attacks"/>. Returns a plain number for fixed A
    /// (e.g. 20), a multiplier formula for random A (e.g. "20×D6"), or the raw value when models &lt;= 1.
    /// </summary>
    public static string TotalAttacks(int models, string attacks)
    {
        var a = (attacks ?? "").Trim();
        if (a.Length == 0)
            return "–";

        var count = Math.Max(1, models);
        if (int.TryParse(a, out var perModel))
            return (perModel * count).ToString();

        // Random characteristic — keep it honest as a formula rather than a wrong product.
        return count > 1 ? $"{count}×{a}" : a;
    }

    /// <summary>True when the Attacks characteristic is a fixed integer (so a single total can be shown).</summary>
    public static bool IsFixed(string attacks) => int.TryParse((attacks ?? "").Trim(), out _);
}
