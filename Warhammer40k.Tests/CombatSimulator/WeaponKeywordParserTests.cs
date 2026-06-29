using Warhammer40k._11.Features.CombatSimulator.Domain;
using Warhammer40k._11.Features.CombatSimulator.Import;

namespace Warhammer40k.Tests.CombatSimulator;

/// <summary>Pins weapon-keyword tokenization (§6b). Part of the removable Combat Simulator feature.</summary>
public class WeaponKeywordParserTests
{
    [Fact]
    public void Parses_a_comma_separated_list_with_valued_keywords()
    {
        var abilities = WeaponKeywordParser.Parse("Anti-Infantry 4+, Devastating Wounds, Rapid Fire 1, Sustained Hits 2, Melta 2");
        Assert.Contains(abilities, a => a is Anti { Keyword: "Infantry", CritThreshold: 4 });
        Assert.Contains(abilities, a => a is DevastatingWounds);
        Assert.Contains(abilities, a => a is RapidFire { X: 1 });
        Assert.Contains(abilities, a => a is SustainedHits { X: 2 });
        Assert.Contains(abilities, a => a is Melta { X: 2 });
    }

    [Fact]
    public void Unknown_tokens_are_preserved_not_dropped()
    {
        var abilities = WeaponKeywordParser.Parse("Wibble, Lethal Hits");
        Assert.Contains(abilities, a => a is LethalHits);
        Assert.Contains(abilities, a => a is UnknownAbility { Raw: "Wibble" });
    }

    [Fact]
    public void Twin_linked_and_torrent_are_recognised()
    {
        var abilities = WeaponKeywordParser.Parse(new[] { "Twin-linked", "Torrent", "Ignores Cover" });
        Assert.Contains(abilities, a => a is TwinLinked);
        Assert.Contains(abilities, a => a is Torrent);
        Assert.Contains(abilities, a => a is IgnoresCover);
    }
}
