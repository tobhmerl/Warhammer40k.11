using Warhammer40k.Core.Play;

namespace Warhammer40k.Tests;

/// <summary>Pins <see cref="WeaponMath.TotalAttacks"/>: models × fixed A, dice-aware formulas, edge cases.</summary>
public class WeaponMathTests
{
    [Theory]
    [InlineData(20, "1", "20")]   // 20 warriors × A1 gauss flayer = 20
    [InlineData(10, "2", "20")]
    [InlineData(5, "3", "15")]
    [InlineData(1, "4", "4")]
    public void Fixed_attacks_multiply_by_models(int models, string a, string expected) =>
        Assert.Equal(expected, WeaponMath.TotalAttacks(models, a));

    [Theory]
    [InlineData(20, "D6", "20×D6")]   // random A kept as a formula, not a wrong product
    [InlineData(10, "2D6", "10×2D6")]
    [InlineData(3, "D3+1", "3×D3+1")]
    public void Random_attacks_render_as_formula(int models, string a, string expected) =>
        Assert.Equal(expected, WeaponMath.TotalAttacks(models, a));

    [Fact]
    public void Single_model_shows_raw_random_value()
    {
        Assert.Equal("D6", WeaponMath.TotalAttacks(1, "D6"));
        Assert.Equal("3", WeaponMath.TotalAttacks(1, "3"));
    }

    [Fact]
    public void Empty_attacks_returns_dash() => Assert.Equal("–", WeaponMath.TotalAttacks(10, ""));

    [Fact]
    public void Zero_or_negative_models_treated_as_one()
    {
        Assert.Equal("4", WeaponMath.TotalAttacks(0, "4"));
        Assert.Equal("4", WeaponMath.TotalAttacks(-3, "4"));
    }

    [Theory]
    [InlineData("3", true)]
    [InlineData("12", true)]
    [InlineData("D6", false)]
    [InlineData("2D6", false)]
    [InlineData("", false)]
    public void IsFixed_detects_integer_attacks(string a, bool expected) =>
        Assert.Equal(expected, WeaponMath.IsFixed(a));
}
