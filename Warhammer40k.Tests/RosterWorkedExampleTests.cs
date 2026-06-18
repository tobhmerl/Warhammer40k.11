using Warhammer40k.Api;
using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Rosters;
using Warhammer40k.Core.Rosters.Validation;

namespace Warhammer40k.Tests;

/// <summary>
/// The §6 worked example, validated end-to-end against the <b>real</b> embedded seed: Hand of the Dynasty @2000
/// with an Overlord (Warlord + enhancement), two Necron Warriors (one led by the Overlord), and a C'tan Shard
/// of the Nightbringer. Proves the roster is Ready only when every R-rule passes, and that the Nightbringer
/// cannot be duplicated (R4) or made Warlord (R5).
/// </summary>
public class RosterWorkedExampleTests
{
    private static readonly CatalogueData Catalogue = CatalogueProvider.LoadEmbedded();
    private const string HandOfTheDynasty = "hand-of-the-dynasty";

    private static Datasheet Sheet(string id) =>
        Catalogue.FindById(id) ?? throw new InvalidOperationException($"Datasheet '{id}' missing from seed.");

    private static RosterUnit NewUnit(string id) => RosterUnit.FromDatasheet(Sheet(id));

    [Fact]
    public void Worked_example_is_ready_when_all_rules_pass()
    {
        var warriorsA = NewUnit("necron-warriors");
        var warriorsB = NewUnit("necron-warriors");

        var overlord = NewUnit("overlord");
        overlord.IsWarlord = true;
        overlord.AssignedEnhancementId = "dynastic-heirloom"; // Hand of the Dynasty has no authored points yet → permissive (0 pts)
        overlord.AttachedToRosterUnitId = warriorsA.Id;       // attach the Overlord to a Warriors unit

        var nightbringer = NewUnit("ctan-shard-of-the-nightbringer");

        var roster = new Roster
        {
            Name = "Worked Example",
            Faction = Roster.NecronsFaction,
            PointsLimit = 2000,
            DetachmentId = HandOfTheDynasty,
            Units = [overlord, warriorsA, warriorsB, nightbringer],
        };

        var result = new RosterValidator().Validate(roster, Catalogue);

        // Overlord 85 + Warriors 90 + Warriors 90 + Nightbringer 340 (+ enhancement 0) = 605.
        Assert.Equal(605, result.TotalPoints);
        Assert.True(result.TotalPoints <= roster.PointsLimit);
        Assert.Single(roster.Units, u => u.IsWarlord);                             // exactly one Warlord
        Assert.True(roster.Units.Count(u => !string.IsNullOrEmpty(u.AssignedEnhancementId)) <= 3); // ≤3 enhancements
        Assert.False(result.HasMessageFrom("R3"));                                 // no copy-cap breach
        Assert.False(result.HasMessageFrom("R4"));                                 // no Epic Hero breach
        Assert.Empty(result.Errors);                                               // Ready only when no errors
        Assert.True(result.IsReady);
    }

    [Fact]
    public void Second_nightbringer_breaks_readiness_via_R4()
    {
        var overlord = NewUnit("overlord");
        overlord.IsWarlord = true;

        var roster = new Roster
        {
            Name = "Two Nightbringers",
            Faction = Roster.NecronsFaction,
            PointsLimit = 2000,
            DetachmentId = HandOfTheDynasty,
            Units = [overlord, NewUnit("ctan-shard-of-the-nightbringer"), NewUnit("ctan-shard-of-the-nightbringer")],
        };

        var result = new RosterValidator().Validate(roster, Catalogue);

        Assert.True(result.HasMessageFrom("R4"));
        Assert.False(result.IsReady);
    }

    [Fact]
    public void Nightbringer_cannot_be_warlord_via_R5()
    {
        var nightbringer = NewUnit("ctan-shard-of-the-nightbringer");
        nightbringer.IsWarlord = true; // the only Warlord, but C'tan are not eligible

        var roster = new Roster
        {
            Name = "Star God Warlord",
            Faction = Roster.NecronsFaction,
            PointsLimit = 2000,
            DetachmentId = HandOfTheDynasty,
            Units = [nightbringer, NewUnit("necron-warriors")],
        };

        var result = new RosterValidator().Validate(roster, Catalogue);

        Assert.Contains(result.Errors, m => m.RuleId == "R5" && m.Text.Contains("not eligible"));
        Assert.False(result.IsReady);
    }
}
