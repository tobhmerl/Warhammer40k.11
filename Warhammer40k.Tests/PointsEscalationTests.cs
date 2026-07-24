using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Rosters;
using Warhammer40k.Core.Rosters.Validation;

namespace Warhammer40k.Tests;

/// <summary>
/// Per-copy "Nth-unit" escalation pricing: datasheets with an <see cref="Datasheet.EscalationRank"/> charge
/// the cheaper <see cref="PointsOption.Points"/> for early copies and the <see cref="PointsOption.EscalatedPoints"/>
/// once a unit's copy rank reaches the threshold (e.g. Lokhust Destroyers cost more from your 3rd unit on).
/// </summary>
public class PointsEscalationTests
{
    private static Datasheet Tiered(string id, int rank, params (int Models, int Points, int Escalated)[] options) => new()
    {
        Id = id,
        Name = id,
        EscalationRank = rank,
        PointsOptions = options
            .Select(o => new PointsOption { Models = o.Models, Points = o.Points, EscalatedPoints = o.Escalated })
            .ToList(),
    };

    private static Datasheet Flat(string id, int models, int points) => new()
    {
        Id = id,
        Name = id,
        PointsOptions = [new PointsOption { Models = models, Points = points }],
    };

    private static RosterUnit Unit(string datasheetId, int models) =>
        new() { Id = Guid.NewGuid().ToString("n"), DatasheetId = datasheetId, ModelCount = models };

    private static CatalogueData Cat(params Datasheet[] sheets) =>
        new() { Faction = "Necrons", Datasheets = sheets.ToList() };

    [Fact]
    public void UnitPoints_uses_base_price_below_escalation_rank()
    {
        var sheet = Tiered("lokhust-destroyers", rank: 3, (3, 80, 90), (6, 145, 155));
        var unit = Unit("lokhust-destroyers", 3);

        Assert.Equal(80, RosterCalculator.UnitPoints(unit, sheet, copyRank: 1));
        Assert.Equal(80, RosterCalculator.UnitPoints(unit, sheet, copyRank: 2));
    }

    [Fact]
    public void UnitPoints_uses_escalated_price_at_or_above_escalation_rank()
    {
        var sheet = Tiered("lokhust-destroyers", rank: 3, (3, 80, 90), (6, 145, 155));
        var unit = Unit("lokhust-destroyers", 6);

        Assert.Equal(155, RosterCalculator.UnitPoints(unit, sheet, copyRank: 3));
        Assert.Equal(155, RosterCalculator.UnitPoints(unit, sheet, copyRank: 4));
    }

    [Fact]
    public void UnitPoints_without_escalation_ignores_rank()
    {
        var sheet = Flat("necron-warriors", 10, 80);
        var unit = Unit("necron-warriors", 10);

        Assert.Equal(80, RosterCalculator.UnitPoints(unit, sheet, copyRank: 1));
        Assert.Equal(80, RosterCalculator.UnitPoints(unit, sheet, copyRank: 9));
    }

    [Fact]
    public void Default_overload_prices_at_rank_one()
    {
        var sheet = Tiered("monolith", rank: 2, (1, 420, 440));
        var unit = Unit("monolith", 1);

        Assert.Equal(420, RosterCalculator.UnitPoints(unit, sheet));
    }

    [Fact]
    public void CopyRank_counts_same_datasheet_units_in_roster_order()
    {
        var a = Unit("lokhust-destroyers", 3);
        var b = Unit("lokhust-destroyers", 3);
        var c = Unit("lokhust-destroyers", 3);
        var other = Unit("necron-warriors", 10);
        var roster = new Roster { Units = [a, other, b, c] };

        Assert.Equal(1, RosterCalculator.CopyRank(roster, a));
        Assert.Equal(2, RosterCalculator.CopyRank(roster, b));
        Assert.Equal(3, RosterCalculator.CopyRank(roster, c));
        Assert.Equal(1, RosterCalculator.CopyRank(roster, other));
    }

    [Fact]
    public void TotalPoints_escalates_only_copies_at_or_past_the_threshold()
    {
        var cat = Cat(Tiered("lokhust-destroyers", rank: 3, (3, 80, 90)));
        var roster = new Roster
        {
            Units =
            [
                Unit("lokhust-destroyers", 3), // rank 1 -> 80
                Unit("lokhust-destroyers", 3), // rank 2 -> 80
                Unit("lokhust-destroyers", 3), // rank 3 -> 90
                Unit("lokhust-destroyers", 3), // rank 4 -> 90
            ],
        };

        Assert.Equal(80 + 80 + 90 + 90, RosterCalculator.TotalPoints(roster, cat, (Detachment?)null));
    }

    [Fact]
    public void TotalPoints_ranks_each_datasheet_independently()
    {
        var cat = Cat(
            Tiered("monolith", rank: 2, (1, 420, 440)),
            Tiered("obelisk", rank: 2, (1, 280, 310)));
        var roster = new Roster
        {
            Units =
            [
                Unit("monolith", 1), // rank 1 -> 420
                Unit("obelisk", 1),  // rank 1 -> 280
                Unit("monolith", 1), // rank 2 -> 440
                Unit("obelisk", 1),  // rank 2 -> 310
            ],
        };

        Assert.Equal(420 + 280 + 440 + 310, RosterCalculator.TotalPoints(roster, cat, (Detachment?)null));
    }

    [Fact]
    public void Real_seed_three_Doomsday_Arks_cost_650_per_MFM_2026()
    {
        var cat = Warhammer40k.Api.CatalogueProvider.LoadEmbedded();
        var sheet = cat.Datasheets.Single(d => d.Name == "Doomsday Ark");

        // MFM 2026: 1st & 2nd Doomsday Ark are 210 each, the 3rd (and beyond) escalate to 230.
        Assert.Equal(3, sheet.EscalationRank);
        var roster = new Roster
        {
            Units =
            [
                Unit(sheet.Id, 1),
                Unit(sheet.Id, 1),
                Unit(sheet.Id, 1),
            ],
        };

        Assert.Equal(650, RosterCalculator.TotalPoints(roster, cat, (Detachment?)null));
    }
}
