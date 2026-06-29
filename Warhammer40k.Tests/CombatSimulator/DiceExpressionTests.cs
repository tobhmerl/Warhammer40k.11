using Warhammer40k._11.Features.CombatSimulator.Dice;
using Warhammer40k._11.Features.CombatSimulator.Engine;

namespace Warhammer40k.Tests.CombatSimulator;

/// <summary>Pins the dice-expression parser + evaluator (§5). Part of the removable Combat Simulator feature.</summary>
public class DiceExpressionTests
{
    [Theory]
    [InlineData("3", 0, 0, 3)]
    [InlineData("D6", 1, 6, 0)]
    [InlineData("2D6", 2, 6, 0)]
    [InlineData("D6+1", 1, 6, 1)]
    [InlineData("D3+3", 1, 3, 3)]
    [InlineData("2D6-1", 2, 6, -1)]
    public void Parses_the_grammar(string text, int count, int sides, int mod)
    {
        var e = DiceExpression.Parse(text);
        Assert.Equal(count, e.Count);
        Assert.Equal(sides, e.Sides);
        Assert.Equal(mod, e.Modifier);
    }

    [Theory]
    [InlineData("3", 3.0)]
    [InlineData("D6", 3.5)]
    [InlineData("2D6", 7.0)]
    [InlineData("D6+1", 4.5)]
    [InlineData("D3+3", 5.0)]
    [InlineData("D3", 2.0)]
    public void Computes_the_mean(string text, double expected) =>
        Assert.Equal(expected, DiceExpression.Parse(text).ExpectedValue(), 3);

    [Fact]
    public void Unparseable_falls_back_to_a_constant()
    {
        var logged = false;
        var e = DiceExpression.Parse("banana", fallback: 2, onError: _ => logged = true);
        Assert.True(logged);
        Assert.Equal(2.0, e.ExpectedValue(), 3);
    }

    [Fact]
    public void Roll_is_within_bounds()
    {
        var rng = new DiceRoller(123);
        var e = DiceExpression.Parse("2D6+1");
        for (var i = 0; i < 1000; i++)
        {
            var r = e.Roll(rng);
            Assert.InRange(r, 3, 13);
        }
    }
}
