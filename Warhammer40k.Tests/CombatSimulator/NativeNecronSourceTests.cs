using Warhammer40k.Api;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;
using Warhammer40k._11.Features.CombatSimulator.Adapters;
using Warhammer40k._11.Features.CombatSimulator.Domain;

namespace Warhammer40k.Tests.CombatSimulator;

/// <summary>
/// The native adapter must carry a unit's datasheet keywords into the simulator, otherwise Anti-[keyword]
/// can never match its target. Part of the removable Combat Simulator feature.
/// </summary>
public class NativeNecronSourceTests
{
    private static CombatUnit Map(string datasheetId, int models)
    {
        var catalogue = CatalogueProvider.LoadEmbedded();
        var roster = new Roster
        {
            Units = [new RosterUnit { Id = "r1", DatasheetId = datasheetId, ModelCount = models }],
        };
        var unit = BattleRoster.Build(roster, catalogue).Units.Single();
        return NativeNecronSource.FromBattleUnit(unit);
    }

    [Fact]
    public void Real_seed_warriors_carry_their_datasheet_keywords()
    {
        var combat = Map("necron-warriors", 10);

        Assert.Contains("Infantry", combat.Keywords, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Qualified_faction_keyword_also_contributes_its_bare_value()
    {
        var combat = Map("necron-warriors", 10);

        // The datasheet carries "Faction: Necrons"; both spellings are offered so Anti-X matches either way.
        Assert.Contains("Faction: Necrons", combat.Keywords, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Necrons", combat.Keywords, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Keywords_are_de_duplicated()
    {
        var combat = Map("necron-warriors", 10);

        Assert.Equal(combat.Keywords.Distinct(StringComparer.OrdinalIgnoreCase).Count(), combat.Keywords.Count);
    }
}
