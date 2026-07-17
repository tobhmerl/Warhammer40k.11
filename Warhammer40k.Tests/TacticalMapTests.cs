using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Tactical;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins the Tactical Map domain: keyword-based base-size defaults and the map-plan model that seeds and
/// persists per-model tokens.
/// </summary>
public class TacticalMapTests
{
    [Theory]
    [InlineData(new[] { "Infantry" }, 32)]
    [InlineData(new[] { "Infantry", "Character" }, 40)]
    [InlineData(new[] { "Epic Hero", "Character" }, 40)]
    [InlineData(new[] { "Vehicle" }, 90)]
    [InlineData(new[] { "Monster" }, 90)]
    [InlineData(new[] { "Mounted" }, 60)]
    public void Base_size_defaults_follow_keywords(string[] keywords, int expectedMm)
    {
        Assert.Equal(expectedMm, BaseSizeDefaults.ForKeywords(keywords));
    }

    [Fact]
    public void Base_size_defaults_fall_back_to_standard_infantry()
    {
        Assert.Equal(BaseSizeDefaults.Default, BaseSizeDefaults.ForKeywords([]));
        Assert.Equal(BaseSizeDefaults.Default, BaseSizeDefaults.ForKeywords(["Fly", "Objective Secured"]));
    }

    [Fact]
    public void Base_size_defaults_from_datasheet_use_its_keywords()
    {
        var vehicle = new Datasheet { Id = "monolith", Name = "Monolith", Keywords = ["Vehicle", "Titanic"] };
        Assert.Equal(90, BaseSizeDefaults.ForDatasheet(vehicle));
    }

    [Fact]
    public void Resolve_returns_default_map_for_unknown_id()
    {
        Assert.Equal(TacticalMaps.DefaultMapId, TacticalMaps.Resolve("does-not-exist").Id);
        Assert.Equal(TacticalMaps.DefaultMapId, TacticalMaps.Resolve(null).Id);
    }

    [Fact]
    public void Layout_a_is_a_standard_44_by_60_board()
    {
        var map = TacticalMaps.Resolve(TacticalMaps.DefaultMapId);
        Assert.Equal(44, map.WidthInches);
        Assert.Equal(60, map.HeightInches);
        Assert.False(string.IsNullOrWhiteSpace(map.BackgroundUrl));
    }

    [Fact]
    public void A_plan_defaults_to_the_layout_a_map_and_no_tokens()
    {
        var plan = new TacticalPlan();
        Assert.Equal(TacticalMaps.DefaultMapId, plan.MapId);
        Assert.Empty(plan.Tokens);
    }

    [Fact]
    public void Tokens_carry_side_base_size_and_inch_position()
    {
        var token = new MapToken
        {
            RosterUnitId = "u1",
            Label = "Necron Warriors",
            Side = MapSide.Player,
            BaseMm = 32,
            XInches = 12.5,
            YInches = 40,
        };

        Assert.Equal(MapSide.Player, token.Side);
        Assert.Equal(32, token.BaseMm);
        Assert.Equal(12.5, token.XInches);
        Assert.Equal(40, token.YInches);
        Assert.False(string.IsNullOrEmpty(token.Id));
    }

    // ---- Coherency ----

    private static MapToken Model(string unit, double x, double y, int baseMm = 32) =>
        new() { RosterUnitId = unit, Side = MapSide.Player, BaseMm = baseMm, XInches = x, YInches = y };

    [Fact]
    public void Required_neighbours_scale_with_unit_size()
    {
        Assert.Equal(0, Coherency.RequiredNeighbours(1));
        Assert.Equal(1, Coherency.RequiredNeighbours(2));
        Assert.Equal(1, Coherency.RequiredNeighbours(6));
        Assert.Equal(2, Coherency.RequiredNeighbours(7));
        Assert.Equal(2, Coherency.RequiredNeighbours(20));
    }

    [Fact]
    public void Edge_distance_subtracts_both_base_radii()
    {
        // Two 32mm bases (radius ~0.63") with centres 3" apart -> edge ~1.74".
        var a = Model("u", 0, 0);
        var b = Model("u", 3, 0);
        var edge = Coherency.EdgeDistanceInches(a, b);
        Assert.InRange(edge, 1.7, 1.8);
    }

    [Fact]
    public void A_model_within_two_inches_of_one_other_is_coherent_in_a_small_unit()
    {
        var moved = Model("u", 0, 0);
        var others = new[] { Model("u", 2, 0), Model("u", 30, 0) };
        Assert.True(Coherency.IsInCoherency(moved, others));
    }

    [Fact]
    public void A_stranded_model_is_out_of_coherency()
    {
        var moved = Model("u", 0, 0);
        var others = new[] { Model("u", 20, 0), Model("u", 22, 0) };
        Assert.False(Coherency.IsInCoherency(moved, others));
    }

    [Fact]
    public void A_large_unit_model_needs_two_neighbours_within_range()
    {
        // Unit of 7: the moved model plus 6 others. Only one neighbour is close -> not coherent.
        var moved = Model("u", 0, 0);
        var others = new List<MapToken>
        {
            Model("u", 2, 0),   // close
            Model("u", 20, 0), Model("u", 21, 0), Model("u", 22, 0), Model("u", 23, 0), Model("u", 24, 0),
        };
        Assert.False(Coherency.IsInCoherency(moved, others));

        // Add a second close neighbour -> coherent (still a 7-model unit context via others.Count + 1).
        var others2 = new List<MapToken> { Model("u", 2, 0), Model("u", -2, 0),
            Model("u", 20, 0), Model("u", 21, 0), Model("u", 22, 0), Model("u", 23, 0) };
        Assert.True(Coherency.IsInCoherency(moved, others2));
    }

    [Fact]
    public void Broken_ids_flag_only_stranded_models_and_ignore_singletons()
    {
        var tokens = new List<MapToken>
        {
            // Coherent pair.
            Model("a", 0, 0), Model("a", 2, 0),
            // Stranded model in unit b (its partner is far away) -> both flagged.
            Model("b", 0, 30), Model("b", 20, 30),
            // Opponent marker (no unit id) -> never flagged.
            new() { RosterUnitId = "", Side = MapSide.Opponent, BaseMm = 40, XInches = 40, YInches = 40 },
        };

        var broken = Coherency.BrokenTokenIds(tokens);
        Assert.Equal(2, broken.Count);
        Assert.All(tokens.Where(t => t.RosterUnitId == "b"), t => Assert.Contains(t.Id, broken));
        Assert.DoesNotContain(tokens.First(t => t.RosterUnitId == "a").Id, broken);
    }
}
