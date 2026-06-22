using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins the 11th-edition detachment data: which detachments are selectable, Cryptek Conclave's DP cost and
/// smart effects, and the Detachment-Points budget by points level.
/// </summary>
public class DetachmentCatalogueTests
{
    [Theory]
    [InlineData(500, 2)]
    [InlineData(1000, 2)]
    [InlineData(1250, 3)]
    [InlineData(1500, 3)]
    [InlineData(2000, 3)]
    [InlineData(3000, 3)]
    public void Budget_is_2_at_or_below_1000_else_3(int points, int expected) =>
        Assert.Equal(expected, DetachmentCatalogue.Budget(points));

    [Fact]
    public void Only_detachments_with_authored_rules_are_selectable()
    {
        Assert.All(DetachmentCatalogue.Selectable, d => Assert.True(d.Enabled));
        Assert.Contains(DetachmentCatalogue.Selectable, d => d.Id == "cryptek-conclave");
        Assert.Contains(DetachmentCatalogue.Selectable, d => d.Id == "hand-of-the-dynasty");
        // The others are still disabled (hidden) until their rules are authored.
        Assert.DoesNotContain(DetachmentCatalogue.Selectable, d => d.Id == "skyshroud-spearhead");
    }

    [Fact]
    public void Cryptek_Conclave_is_2DP_with_a_Cryptek_Assault_grant_and_shooting_choices()
    {
        var cryptek = DetachmentCatalogue.FindById("cryptek-conclave");

        Assert.NotNull(cryptek);
        Assert.True(cryptek!.Enabled);
        Assert.Equal(2, cryptek.DetachmentPoints);

        Assert.Contains(cryptek.WeaponGrants, g =>
            g.Keywords.Contains("Cryptek")
            && g.Scope == GrantScope.Model
            && g.WeaponClass == DetachmentWeaponClass.Ranged
            && g.Abilities.Contains("Assault"));

        var choice = Assert.Single(cryptek.WeaponChoices);
        Assert.Equal("Cryptek", choice.RequiresModelKeyword);
        Assert.Equal(5, choice.Options.Count);
        Assert.Contains("Ignores Cover", choice.Options);
    }

    [Fact]
    public void Hand_of_the_Dynasty_is_1DP_DYNASTY_with_a_unit_scoped_Assault_grant()
    {
        var hotd = DetachmentCatalogue.FindById("hand-of-the-dynasty");

        Assert.NotNull(hotd);
        Assert.True(hotd!.Enabled);
        Assert.Equal(1, hotd.DetachmentPoints);
        Assert.Contains("Dynasty", hotd.Tags);

        var grant = Assert.Single(hotd.WeaponGrants);
        Assert.Equal(GrantScope.Unit, grant.Scope);
        Assert.Contains("Immortals", grant.Keywords);
        Assert.Contains("Necron Warriors", grant.Keywords);
        Assert.Contains("Assault", grant.Abilities);
        Assert.Empty(hotd.WeaponChoices); // no selectable shooting ability
    }
}
