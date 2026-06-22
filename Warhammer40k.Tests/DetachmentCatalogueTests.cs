using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins the 11th-edition detachment data: the Detachment-Points budget by points level, that only authored
/// detachments are selectable, and that Cryptek Conclave carries its smart effects.
/// </summary>
public class DetachmentCatalogueTests
{
    [Theory]
    [InlineData(500, 2)]
    [InlineData(1000, 2)]
    [InlineData(1001, 3)]
    [InlineData(1250, 3)]
    [InlineData(1500, 3)]
    [InlineData(2000, 3)]
    public void Budget_is_2_at_or_below_1000_otherwise_3(int points, int dp) =>
        Assert.Equal(dp, DetachmentCatalogue.Budget(points));

    [Fact]
    public void Only_enabled_detachments_are_selectable_and_Cryptek_Conclave_is_one()
    {
        Assert.All(DetachmentCatalogue.Selectable, d => Assert.True(d.Enabled));
        Assert.Contains(DetachmentCatalogue.Selectable, d => d.Id == "cryptek-conclave");
        // The unauthored detachments are kept but hidden from selection.
        Assert.Contains(DetachmentCatalogue.BuiltIn, d => d.Id == "hand-of-the-dynasty" && !d.Enabled);
    }

    [Fact]
    public void Cryptek_Conclave_carries_its_smart_effects()
    {
        var d = DetachmentCatalogue.FindById("cryptek-conclave")!;

        Assert.True(d.Enabled);
        Assert.Equal(2, d.DetachmentPoints);
        Assert.Contains(d.WeaponGrants, g => g.RequiresModelKeyword == "Cryptek"
            && g.WeaponClass == DetachmentWeaponClass.Ranged && g.Abilities.Contains("Assault"));
        Assert.Contains(d.WeaponChoices, c => c.RequiresModelKeyword == "Cryptek" && c.Options.Count == 5);
        Assert.NotEmpty(d.Rules);
    }
}
