using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Tests;

/// <summary>
/// Tests for the AB7 authoring support in Core: id-preserving enrichment (so editing a datasheet doesn't
/// break roster references) and the <see cref="CatalogueIntegrity"/> referential-integrity checks.
/// </summary>
public class CatalogueAuthoringTests
{
    [Fact]
    public void Enrich_assigns_slug_id_only_when_empty()
    {
        var data = new CatalogueData
        {
            Faction = "Necrons",
            Datasheets =
            [
                new Datasheet { Name = "Necron Warriors" },                       // no id → derive
                new Datasheet { Id = "legacy-id", Name = "Renamed Overlord" },    // existing id → preserve
            ],
        };

        CatalogueSeedLoader.Enrich(data);

        Assert.Equal("necron-warriors", data.Datasheets[0].Id);
        Assert.Equal("legacy-id", data.Datasheets[1].Id); // not re-slugged to "renamed-overlord"
    }

    [Fact]
    public void Enrich_still_recomputes_other_derived_fields_for_existing_ids()
    {
        var data = new CatalogueData
        {
            Faction = "Necrons",
            Datasheets =
            [
                new Datasheet
                {
                    Id = "kept",
                    Name = "Custom Lord",
                    IsCharacter = true,
                    Keywords = ["Faction: Necrons", "Monster"],
                },
            ],
        };

        CatalogueSeedLoader.Enrich(data);

        var d = data.Datasheets[0];
        Assert.Equal("kept", d.Id);
        Assert.True(d.IsMonster);          // recomputed from keywords
        Assert.True(d.WarlordEligible);    // recomputed from IsCharacter
    }

    [Fact]
    public void Integrity_flags_duplicate_datasheet_ids_as_errors()
    {
        var data = new CatalogueData
        {
            Datasheets =
            [
                new Datasheet { Id = "dup", Name = "One", PointsOptions = [new PointsOption { Models = 1, Points = 10 }] },
                new Datasheet { Id = "dup", Name = "Two", PointsOptions = [new PointsOption { Models = 1, Points = 10 }] },
            ],
        };

        var issues = CatalogueIntegrity.Check(data);

        Assert.Contains(issues, i => i.IsError && i.Text.Contains("Duplicate datasheet id"));
    }

    [Fact]
    public void Integrity_warns_on_binding_to_unknown_unit()
    {
        var data = new CatalogueData
        {
            Datasheets = [new Datasheet { Id = "a", Name = "Alpha", PointsOptions = [new PointsOption { Models = 1, Points = 10 }] }],
            PantheonBindings = [new PantheonBinding { Name = "Ghost Binding", Unit = "Nonexistent", Points = 30 }],
        };

        var issues = CatalogueIntegrity.Check(data);

        Assert.Contains(issues, i => !i.IsError && i.Text.Contains("references unknown unit"));
    }

    [Fact]
    public void Integrity_flags_wargear_min_above_max_and_duplicate_option_ids()
    {
        var data = new CatalogueData
        {
            Datasheets =
            [
                new Datasheet
                {
                    Id = "w",
                    Name = "Wargear Unit",
                    PointsOptions = [new PointsOption { Models = 1, Points = 10 }],
                    WargearGroups =
                    [
                        new WargearGroup
                        {
                            Id = "g", Name = "Guns", Min = 2, Max = 1,
                            Options =
                            [
                                new WargearOption { Id = "o1", Name = "Gun A" },
                                new WargearOption { Id = "o1", Name = "Gun B" }, // duplicate id
                            ],
                        },
                    ],
                },
            ],
        };

        var issues = CatalogueIntegrity.Check(data);

        Assert.Contains(issues, i => i.IsError && i.Text.Contains("Min 2 above Max 1"));
        Assert.Contains(issues, i => i.IsError && i.Text.Contains("duplicate option id"));
    }

    [Fact]
    public void Integrity_is_clean_for_a_well_formed_catalogue()
    {
        var data = new CatalogueData
        {
            Faction = "Necrons",
            Datasheets =
            [
                new Datasheet { Name = "Necron Warriors", Keywords = ["Faction: Necrons"], PointsOptions = [new PointsOption { Models = 10, Points = 90 }] },
            ],
        };
        CatalogueSeedLoader.Enrich(data);

        Assert.DoesNotContain(CatalogueIntegrity.Check(data), i => i.IsError);
    }
}
