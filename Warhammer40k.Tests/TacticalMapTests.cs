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
}
