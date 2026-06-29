using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins <see cref="StatMath"/>: a "+1 to Hit" improves a 4+ to a 3+ (the headline behaviour), with clamping,
/// integer/Move handling, and dice-aware formulas for random characteristics.
/// </summary>
public class StatMathTests
{
    [Theory]
    [InlineData("4+", 1, "3+")]   // the headline: +1 to Hit turns BS 4+ into 3+
    [InlineData("3+", 1, "2+")]
    [InlineData("4+", 2, "2+")]
    [InlineData("6+", 1, "5+")]   // Leadership is also "N+"
    [InlineData("4+", -1, "5+")]  // a debuff worsens it
    public void Roll_characteristic_improves_with_positive_delta(string raw, int delta, string expected) =>
        Assert.Equal(expected, StatMath.Apply(raw, delta));

    [Theory]
    [InlineData("2+", 1, "2+")]   // can't get better than 2+
    [InlineData("2+", 3, "2+")]
    [InlineData("6+", -2, "6+")]  // can't display worse than 6+
    public void Roll_characteristic_is_clamped(string raw, int delta, string expected) =>
        Assert.Equal(expected, StatMath.Apply(raw, delta));

    [Theory]
    [InlineData("7", 1, "8")]     // Toughness
    [InlineData("2", 1, "3")]     // OC / Attacks
    [InlineData("10", -2, "8")]
    public void Integer_characteristic_adds_delta(string raw, int delta, string expected) =>
        Assert.Equal(expected, StatMath.Apply(raw, delta));

    [Theory]
    [InlineData("8\"", 1, "9\"")] // Move
    [InlineData("8\"", 2, "10\"")]
    public void Move_adds_delta_to_inches(string raw, int delta, string expected) =>
        Assert.Equal(expected, StatMath.Apply(raw, delta));

    [Theory]
    [InlineData("24\"", 6, "30\"")] // Gauntlet of Compression: +6" weapon Range
    [InlineData("12\"", 6, "18\"")]
    public void Range_adds_delta_to_inches(string raw, int delta, string expected) =>
        Assert.Equal(expected, StatMath.Apply(raw, delta));

    [Theory]
    [InlineData("D6", 1, "D6+1")]    // no constant → fold into a formula
    [InlineData("D3+1", 1, "D3+2")]  // existing constant → combine
    [InlineData("D3+1", -1, "D3")]   // constant cancels out
    public void Random_characteristic_uses_a_formula(string raw, int delta, string expected) =>
        Assert.Equal(expected, StatMath.Apply(raw, delta));

    [Theory]
    [InlineData("N/A", 1, "N/A")]    // an auto-hit weapon has no Hit roll, so +1 Hit does nothing
    [InlineData("N/A", -1, "N/A")]
    public void Auto_hit_skill_is_unchanged_by_a_delta(string raw, int delta, string expected) =>
        Assert.Equal(expected, StatMath.Apply(raw, delta));

    [Theory]
    [InlineData("4+", 0, "4+")]
    [InlineData("", 5, "")]
    public void Zero_delta_or_blank_is_unchanged(string raw, int delta, string expected) =>
        Assert.Equal(expected, StatMath.Apply(raw, delta));

    [Fact]
    public void ApplyAll_sums_deltas()
    {
        var mods = new[]
        {
            new StatModifier { Target = StatTarget.Skill, Delta = 1 },
            new StatModifier { Target = StatTarget.Skill, Delta = 1 },
        };
        Assert.Equal("2+", StatMath.ApplyAll("4+", mods));
    }

    [Fact]
    public void ApplyAll_with_no_modifiers_is_unchanged() =>
        Assert.Equal("4+", StatMath.ApplyAll("4+", Array.Empty<StatModifier>()));

    [Fact]
    public void Changes_reports_whether_the_display_moves()
    {
        var plusOne = new[] { new StatModifier { Target = StatTarget.Skill, Delta = 1 } };
        Assert.True(StatMath.Changes("4+", plusOne));
        Assert.False(StatMath.Changes("2+", plusOne)); // already clamped — no visible change
        Assert.False(StatMath.Changes("4+", Array.Empty<StatModifier>()));
    }

    [Fact]
    public void Absolute_set_overrides_the_value()
    {
        var setSave = new[] { new StatModifier { Target = StatTarget.Save, SetValue = "3+" } };
        Assert.Equal("3+", StatMath.ApplyAll("4+", setSave));

        var setMove = new[] { new StatModifier { Target = StatTarget.Move, SetValue = "8\"" } };
        Assert.Equal("8\"", StatMath.ApplyAll("12\"", setMove));
    }

    [Fact]
    public void Absolute_set_wins_over_deltas_regardless_of_order()
    {
        var mods = new[]
        {
            new StatModifier { Target = StatTarget.Save, Delta = -1 },        // would worsen 4+ to 5+
            new StatModifier { Target = StatTarget.Save, SetValue = "3+" },   // but the set wins
        };
        Assert.Equal("3+", StatMath.ApplyAll("4+", mods));
    }

    [Fact]
    public void Absolute_set_is_reported_as_a_change()
    {
        var setSave = new[] { new StatModifier { Target = StatTarget.Save, SetValue = "3+" } };
        Assert.True(StatMath.Changes("4+", setSave));
        Assert.False(StatMath.Changes("3+", setSave)); // already 3+ — no visible change
    }
}
