using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Tests;

public class CatalogueSeedLoaderTests
{
    // Inline fixture exercising every derivation branch (real data is covered by CatalogueProviderTests).
    private const string SeedJson = """
    {
      "faction": "Necrons",
      "datasheets": [
        {
          "name": "Overlord", "points": 85, "primaryRole": "Character",
          "isEpicHero": false, "isBattleline": false, "isDedicatedTransport": false, "isCharacter": true,
          "keywords": ["Faction: Necrons","Infantry","Character","Noble","Overlord"],
          "factionRules": ["Leader","Reanimation Protocols"],
          "abilities": [
            { "name": "Leader", "text": "This model can be attached to the following units:\n■ IMMORTALS\n■ LYCHGUARD\n■ NECRON WARRIORS" }
          ],
          "pointsOptions": [ { "models": 1, "points": 85 } ]
        },
        {
          "name": "Chronomancer", "points": 65, "primaryRole": "Character",
          "isEpicHero": false, "isBattleline": false, "isDedicatedTransport": false, "isCharacter": true,
          "keywords": ["Faction: Necrons","Infantry","Character","Cryptek","Chronomancer"],
          "factionRules": ["Leader","Reanimation Protocols"],
          "abilities": [
            { "name": "Leader", "text": "This model can be attached to the following units:\n■ IMMORTALS\n■ NECRON WARRIORS\nYou can attach this model to one of the above units even if one ROYAL WARDEN or NOBLE model has already been attached to it." }
          ],
          "pointsOptions": [ { "models": 1, "points": 65 } ]
        },
        {
          "name": "Immortals", "points": 70, "primaryRole": "Battleline",
          "isEpicHero": false, "isBattleline": true, "isDedicatedTransport": false, "isCharacter": false,
          "keywords": ["Faction: Necrons","Infantry","Battleline","Immortals"],
          "pointsOptions": [ { "models": 5, "points": 70 }, { "models": 10, "points": 150 } ]
        },
        {
          "name": "Necron Warriors", "points": 90, "primaryRole": "Battleline",
          "isEpicHero": false, "isBattleline": true, "isDedicatedTransport": false, "isCharacter": false,
          "keywords": ["Faction: Necrons","Infantry","Battleline","Necron Warriors"],
          "pointsOptions": [ { "models": 10, "points": 90 } ]
        },
        {
          "name": "Lychguard", "points": 85, "primaryRole": "Infantry",
          "isEpicHero": false, "isBattleline": false, "isDedicatedTransport": false, "isCharacter": false,
          "keywords": ["Faction: Necrons","Infantry","Lychguard"],
          "pointsOptions": [ { "models": 5, "points": 85 } ]
        },
        {
          "name": "Ghost Ark", "points": 115, "primaryRole": "Dedicated Transport",
          "isEpicHero": false, "isBattleline": false, "isDedicatedTransport": true, "isCharacter": false,
          "keywords": ["Faction: Necrons","Vehicle","Transport","Dedicated Transport","Ghost Ark"],
          "pointsOptions": [ { "models": 1, "points": 115 } ]
        },
        {
          "name": "C'tan Shard of the Deceiver", "points": 310, "primaryRole": "Epic Hero",
          "isEpicHero": true, "isBattleline": false, "isDedicatedTransport": false, "isCharacter": true,
          "keywords": ["Faction: Necrons","Monster","Character","Epic Hero","C'tan Shard of the Deceiver"],
          "abilities": [ { "name": "Enslaved Star God", "text": "This model cannot be your WARLORD." } ],
          "pointsOptions": [ { "models": 1, "points": 310 } ]
        },
        {
          "name": "Transcendent C'tan", "points": 325, "primaryRole": "Character",
          "isEpicHero": false, "isBattleline": false, "isDedicatedTransport": false, "isCharacter": true,
          "keywords": ["Faction: Necrons","Monster","Character","Transcendent C'tan"],
          "abilities": [
            { "name": "C'tan Shard", "text": "This model cannot be given Enhancements." },
            { "name": "Enslaved Star God", "text": "This model cannot be your WARLORD." }
          ],
          "pointsOptions": [ { "models": 1, "points": 325 } ]
        },
        {
          "name": "The Silent King", "points": 400, "primaryRole": "Epic Hero",
          "isEpicHero": true, "isBattleline": false, "isDedicatedTransport": false, "isCharacter": false,
          "keywords": ["Faction: Necrons","Vehicle","Epic Hero","Triarch"],
          "pointsOptions": [ { "models": 3, "points": 400 } ]
        }
      ],
      "pantheonBindings": [
        { "name": "Singularity Matrix", "unit": "C'tan Shard of the Deceiver", "points": 55 }
      ]
    }
    """;

    private static CatalogueData Load() => CatalogueSeedLoader.Load(SeedJson);

    private static Datasheet Get(CatalogueData cat, string name) =>
        cat.Datasheets.Single(d => d.Name == name);

    [Fact]
    public void Parses_faction_datasheets_and_bindings()
    {
        var cat = Load();
        Assert.Equal("Necrons", cat.Faction);
        Assert.Equal(9, cat.Datasheets.Count);
        Assert.Single(cat.PantheonBindings);
    }

