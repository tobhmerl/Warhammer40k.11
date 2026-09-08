using System.Reflection;
using Warhammer40k.Api;
using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;
using Warhammer40k._11.Pages;

namespace Warhammer40k.Tests;

public class StratagemTargetingTests
{
    private static readonly string[] Vocabulary =
        ["Necrons", "Cryptek", "Infantry", "Character", "Monster", "Vehicle", "Titanic", "Immortals", "Necron Warriors", "Destroyer Cult", "Epic Hero"];

    [Theory]
    [InlineData("One CRYPTEK INFANTRY unit.", true, "Cryptek", "Infantry")]
    [InlineData("One CRYPTEK INFANTRY unit.", false, "Cryptek")]
    [InlineData("One CRYPTEK INFANTRY unit.", false, "Infantry")]
    [InlineData("One CRYPTEK INFANTRY unit.", true, "cryptek", "INFANTRY")]
    [InlineData("That MONSTER/VEHICLE unit.", true, "Monster")]
    [InlineData("That MONSTER/VEHICLE unit.", true, "Vehicle")]
    [InlineData("That MONSTER/VEHICLE unit.", false, "Infantry")]
    [InlineData("One NECRONS MONSTER/VEHICLE unit.", true, "Faction: Necrons", "Vehicle")]
    [InlineData("One NECRONS MONSTER/VEHICLE unit.", false, "Vehicle")]
    [InlineData("One NECRONS MONSTER/VEHICLE unit.", false, "Necrons", "Infantry")]
    [InlineData("One IMMORTALS/NECRON WARRIORS unit.", true, "Immortals")]
    [InlineData("One IMMORTALS/NECRON WARRIORS unit.", true, "Necron Warriors")]
    [InlineData("One IMMORTALS/NECRON WARRIORS unit.", false, "Necrons", "Warriors")]
    [InlineData("One NECRONS DESTROYER CULT unit.", true, "Faction: Necrons", "Destroyer Cult")]
    [InlineData("One NECRONS INFANTRY CHARACTER model.", true, "Faction: Necrons", "Infantry", "Character")]
    [InlineData("One NECRONS INFANTRY CHARACTER model.", false, "Faction: Necrons", "Infantry")]
    [InlineData("One friendly unit (excluding TITANIC units).", false, "Titanic")]
    [InlineData("One friendly unit (excluding TITANIC units).", true, "Vehicle")]
    [InlineData("One NECRONS unit (excluding MONSTER/VEHICLE units).", false, "Necrons", "Vehicle")]
    [InlineData("One NECRONS unit (excluding MONSTER/VEHICLE units).", true, "Necrons", "Infantry")]
    [InlineData("One CHARACTER model (excluding EPIC HERO models).", false, "Character", "Epic Hero")]
    [InlineData("One CHARACTER model (excluding EPIC HERO models).", true, "Character")]
    [InlineData("One friendly unit that is eligible to fight.", true, "Infantry")]
    [InlineData("That unit or model.", true, "Vehicle")]
    [InlineData("One IMMORTALS or NECRON WARRIORS unit.", true, "Necron Warriors")]
    public void Matches_keyword_requirements_without_losing_multiword_names(string target, bool expected, params string[] keywords) =>
        Assert.Equal(expected, StratagemTargeting.AppliesTo(target, keywords, Vocabulary));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void No_target_keyword_requirement_does_not_exclude_a_unit(string? target) =>
        Assert.True(StratagemTargeting.AppliesTo(target, []));

    [Fact]
    public void Custom_catalogue_multiword_keywords_are_preserved()
    {
        string[] known = ["Cryptek", "Royal Court Guard", "Infantry"];
        Assert.True(StratagemTargeting.AppliesTo("One CRYPTEK ROYAL COURT GUARD unit.", ["Cryptek", "Royal Court Guard"], known));
        Assert.False(StratagemTargeting.AppliesTo("One CRYPTEK ROYAL COURT GUARD unit.", ["Cryptek", "Royal", "Court", "Guard"], known));
    }

