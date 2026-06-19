using Warhammer40k.Api;
using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins the attachment-relationship queries on <see cref="CatalogueData"/> that drive the Catalogue's
/// attach filter: forward (a Leader's bodyguards), reverse (a unit's Leaders), and the bidirectional union.
/// Uses a small inline catalogue for the logic plus the real embedded seed for the Imotekh worked example.
/// </summary>
public class CatalogueAttachmentTests
{
    private static Datasheet Sheet(string id, string name, params string[] leaderTargetIds) => new()
    {
        Id = id,
        Name = name,
        LeaderTargetIds = leaderTargetIds.ToList(),
    };

    // overlord & royal-warden lead warriors/immortals; cryptek leads (joins) the royal-warden.
    private static CatalogueData Inline() => new()
    {
        Datasheets =
        [
            Sheet("overlord", "Overlord", "necron-warriors", "immortals"),
            Sheet("royal-warden", "Royal Warden", "necron-warriors", "immortals"),
            Sheet("cryptek", "Cryptek", "royal-warden"),
            Sheet("necron-warriors", "Necron Warriors"),
            Sheet("immortals", "Immortals"),
            Sheet("lone-vehicle", "Lone Vehicle"),
        ],
    };

    [Fact]
    public void UnitsLedBy_returns_forward_targets_in_catalogue_order()
    {
        var cat = Inline();
        var led = cat.UnitsLedBy(cat.FindById("overlord")!);
        Assert.Equal(new[] { "necron-warriors", "immortals" }, led.Select(d => d.Id));
    }

    [Fact]
    public void UnitsLedBy_is_empty_for_a_non_leader()
    {
        var cat = Inline();
        Assert.Empty(cat.UnitsLedBy(cat.FindById("necron-warriors")!));
    }

    [Fact]
    public void LeadersOf_returns_every_leader_that_can_join_the_unit()
    {
        var cat = Inline();
        var leaders = cat.LeadersOf(cat.FindById("necron-warriors")!);
        Assert.Equal(new[] { "overlord", "royal-warden" }, leaders.Select(d => d.Id));
    }

    [Fact]
    public void AttachmentPartners_unions_both_directions_and_excludes_self()
    {
        var cat = Inline();
        // Royal Warden both leads (warriors, immortals) and can be joined by the Cryptek.
        var partners = cat.AttachmentPartners(cat.FindById("royal-warden")!).Select(d => d.Id).ToList();
        // Catalogue order: cryptek, necron-warriors, immortals.
        Assert.Equal(new[] { "cryptek", "necron-warriors", "immortals" }, partners);
        Assert.DoesNotContain("royal-warden", partners);
    }

    [Fact]
    public void AttachmentPartners_is_empty_for_an_unattachable_unit()
    {
        var cat = Inline();
        Assert.Empty(cat.AttachmentPartners(cat.FindById("lone-vehicle")!));
    }

    [Fact]
    public void Real_seed_Imotekh_can_attach_to_warriors_immortals_and_lychguard()
    {
        var catalogue = CatalogueProvider.LoadEmbedded();
        var imotekh = catalogue.FindById("imotekh-the-stormlord");
        Assert.NotNull(imotekh);

        var partners = catalogue.AttachmentPartners(imotekh!).Select(d => d.Id).ToHashSet();
        Assert.Contains("necron-warriors", partners);
        Assert.Contains("immortals", partners);
        Assert.Contains("lychguard", partners);
    }

    [Fact]
    public void Real_seed_warriors_list_their_leaders_including_overlord()
    {
        var catalogue = CatalogueProvider.LoadEmbedded();
        var warriors = catalogue.FindById("necron-warriors");
        Assert.NotNull(warriors);

        var leaderIds = catalogue.LeadersOf(warriors!).Select(d => d.Id).ToHashSet();
        Assert.Contains("overlord", leaderIds);
        Assert.Contains("imotekh-the-stormlord", leaderIds);
    }
}
