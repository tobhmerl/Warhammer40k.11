using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Warhammer40k.Api;
using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Rosters;
using Warhammer40k.Core.Rosters.Validation;
using Warhammer40k.Core.Text;

namespace Warhammer40k.Tests;

public class UnitPointsUpdateTests
{
    public static IEnumerable<object[]> SourcePrices
    {
        get
        {
            using var source = ReadSource();
            foreach (var unit in source.RootElement.GetProperty("units").EnumerateArray())
                foreach (var cost in unit.GetProperty("costs").EnumerateArray())
                    yield return [unit.GetProperty("name").GetString()!, unit.GetProperty("id").GetString()!, cost.GetProperty("models").GetInt32(), cost.GetProperty("points").GetInt32()];
        }
    }

    [Theory]
    [MemberData(nameof(SourcePrices))]
    public void Every_source_size_costs_exactly_the_supplied_points_for_all_copies(string name, string id, int models, int points)
    {
        var catalogue = CatalogueProvider.LoadEmbedded();
        var sheet = catalogue.Datasheets.SingleOrDefault(sheet => sheet.Name == name) ?? catalogue.FindById(Slugger.Slug(id));
        Assert.NotNull(sheet);
        var tier = Assert.Single(sheet!.PointsOptions.Where(tier => tier.Models == models));
        Assert.Equal(points, tier.Points);
        Assert.Null(tier.EscalatedPoints);
        Assert.Equal(0, sheet.EscalationRank);
        var unit = RosterUnit.FromDatasheet(sheet);
        unit.ModelCount = models;
        foreach (var copy in new[] { 1, 2, 3, 4, 6, 10 })
            Assert.Equal(points, RosterCalculator.UnitPoints(unit, sheet, copy));
    }

    [Fact]
    public void Source_matches_all_units_and_preserves_all_size_tiers_with_only_explicit_apostrophe_aliases()
    {
        using var source = ReadSource();
        var catalogue = CatalogueProvider.LoadEmbedded();
        Assert.Equal("2026-09-02", catalogue.UnitPointsVersion);
        var aliases = new Dictionary<string, string>
        {
            ["C’tan Shard of the Deceiver"] = "C'tan Shard of the Deceiver",
            ["C’tan Shard of the Nightbringer"] = "C'tan Shard of the Nightbringer",
            ["C’tan Shard of the Void Dragon"] = "C'tan Shard of the Void Dragon",
            ["Transcendent C’tan"] = "Transcendent C'tan",
        };
        var matched = new HashSet<string>();
        var exact = 0;
        foreach (var unit in source.RootElement.GetProperty("units").EnumerateArray())
        {
            var name = unit.GetProperty("name").GetString()!;
            var sheet = catalogue.Datasheets.SingleOrDefault(sheet => sheet.Name == name);
            if (sheet is not null)
                exact++;
            else
            {
                Assert.True(aliases.TryGetValue(name, out var existing));
                sheet = catalogue.FindById(Slugger.Slug(unit.GetProperty("id").GetString()!));
                Assert.Equal(existing, sheet!.Name);
            }
            Assert.True(matched.Add(sheet!.Id));
            Assert.Equal(unit.GetProperty("costs").EnumerateArray().Select(cost => cost.GetProperty("models").GetInt32()).Order(),
                sheet.PointsOptions.Select(tier => tier.Models).Order());
            Assert.Equal(sheet.PointsOptions.Min(tier => tier.Points), sheet.Points);
        }
        Assert.Equal(48, exact);
        Assert.Equal(52, matched.Count);
        Assert.Equal(catalogue.Datasheets.Count, matched.Count);
    }

    [Fact]
    public void Silent_King_remains_a_three_model_400_point_unit()
    {
        var sheet = CatalogueProvider.LoadEmbedded().FindById("the-silent-king")!;
        var tier = Assert.Single(sheet.PointsOptions);
        Assert.Equal(3, tier.Models);
        Assert.Equal(400, tier.Points);
    }

    [Fact]
    public void Old_custom_catalogue_prices_refresh_without_changing_other_content_or_sizes()
    {
        var reference = CatalogueProvider.LoadEmbedded();
        var catalogue = Clone(reference);
        catalogue.UnitPointsVersion = null;
        var sheet = catalogue.FindById("immortals")!;
        sheet.Name = "My Immortals";
        sheet.Points = 1;
        sheet.EscalationRank = 3;
        foreach (var tier in sheet.PointsOptions) { tier.Points = 1; tier.EscalatedPoints = 2; }
        sheet.Abilities.Add(new Ability { Name = "Custom rule", Text = "Keep this entered rule." });
        sheet.Keywords.Add("Custom keyword");
        var before = NonPricing(catalogue);
        var referenceBefore = JsonSerializer.Serialize(reference);
        Assert.Equal(1, UnitPointsUpdate.Apply(catalogue, reference));
        Assert.True(JsonNode.DeepEquals(before, NonPricing(catalogue)));
        Assert.Equal("My Immortals", sheet.Name);
        Assert.Equal(150, sheet.PointsOptions.Single(tier => tier.Models == 10).Points);
        Assert.Equal("2026-09-02", catalogue.UnitPointsVersion);
        Assert.Equal(referenceBefore, JsonSerializer.Serialize(reference));
    }

