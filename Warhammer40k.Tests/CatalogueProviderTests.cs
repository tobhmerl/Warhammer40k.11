using Warhammer40k.Api;
using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Tests;

/// <summary>
/// Exercises the <b>real</b> embedded seed (52 datasheets / 4 bindings) through
/// <see cref="CatalogueProvider"/> to prove the loader derivations hold on production data.
/// Branch coverage of the derivation rules lives in <see cref="CatalogueSeedLoaderTests"/>.
/// </summary>
public class CatalogueProviderTests
{
    private static readonly CatalogueData Catalogue = CatalogueProvider.LoadEmbedded();

    private static Datasheet Get(string id) =>
        Catalogue.FindById(id) ?? throw new InvalidOperationException($"Datasheet '{id}' missing from seed.");

    [Fact]
    public void Loads_real_seed_with_expected_counts()
    {
        Assert.Equal("Necrons", Catalogue.Faction);
        Assert.Equal(52, Catalogue.Datasheets.Count);
        Assert.Equal(4, Catalogue.PantheonBindings.Count);
    }

    [Fact]
    public void Every_datasheet_gets_a_unique_non_empty_slug_id()
    {
        Assert.All(Catalogue.Datasheets, d => Assert.False(string.IsNullOrWhiteSpace(d.Id)));
        var distinct = Catalogue.Datasheets.Select(d => d.Id).Distinct().Count();
        Assert.Equal(Catalogue.Datasheets.Count, distinct);
    }

    [Theory]
    [InlineData("overlord", 3)]          // generic Character
    [InlineData("necron-warriors", 6)]   // Battleline
    [InlineData("immortals", 6)]         // Battleline
    [InlineData("ghost-ark", 6)]         // Dedicated Transport
    [InlineData("ctan-shard-of-the-nightbringer", 1)] // Epic Hero
    public void Derives_copy_caps_on_real_units(string id, int expected) =>
        Assert.Equal(expected, Get(id).MaxCopies);

    [Fact]
    public void Overlord_is_a_warlord_eligible_leader()
    {
        var overlord = Get("overlord");
        Assert.True(overlord.IsCharacter);
        Assert.True(overlord.WarlordEligible);
        Assert.True(overlord.CanTakeEnhancements);
        Assert.True(overlord.HasLeaderAbility);
        Assert.Contains("immortals", overlord.LeaderTargetIds);
        Assert.Contains("necron-warriors", overlord.LeaderTargetIds);
    }

    [Fact]
    public void Ctan_cannot_be_warlord_or_take_enhancements()
    {
        var nightbringer = Get("ctan-shard-of-the-nightbringer");
        Assert.True(nightbringer.IsMonster);
        Assert.True(nightbringer.IsUnique);
        Assert.False(nightbringer.WarlordEligible);
        Assert.False(nightbringer.CanTakeEnhancements);

        var transcendent = Get("transcendent-ctan");
        Assert.True(transcendent.IsMonster);
        Assert.False(transcendent.WarlordEligible);
        Assert.False(transcendent.CanTakeEnhancements); // "cannot be given Enhancements"
    }

    [Fact]
    public void Every_monster_has_a_matching_pantheon_binding()
    {
        var monsters = Catalogue.Datasheets.Where(d => d.IsMonster).ToList();
        Assert.Equal(Catalogue.PantheonBindings.Count, monsters.Count);
        Assert.All(monsters, m => Assert.NotNull(Catalogue.FindBindingForUnit(m.Name)));
    }

    [Theory]
    [InlineData("C'tan Shard of the Deceiver", "Singularity Matrix", 55)]
    [InlineData("C'tan Shard of the Void Dragon", "Animus Damper", 35)]
    public void Resolves_pantheon_binding_details(string unit, string bindingName, int points)
    {
        var binding = Catalogue.FindBindingForUnit(unit);
        Assert.NotNull(binding);
        Assert.Equal(bindingName, binding!.Name);
        Assert.Equal(points, binding.Points);
    }

    [Fact]
    public void Provider_caches_a_single_enriched_instance()
    {
        var provider = new CatalogueProvider();
        Assert.Same(provider.Catalogue, provider.Catalogue);
    }
}
