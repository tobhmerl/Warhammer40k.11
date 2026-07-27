using System.Text.Json;
using Warhammer40k.Api;
using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins the JSON roster export against the <b>real</b> embedded seed: an Overlord (Warlord, carrying an
/// enhancement) attached to Necron Warriors under Hand of the Dynasty. Asserts that the document carries the
/// whole army — attached units as one entry, invulnerable saves, the enhancement, conferred abilities and the
/// detachment's stratagems — because the export exists precisely so nothing has to be looked up elsewhere.
/// </summary>
public class RosterExporterTests
{
    private static readonly CatalogueData Catalogue = CatalogueProvider.LoadEmbedded();
    private const string HandOfTheDynasty = "hand-of-the-dynasty";

    private static RosterUnit NewUnit(string id) => RosterUnit.FromDatasheet(
        Catalogue.FindById(id) ?? throw new InvalidOperationException($"Datasheet '{id}' missing from seed."));

    private static Roster BuildRoster()
    {
        var warriors = NewUnit("necron-warriors");
        var overlord = NewUnit("overlord");
        overlord.IsWarlord = true;
        overlord.AttachedToRosterUnitId = warriors.Id;

        return new Roster
        {
            Name = "Export Example",
            Faction = Roster.NecronsFaction,
            PointsLimit = 2000,
            DetachmentId = HandOfTheDynasty,
            Units = [overlord, warriors],
        };
    }

    [Fact]
    public void Export_merges_attached_leader_into_one_unit_with_both_model_groups()
    {
        var export = RosterExporter.Build(BuildRoster(), Catalogue);

        var unit = Assert.Single(export.Units);
        Assert.True(unit.IsAttachedUnit);
        Assert.True(unit.IsWarlord);
        Assert.Collection(unit.Models.OrderByDescending(m => m.IsLeader == true),
            leader => Assert.Equal("Overlord", leader.Name),
            body => Assert.Equal("Necron Warriors", body.Name));
    }

    [Fact]
    public void Export_carries_roster_header_and_detachment_context()
    {
        var export = RosterExporter.Build(BuildRoster(), Catalogue);

        Assert.Equal("Export Example", export.Roster.Name);
        Assert.Equal(2000, export.Roster.PointsLimit);
        Assert.True(export.Roster.TotalPoints > 0);

        var detachment = Assert.Single(export.Detachments);
        Assert.NotEmpty(detachment.Rules);
        Assert.NotEmpty(detachment.Stratagems);
        Assert.All(detachment.Stratagems, s => Assert.False(string.IsNullOrWhiteSpace(s.Effect)));
    }

    [Fact]
    public void Export_lists_army_rules_and_applicable_core_stratagems()
    {
        var export = RosterExporter.Build(BuildRoster(), Catalogue);

        Assert.NotEmpty(export.ArmyRules);
        Assert.NotEmpty(export.CoreStratagems);
    }

    [Fact]
    public void Export_surfaces_the_overlords_invulnerable_save()
    {
        var export = RosterExporter.Build(BuildRoster(), Catalogue);

        var unit = Assert.Single(export.Units);
        Assert.Contains(unit.InvulnerableSaves, s => s.Value.Contains("4+"));
    }

    [Fact]
    public void Export_includes_an_assigned_enhancement_on_its_bearer()
    {
        var roster = BuildRoster();
        // Hand of the Dynasty's Upgrades are unit-scoped, so the bearer is the Warriors body, not the Leader.
        var warriors = roster.Units.First(u => u.DatasheetId == "necron-warriors");
        warriors.AssignedEnhancementId = "enlivened-sentinels";

        var export = RosterExporter.Build(roster, Catalogue);

        var body = Assert.Single(export.Units).Models.First(m => m.IsLeader != true);
        Assert.NotNull(body.Enhancement);
        Assert.Equal("Enlivened Sentinels", body.Enhancement!.Name);
        Assert.Equal(20, body.Enhancement.Points);
    }

    [Fact]
    public void Export_gives_every_weapon_a_full_profile_and_a_carrier_count()
    {
        var export = RosterExporter.Build(BuildRoster(), Catalogue);

        var weapons = export.Units.SelectMany(u => u.Models).SelectMany(m => m.Weapons).ToList();
        Assert.NotEmpty(weapons);
        Assert.All(weapons, w =>
        {
            Assert.False(string.IsNullOrWhiteSpace(w.Name));
            Assert.Contains(w.Type, new[] { "Ranged", "Melee" });
            Assert.False(string.IsNullOrWhiteSpace(w.Damage));
            Assert.True(w.ModelsCarrying > 0);
        });
    }

    [Fact]
    public void Export_keeps_the_printed_statline_alongside_the_buffed_one()
    {
        var export = RosterExporter.Build(BuildRoster(), Catalogue);

        var body = Assert.Single(export.Units).Models.First(m => m.IsLeader != true);
        Assert.NotNull(body.BaseStatline);
        Assert.False(string.IsNullOrWhiteSpace(body.Statline.Toughness));
        Assert.False(string.IsNullOrWhiteSpace(body.Statline.Save));
    }

    [Fact]
    public void ToJson_produces_parsable_json_without_empty_collections()
    {
        var json = RosterExporter.ToJson(BuildRoster(), Catalogue);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("tombworld.roster-export/1", root.GetProperty("schema").GetString());
        Assert.NotEqual(0, root.GetProperty("units").GetArrayLength());
        // Omitted-when-null keeps the document readable: nothing is emitted as an empty optional list.
        Assert.False(root.GetProperty("units")[0].TryGetProperty("coreAbilities", out var core) && core.GetArrayLength() == 0);
    }
}