    [Fact]
    public void Derives_stable_slug_ids()
    {
        var cat = Load();
        Assert.Equal("overlord", Get(cat, "Overlord").Id);
        Assert.Equal("necron-warriors", Get(cat, "Necron Warriors").Id);
        Assert.Equal("ctan-shard-of-the-deceiver", Get(cat, "C'tan Shard of the Deceiver").Id);
        Assert.NotNull(cat.FindById("ctan-shard-of-the-deceiver"));
    }

    [Theory]
    [InlineData("Immortals", 6)]            // Battleline
    [InlineData("Ghost Ark", 6)]            // Dedicated Transport
    [InlineData("Lychguard", 3)]            // generic
    [InlineData("Overlord", 3)]             // generic Character
    [InlineData("C'tan Shard of the Deceiver", 1)] // Epic Hero
    [InlineData("The Silent King", 1)]      // Epic Hero
    public void Derives_max_copies(string name, int expected) =>
        Assert.Equal(expected, Get(Load(), name).MaxCopies);

    [Theory]
    [InlineData("Overlord", true)]
    [InlineData("Chronomancer", true)]
    [InlineData("C'tan Shard of the Deceiver", false)] // "cannot be your WARLORD"
    [InlineData("Transcendent C'tan", false)]          // "cannot be your WARLORD"
    [InlineData("The Silent King", false)]             // not a Character
    [InlineData("Immortals", false)]                   // not a Character
    public void Derives_warlord_eligibility(string name, bool expected) =>
        Assert.Equal(expected, Get(Load(), name).WarlordEligible);

    [Theory]
    [InlineData("Overlord", true)]
    [InlineData("Chronomancer", true)]
    [InlineData("C'tan Shard of the Deceiver", false)] // Epic Hero
    [InlineData("Transcendent C'tan", false)]          // "cannot be given Enhancements"
    [InlineData("Immortals", false)]                   // not a Character
    public void Derives_enhancement_eligibility(string name, bool expected) =>
        Assert.Equal(expected, Get(Load(), name).CanTakeEnhancements);

    [Fact]
    public void Derives_monster_flag()
    {
        var cat = Load();
        Assert.True(Get(cat, "C'tan Shard of the Deceiver").IsMonster);
        Assert.True(Get(cat, "Transcendent C'tan").IsMonster);
        Assert.False(Get(cat, "Overlord").IsMonster);
    }

    [Fact]
    public void Resolves_leader_targets_and_co_leader_flag()
    {
        var cat = Load();

        var overlord = Get(cat, "Overlord");
        Assert.True(overlord.HasLeaderAbility);
        Assert.False(overlord.AllowsCoLeader);
        Assert.Contains("immortals", overlord.LeaderTargetIds);
        Assert.Contains("lychguard", overlord.LeaderTargetIds);
        Assert.Contains("necron-warriors", overlord.LeaderTargetIds);

        var chrono = Get(cat, "Chronomancer");
        Assert.True(chrono.AllowsCoLeader);
        Assert.Contains("immortals", chrono.LeaderTargetIds);
        Assert.Contains("necron-warriors", chrono.LeaderTargetIds);
        Assert.DoesNotContain("lychguard", chrono.LeaderTargetIds);
    }

    [Fact]
    public void Non_leaders_have_no_targets()
    {
        var cat = Load();
        var immortals = Get(cat, "Immortals");
        Assert.False(immortals.HasLeaderAbility);
        Assert.Empty(immortals.LeaderTargetIds);
    }

    [Fact]
    public void Enrich_is_idempotent()
    {
        var cat = Load();
        var before = Get(cat, "Overlord").LeaderTargetIds.Count;
        CatalogueSeedLoader.Enrich(cat);
        CatalogueSeedLoader.Enrich(cat);
        Assert.Equal(before, Get(cat, "Overlord").LeaderTargetIds.Count);
    }

    [Fact]
    public void Finds_pantheon_binding_for_unit()
    {
        var cat = Load();
        var binding = cat.FindBindingForUnit("C'tan Shard of the Deceiver");
        Assert.NotNull(binding);
        Assert.Equal("Singularity Matrix", binding!.Name);
        Assert.Equal(55, binding.Points);
    }

    [Fact]
    public void Deserializes_per_copy_escalation_fields()
    {
        const string json = """
        {
          "faction": "Necrons",
          "datasheets": [
            {
              "name": "Lokhust Destroyers", "points": 40, "primaryRole": "Infantry",
              "isEpicHero": false, "isBattleline": false, "isDedicatedTransport": false, "isCharacter": false,
              "keywords": ["Faction: Necrons","Lokhust Destroyers"],
              "escalationRank": 3,
              "pointsOptions": [
                { "models": 1, "points": 40, "escalatedPoints": 50 },
                { "models": 6, "points": 160, "escalatedPoints": 170 }
              ]
            }
          ]
        }
        """;

        var sheet = CatalogueSeedLoader.Load(json).Datasheets.Single();

        Assert.Equal(3, sheet.EscalationRank);
        var small = sheet.PointsOptions.Single(o => o.Models == 1);
        Assert.Equal(40, small.Points);
        Assert.Equal(50, small.EscalatedPoints);
        Assert.Equal(170, sheet.PointsOptions.Single(o => o.Models == 6).EscalatedPoints);
    }

    [Fact]
    public void Flat_priced_datasheets_have_no_escalation()
    {
        var cat = Load();
        var immortals = Get(cat, "Immortals");
        Assert.Equal(0, immortals.EscalationRank);
        Assert.All(immortals.PointsOptions, o => Assert.Null(o.EscalatedPoints));
    }
}