    [Fact]
    public void Microscarab_Swarm_matches_the_scheduled_Immortals_and_Plasmancer_group_in_both_Fight_turns()
    {
        var catalogue = CatalogueProvider.LoadEmbedded();
        var immortals = RosterUnit.FromDatasheet(catalogue.FindById("immortals")!);
        var plasmancer = RosterUnit.FromDatasheet(catalogue.FindById("plasmancer")!);
        plasmancer.AttachedToRosterUnitId = immortals.Id;
        var detachment = DetachmentCatalogue.FindById("cryptek-conclave")!;
        var stratagem = detachment.Stratagems.Single(s => s.Id == "microscarab-swarm");
        var roster = new Roster { DetachmentIds = [detachment.Id], Units = [immortals, plasmancer] };
        var schedule = roster.GetOrCreateSchedule(AbilityScheduleKeys.ForDetachmentStratagem(detachment.Id, stratagem.Id));
        schedule.SetWindow(BattlePhase.Fight, BattleTurn.Player, true);
        schedule.SetWindow(BattlePhase.Fight, BattleTurn.Opponent, true);
        schedule.SetWindow(BattlePhase.Shooting, BattleTurn.Opponent, true);
        var battle = BattleRoster.Build(roster, catalogue);
        var group = Assert.Single(battle.Units);
        var keywords = group.Parts.SelectMany(p => p.Datasheet.Keywords);

        Assert.True(battle.DetachmentStratagemUsable(detachment, stratagem, BattlePhase.Fight, BattleTurn.Player));
        Assert.True(battle.DetachmentStratagemUsable(detachment, stratagem, BattlePhase.Fight, BattleTurn.Opponent));
        Assert.True(battle.DetachmentStratagemUsable(detachment, stratagem, BattlePhase.Shooting, BattleTurn.Opponent));
        Assert.False(battle.DetachmentStratagemUsable(detachment, stratagem, BattlePhase.Shooting, BattleTurn.Player));
        Assert.True(StratagemTargeting.AppliesTo(stratagem.Target, keywords, catalogue.Datasheets.SelectMany(s => s.Keywords)));
        Assert.False(StratagemTargeting.AppliesTo(stratagem.Target, group.Primary.Datasheet.Keywords, catalogue.Datasheets.SelectMany(s => s.Keywords)));
        Assert.True(UiAppliesTo(catalogue, group, stratagem.Target));
    }

    [Fact]
    public void Crushing_Impact_matches_an_actual_vehicle_in_the_final_UI_filter()
    {
        var catalogue = CatalogueProvider.LoadEmbedded();
        var unit = RosterUnit.FromDatasheet(catalogue.FindById("canoptek-doomstalker")!);
        var group = Assert.Single(BattleRoster.Build(new Roster { Units = [unit] }, catalogue).Units);
        var stratagem = CoreStratagemCatalogue.All.Single(s => s.Name == "Crushing Impact");

        Assert.True(UiAppliesTo(catalogue, group, stratagem.Target));
    }

    [Theory]
    [InlineData("immortals")]
    [InlineData("necron-warriors")]
    public void Will_of_the_Conqueror_matches_each_alternative_in_the_final_UI_filter(string datasheetId)
    {
        var catalogue = CatalogueProvider.LoadEmbedded();
        var unit = RosterUnit.FromDatasheet(catalogue.FindById(datasheetId)!);
        var group = Assert.Single(BattleRoster.Build(new Roster { Units = [unit] }, catalogue).Units);
        var stratagem = DetachmentCatalogue.FindById("hand-of-the-dynasty")!.Stratagems.Single(s => s.Id == "will-of-the-conqueror");

        Assert.True(UiAppliesTo(catalogue, group, stratagem.Target));
    }

    private static bool UiAppliesTo(CatalogueData catalogue, BattleUnit unit, string target)
    {
        var page = new PlaySession();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(PlaySession).GetField("_catalogue", flags)!.SetValue(page, catalogue);
        return (bool)typeof(PlaySession).GetMethod("StratagemAppliesTo", flags)!.Invoke(page, [unit, target])!;
    }
}
