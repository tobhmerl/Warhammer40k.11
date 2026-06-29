using System.Globalization;
using System.Text.RegularExpressions;
using Warhammer40k._11.Features.CombatSimulator.Domain;

namespace Warhammer40k._11.Features.CombatSimulator.Import;

/// <summary>
/// Turns a weapon's keyword list (e.g. <c>"Anti-Infantry 4+, Devastating Wounds, Rapid Fire 1"</c> or a list of
/// already-split tokens) into typed <see cref="WeaponAbility"/> values. Unrecognised tokens become
/// <see cref="UnknownAbility"/> so they are shown, never dropped. Part of the removable Combat Simulator feature.
/// </summary>
public static class WeaponKeywordParser
{
    private static readonly Regex RapidFireRx = new(@"^rapid\s*fire\s*(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SustainedRx = new(@"^sustained\s*hits\s*(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MeltaRx = new(@"^melta\s*(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AntiRx = new(@"^anti-?\s*([a-z' ]+?)\s*(\d)\+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Parses a single keyword string (possibly comma-separated) into abilities.</summary>
    public static List<WeaponAbility> Parse(string? keywords)
    {
        var result = new List<WeaponAbility>();
        if (string.IsNullOrWhiteSpace(keywords))
            return result;
        foreach (var raw in keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            AddToken(result, raw);
        return result;
    }

    /// <summary>Parses an already-split list of keyword tokens into abilities.</summary>
    public static List<WeaponAbility> Parse(IEnumerable<string> tokens)
    {
        var result = new List<WeaponAbility>();
        if (tokens is null)
            return result;
        foreach (var token in tokens)
            AddToken(result, token);
        return result;
    }

    private static void AddToken(List<WeaponAbility> result, string raw)
    {
        var token = raw.Trim();
        if (token.Length == 0)
            return;

        Match m;
        if ((m = RapidFireRx.Match(token)).Success) { result.Add(new RapidFire(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))); return; }
        if ((m = SustainedRx.Match(token)).Success) { result.Add(new SustainedHits(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))); return; }
        if ((m = MeltaRx.Match(token)).Success) { result.Add(new Melta(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))); return; }
        if ((m = AntiRx.Match(token)).Success)
        {
            var kw = m.Groups[1].Value.Trim();
            result.Add(new Anti(kw, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)));
            return;
        }

        var key = token.ToLowerInvariant();
        WeaponAbility? ability = key switch
        {
            "blast" => new Blast(),
            "lethal hits" => new LethalHits(),
            "devastating wounds" => new DevastatingWounds(),
            "twin-linked" or "twin linked" => new TwinLinked(),
            "assault" => new Assault(),
            "heavy" => new Heavy(),
            "pistol" => new Pistol(),
            "psychic" => new Psychic(),
            "hazardous" => new Hazardous(),
            "ignores cover" => new IgnoresCover(),
            "precision" => new Precision(),
            "lance" => new Lance(),
            "torrent" => new Torrent(),
            "one shot" => new OneShot(),
            "extra attacks" => new ExtraAttacks(),
            "indirect fire" => new IndirectFire(),
            _ => null,
        };
        result.Add(ability ?? new UnknownAbility(token));
    }
}
