using Warhammer40k.Core.Tactical;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins the tactical-map unit-movement geometry (rigid translate/rotate with board clamping) and the
/// stat-string parsing that feeds the range rings.
/// </summary>
public class TacticalFormationTests
{
    private static readonly MapDefinition Board = TacticalMaps.Resolve(TacticalMaps.DefaultMapId);

    private static MapToken Token(double x, double y, int baseMm = 32, string unit = "u1") =>
        new() { RosterUnitId = unit, XInches = x, YInches = y, BaseMm = baseMm };

    private static List<Formation.Placement> Placements(params MapToken[] tokens) =>
        tokens.Select(t => new Formation.Placement(t, t.XInches, t.YInches)).ToList();

    // ---- TacticalStats.ParseInches ----

    [Theory]
    [InlineData("10\"", 10)]
    [InlineData("6\"", 6)]
    [InlineData("3.5\"", 3.5)]
    [InlineData("24", 24)]
    [InlineData(" 12\" ", 12)]
    public void Parse_inches_reads_the_leading_number(string text, double expected)
    {
        Assert.Equal(expected, TacticalStats.ParseInches(text));
    }

    [Theory]
    [InlineData("Melee")]
    [InlineData("D6")]
    [InlineData("-")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_inches_returns_null_for_non_numeric_stats(string? text)
    {
        Assert.Null(TacticalStats.ParseInches(text));
    }

    // ---- Formation.Translate ----

    [Fact]
    public void Translate_moves_the_whole_formation_rigidly()
    {
        var a = Token(10, 10);
        var b = Token(12, 10);
        Formation.Translate(Placements(a, b), 5, 3, Board);

        Assert.Equal(15, a.XInches, 3);
        Assert.Equal(13, a.YInches, 3);
        Assert.Equal(17, b.XInches, 3);
        Assert.Equal(13, b.YInches, 3);
    }

    [Fact]
    public void Translate_keeps_coherency_intact_by_construction()
    {
        var a = Token(10, 10);
        var b = Token(11.5, 10);
        Assert.Empty(Coherency.BrokenTokenIds([a, b]));

        Formation.Translate(Placements(a, b), 20, 25, Board);
        Assert.Empty(Coherency.BrokenTokenIds([a, b]));
    }

    [Fact]
    public void Translate_stops_the_formation_at_the_board_edge_as_one_piece()
    {
        var a = Token(10, 10);
        var b = Token(14, 10);
        // Try to drag far past the right edge: the rightmost base stops at the edge, spacing preserved.
        Formation.Translate(Placements(a, b), 1000, 0, Board);

        var r = Coherency.RadiusInches(b);
        Assert.Equal(Board.WidthInches - r, b.XInches, 3);
        Assert.Equal(4, b.XInches - a.XInches, 3);
    }

    [Fact]
    public void Clamp_delta_is_zero_when_already_at_the_edge()
    {
        var r = Coherency.RadiusInches(Token(0, 0));
        var a = Token(Board.WidthInches - r, 10);
        var (dx, dy) = Formation.ClampDelta(Placements(a), 5, 0, Board);
        Assert.Equal(0, dx, 3);
        Assert.Equal(0, dy, 3);
    }

    // ---- Formation.Rotate ----

    [Fact]
    public void Rotate_quarter_turn_preserves_distances_and_coherency()
    {
        var a = Token(20, 20);
        var b = Token(21.5, 20);
        var c = Token(20, 21.5);
        var tokens = new[] { a, b, c };
        var (ox, oy) = Formation.Centroid(tokens);

        Formation.Rotate(Placements(tokens), ox, oy, Math.PI / 2, Board);

        Assert.Equal(1.5, Distance(a, b), 3);
        Assert.Equal(1.5, Distance(a, c), 3);
        Assert.Empty(Coherency.BrokenTokenIds(tokens));
    }

    [Fact]
    public void Rotate_full_turn_returns_models_to_their_start()
    {
        var a = Token(20, 20);
        var b = Token(23, 24);
        Formation.Rotate(Placements(a, b), 21.5, 22, Math.PI * 2, Board);

        Assert.Equal(20, a.XInches, 3);
        Assert.Equal(20, a.YInches, 3);
        Assert.Equal(23, b.XInches, 3);
        Assert.Equal(24, b.YInches, 3);
    }

    [Fact]
    public void Rotate_clamps_models_onto_the_board()
    {
        var a = Token(2, 2);
        var b = Token(6, 2);
        // Rotating near the corner would push a model off the top edge; it must stay on the board.
        Formation.Rotate(Placements(a, b), 2, 2, -Math.PI / 2, Board);

        var r = Coherency.RadiusInches(b);
        Assert.True(b.YInches >= r - 0.001);
        Assert.True(a.XInches >= r - 0.001 && a.YInches >= r - 0.001);
    }

    [Fact]
    public void Centroid_is_the_average_of_model_centers()
    {
        var (x, y) = Formation.Centroid([Token(10, 20), Token(20, 40)]);
        Assert.Equal(15, x, 3);
        Assert.Equal(30, y, 3);
    }

    private static double Distance(MapToken a, MapToken b)
    {
        var dx = a.XInches - b.XInches;
        var dy = a.YInches - b.YInches;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