    [Theory]
    [InlineData("2026-09-02")]
    [InlineData("2027-01-01")]
    public void Current_or_newer_price_revisions_preserve_later_manual_overrides(string revision)
    {
        var reference = CatalogueProvider.LoadEmbedded();
        var catalogue = Clone(reference);
        catalogue.UnitPointsVersion = revision;
        catalogue.FindById("immortals")!.PointsOptions[0].Points = 123;
        var before = JsonSerializer.Serialize(catalogue);
        Assert.Equal(0, UnitPointsUpdate.Apply(catalogue, reference));
        Assert.Equal(before, JsonSerializer.Serialize(catalogue));
    }

    [Fact]
    public void Refresh_is_idempotent_and_preserves_custom_extra_tiers()
    {
        var reference = CatalogueProvider.LoadEmbedded();
        var catalogue = Clone(reference);
        catalogue.UnitPointsVersion = "2026-08-01";
        var sheet = catalogue.FindById("immortals")!;
        sheet.PointsOptions.RemoveAll(tier => tier.Models == 10);
        sheet.PointsOptions.Add(new PointsOption { Models = 15, Points = 333 });
        UnitPointsUpdate.Apply(catalogue, reference);
        Assert.Equal(150, Assert.Single(sheet.PointsOptions.Where(tier => tier.Models == 10)).Points);
        Assert.Equal(333, Assert.Single(sheet.PointsOptions.Where(tier => tier.Models == 15)).Points);
        Assert.Equal(0, UnitPointsUpdate.Apply(catalogue, reference));
        Assert.Equal(3, sheet.PointsOptions.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-9-2")]
    [InlineData("2026-02-30")]
    public void Reference_without_a_valid_revision_leaves_saved_prices_unchanged(string? revision)
    {
        var reference = CatalogueProvider.LoadEmbedded();
        reference.UnitPointsVersion = revision;
        var catalogue = Clone(reference);
        catalogue.UnitPointsVersion = "2026-08-01";
        catalogue.FindById("immortals")!.PointsOptions[0].Points = 123;
        var before = JsonSerializer.Serialize(catalogue);

        Assert.Equal(0, UnitPointsUpdate.Apply(catalogue, reference));
        Assert.Equal(before, JsonSerializer.Serialize(catalogue));
    }

    [Fact]
    public void Catalogue_with_only_unmatched_units_does_not_advance_the_revision()
    {
        var reference = CatalogueProvider.LoadEmbedded();
        var sheet = Clone(reference).FindById("immortals")!;
        sheet.Id = "custom-unit";
        sheet.Name = "Custom unit";
        var catalogue = new CatalogueData { Faction = "Necrons", Datasheets = [sheet] };
        var before = JsonSerializer.Serialize(catalogue);

        Assert.Equal(0, UnitPointsUpdate.Apply(catalogue, reference));
        Assert.Equal(before, JsonSerializer.Serialize(catalogue));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ambiguous_reference_matches_do_not_change_prices_or_advance_the_revision(bool matchByName)
    {
        var reference = CatalogueProvider.LoadEmbedded();
        var duplicate = Clone(reference).FindById("immortals")!;
        duplicate.Id = matchByName ? "other-immortals" : " IMMORTALS ";
        duplicate.Name = matchByName ? "Immortals" : "Other Immortals";
        reference.Datasheets.Add(duplicate);
        var sheet = Clone(reference).FindById("immortals")!;
        if (!matchByName)
            sheet.Name = "My Immortals";
        sheet.PointsOptions[0].Points = 123;
        var catalogue = new CatalogueData { Faction = "Necrons", Datasheets = [sheet] };
        var before = JsonSerializer.Serialize(catalogue);

        Assert.Equal(0, UnitPointsUpdate.Apply(catalogue, reference));
        Assert.Equal(before, JsonSerializer.Serialize(catalogue));
    }

    [Fact]
    public void Unknown_custom_units_and_other_factions_are_not_replaced()
    {
        var reference = CatalogueProvider.LoadEmbedded();
        var catalogue = Clone(reference);
        catalogue.UnitPointsVersion = null;
        var custom = Clone(reference).Datasheets[0];
        custom.Id = "custom-unit";
        custom.Name = "Custom unit";
        custom.Points = 777;
        catalogue.Datasheets.Add(custom);
        var before = JsonSerializer.Serialize(custom);
        UnitPointsUpdate.Apply(catalogue, reference);
        Assert.Equal(before, JsonSerializer.Serialize(custom));
        catalogue.Faction = "Other faction";
        catalogue.UnitPointsVersion = null;
        before = JsonSerializer.Serialize(catalogue);
        Assert.Equal(0, UnitPointsUpdate.Apply(catalogue, reference));
        Assert.Equal(before, JsonSerializer.Serialize(catalogue));
    }

    [Fact]
    public void Exact_name_matching_precedes_id_matching_and_does_not_rewrite_the_id()
    {
        var reference = CatalogueProvider.LoadEmbedded();
        var catalogue = Clone(reference);
        catalogue.UnitPointsVersion = null;
        var sheet = catalogue.FindById("immortals")!;
        sheet.Id = "custom-immortals-id";
        sheet.PointsOptions[0].Points = 1;
        UnitPointsUpdate.Apply(catalogue, reference);
        Assert.Equal("custom-immortals-id", sheet.Id);
        Assert.Equal(70, sheet.PointsOptions.Single(tier => tier.Models == 5).Points);
    }

    [Fact]
    public void Repository_read_refreshes_old_saved_points_without_rewriting_the_stored_payload()
    {
        var reference = CatalogueProvider.LoadEmbedded();
        var saved = Clone(reference);
        saved.UnitPointsVersion = null;
        saved.FindById("ophydian-destroyers")!.PointsOptions.Single(tier => tier.Models == 6).Points = 145;
        saved.FindById("ophydian-destroyers")!.Abilities.Add(new Ability { Name = "Entered rule", Text = "Keep this." });
        var json = JsonSerializer.Serialize(saved);
        var entity = TableCatalogueRepository.BuildEntity("test-user", json);
        var loaded = TableCatalogueRepository.LoadCurrentCatalogue(TableCatalogueRepository.ExtractJson(entity), reference);
        Assert.Equal(160, loaded.FindById("ophydian-destroyers")!.PointsOptions.Single(tier => tier.Models == 6).Points);
        Assert.Contains(loaded.FindById("ophydian-destroyers")!.Abilities, ability => ability.Name == "Entered rule");
        Assert.Equal(json, TableCatalogueRepository.ExtractJson(entity));
        var roundTrip = CatalogueSeedLoader.Load(JsonSerializer.Serialize(loaded));
        Assert.Equal("2026-09-02", roundTrip.UnitPointsVersion);
        Assert.Equal(0, UnitPointsUpdate.Apply(roundTrip, reference));
    }

    [Fact]
    public void Repository_reload_preserves_manual_prices_saved_after_the_update()
    {
        var reference = CatalogueProvider.LoadEmbedded();
        var saved = Clone(reference);
        saved.UnitPointsVersion = null;
        var refreshed = TableCatalogueRepository.LoadCurrentCatalogue(JsonSerializer.Serialize(saved), reference);
        var sheet = refreshed.FindById("immortals")!;
        sheet.Points = 123;
        sheet.PointsOptions[0].Points = 123;
        sheet.EscalationRank = 3;
        sheet.PointsOptions[0].EscalatedPoints = 150;
        var json = JsonSerializer.Serialize(refreshed);
        var entity = TableCatalogueRepository.BuildEntity("test-user", json);

        var reloaded = TableCatalogueRepository.LoadCurrentCatalogue(TableCatalogueRepository.ExtractJson(entity), reference);

        Assert.Equal(json, JsonSerializer.Serialize(reloaded));
        Assert.Equal(json, TableCatalogueRepository.ExtractJson(entity));
    }

    private static CatalogueData Clone(CatalogueData catalogue) =>
        JsonSerializer.Deserialize<CatalogueData>(JsonSerializer.Serialize(catalogue))!;

    private static JsonNode NonPricing(CatalogueData catalogue)
    {
        var node = JsonSerializer.SerializeToNode(catalogue)!.AsObject();
        node.Remove("unitPointsVersion");
        foreach (var item in node["datasheets"]!.AsArray())
        {
            var sheet = item!.AsObject();
            sheet.Remove("points");
            sheet.Remove("escalationRank");
            foreach (var option in sheet["pointsOptions"]!.AsArray())
            {
                option!.AsObject().Remove("points");
                option.AsObject().Remove("escalatedPoints");
            }
        }
        return node;
    }

    private static JsonDocument ReadSource()
    {
        using var stream = typeof(UnitPointsUpdateTests).Assembly.GetManifestResourceStream("NecronPoints20260902.md")
            ?? throw new InvalidOperationException("The authoritative points fixture is missing.");
        using var reader = new StreamReader(stream);
        var match = Regex.Match(reader.ReadToEnd(), @"(?ms)^```json\s*\r?\n(?<json>.*?)^```");
        return JsonDocument.Parse(match.Groups["json"].Value);
    }
}
