using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Tests;

public class RuleUsageTests
{
    [Theory]
    [InlineData("Once per battle, use this ability.", true)]
    [InlineData("You cannot use this more than once per battle.", true)]
    [InlineData("ONCE PER BATTLE", true)]
    [InlineData("Once\nper\u00a0battle.", true)]
    [InlineData("Once per battle round, use this ability.", false)]
    [InlineData("Once per battle-round.", false)]
    [InlineData("Once per battle\u2011round.", false)]
    [InlineData("Once per **battle** round.", false)]
    [InlineData("Once per battle\nround.", false)]
    [InlineData("Once per turn.", false)]
    [InlineData("Once per phase.", false)]
    [InlineData("Once per battleship.", false)]
    [InlineData("Once per battle round. This separate effect is once per battle.", true)]
    [InlineData("Once per battle. Another option is once per battle round.", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_a_complete_battle_period_uses_the_battle_tracker(string? text, bool expected) =>
        Assert.Equal(expected, RuleUsage.IsOncePerBattle(text));

    [Theory]
    [InlineData("Once per battle round.", "Once per battle round · check usage")]
    [InlineData("Once per turn.", "Once per turn · check usage")]
    [InlineData("Once per phase.", "Once per phase · check usage")]
    [InlineData("Once per **battle** round. Once per battle round.", "Once per battle round · check usage")]
    [InlineData("Once per battle.", null)]
    [InlineData("No limit here.", null)]
    public void Untracked_periods_have_a_concise_manual_hint(string text, string? expected) =>
        Assert.Equal(expected, RuleUsage.ManualLimitHint(text));

    [Fact]
    public async Task Molecular_Erosion_is_not_consumed_for_the_whole_battle()
    {
        var api = MolecularFixture();
        await using var host = await PlayViewParityTests.Session(api);
        var unit = host.Read<BattleRoster>("_battle").Units.Single(unit => unit.Primary.Datasheet.Keywords.Contains("Monster"));
        await host.InvokeAsync("FocusReminder", unit);
        await host.SetAsync("_cp", 2);
        var stratagem = PlayViewParityTests.Stratagem(host, "Molecular Erosion");
        await host.InvokeAsync("OpenNowStratagem", stratagem);
        var html = await host.HtmlAsync();
        Assert.Contains("Once per battle round · check usage", html);
        Assert.DoesNotContain("Used this battle — hide", html);
        await host.InvokeAsync("UseNowStratagem");
        Assert.Equal(1, host.Read<int>("_cp"));
        Assert.DoesNotContain("S|molecular-erosion", host.Read<HashSet<string>>("_usedOncePerBattle"));
        await host.InvokeAsync("SelectPhase", BattlePhase.Fight);
        await host.InvokeAsync("SelectPhase", BattlePhase.Command);
        Assert.Contains("S|Pantheon of Woe|molecular-erosion|" + unit.Id, PlayViewParityTests.NowTokens(host));
    }

    [Fact]
    public async Task A_legacy_saved_per_round_spent_marker_does_not_hide_the_action_in_any_view()
    {
        var api = MolecularFixture();
        await using var host = await PlayViewParityTests.Session(api);
        var unit = host.Read<BattleRoster>("_battle").Units.Single(unit => unit.Primary.Datasheet.Keywords.Contains("Monster"));
        await host.InvokeAsync("ApplyBattleState", new BattleSessionState
        {
            CommandPoints = 2, Phase = BattlePhase.Command, Turn = BattleTurn.Player, FocusMode = true,
            ActiveUnitId = unit.Id, UsedOncePerBattle = ["S|molecular-erosion"],
        });
        var token = "S|Pantheon of Woe|molecular-erosion|" + unit.Id;
        Assert.Contains(token, PlayViewParityTests.NowTokens(host));
        await host.SetAsync("_cardSwipe", false);
        Assert.Contains(token, PlayViewParityTests.NowTokens(host));
        await host.InvokeAsync("ShowOverview");
        Assert.Contains(token, PlayViewParityTests.MatrixTokens(host));
    }

    [Theory]
    [InlineData("battle round")]
    [InlineData("turn")]
    [InlineData("phase")]
    public async Task Non_battle_ability_periods_ignore_legacy_battle_spent_markers(string period)
    {
        var api = PlayReadinessTests.ConfiguredFixture();
        var ability = api.Catalogue.FindById("immortals")!.Abilities.Single(ability => ability.Name == "Implacable Eradication");
        ability.Text = $"Once per {period}, use this ability.";
        await using var host = await PlayViewParityTests.Session(api);
        var unit = host.Read<BattleRoster>("_battle").Units[0];
        await host.InvokeAsync("SetOncePerBattleUsed", "A|" + unit.Id + "|" + ability.Name, true);
        var token = "A|" + AbilityScheduleKeys.ForUnitAbility("immortals", ability.Name) + "|" + unit.Id;
        Assert.Contains(token, PlayViewParityTests.NowTokens(host));
        Assert.Contains("Once per " + period + " · check usage", await host.HtmlAsync());
        await host.SetAsync("_cardSwipe", false);
        Assert.Contains(token, PlayViewParityTests.NowTokens(host));
        await host.InvokeAsync("ShowOverview");
        Assert.Contains(token, PlayViewParityTests.MatrixTokens(host));
    }

    [Fact]
    public async Task A_genuine_once_per_battle_ability_still_honors_saved_spent_state()
    {
        var api = PlayReadinessTests.ConfiguredFixture();
        var ability = api.Catalogue.FindById("immortals")!.Abilities.Single(ability => ability.Name == "Implacable Eradication");
        ability.Text = "Once per battle, use this ability.";
        await using var host = await PlayViewParityTests.Session(api);
        var unit = host.Read<BattleRoster>("_battle").Units[0];
        await host.InvokeAsync("SetOncePerBattleUsed", "A|" + unit.Id + "|" + ability.Name, true);
        var token = "A|" + AbilityScheduleKeys.ForUnitAbility("immortals", ability.Name) + "|" + unit.Id;
        Assert.DoesNotContain(token, PlayViewParityTests.NowTokens(host));
        await host.SetAsync("_cardSwipe", false);
        Assert.DoesNotContain(token, PlayViewParityTests.NowTokens(host));
        await host.InvokeAsync("ShowOverview");
        Assert.DoesNotContain(token, PlayViewParityTests.MatrixTokens(host));
    }

    private static TestApiClient MolecularFixture()
    {
        var api = PlayReadinessTests.ConfiguredFixture();
        api.Roster.DetachmentId = "pantheon-of-woe";
        api.Roster.DetachmentIds = ["pantheon-of-woe"];
        api.Roster.Units.Single(unit => unit.DatasheetId == "plasmancer").AssignedEnhancementId = null;
        api.Roster.Units.Add(RosterUnit.FromDatasheet(api.Catalogue.Datasheets.First(sheet => sheet.Keywords.Contains("Monster"))));
        PlayViewParityTests.Schedule(api, AbilityScheduleKeys.ForDetachmentStratagem("pantheon-of-woe", "molecular-erosion"));
        return api;
    }
}
