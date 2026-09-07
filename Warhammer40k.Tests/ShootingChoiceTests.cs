using System.Reflection;
using System.Text.RegularExpressions;
using Warhammer40k.Api;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;
using Warhammer40k._11.Pages;

namespace Warhammer40k.Tests;

public class ShootingChoiceTests
{
    [Theory]
    [InlineData(BattlePhase.Command, BattleTurn.Player, 0)]
    [InlineData(BattlePhase.Movement, BattleTurn.Player, 0)]
    [InlineData(BattlePhase.Shooting, BattleTurn.Player, 7)]
    [InlineData(BattlePhase.Charge, BattleTurn.Player, 0)]
    [InlineData(BattlePhase.Fight, BattleTurn.Player, 0)]
    [InlineData(BattlePhase.Command, BattleTurn.Opponent, 0)]
    [InlineData(BattlePhase.Movement, BattleTurn.Opponent, 0)]
    [InlineData(BattlePhase.Shooting, BattleTurn.Opponent, 0)]
    [InlineData(BattlePhase.Charge, BattleTurn.Opponent, 0)]
    [InlineData(BattlePhase.Fight, BattleTurn.Opponent, 0)]
    public void Enabled_Atomic_options_are_offered_only_in_the_authored_shooting_window(BattlePhase phase, BattleTurn turn, int expected)
    {
        var api = Fixture(enableAtomic: true);
        api.Roster.AbilitySchedules = api.Library.EffectiveFor(api.Roster);
        var battle = BattleRoster.Build(api.Roster, api.Catalogue);

        Assert.Equal(expected, battle.ShootingOptionsFor(Assert.Single(battle.Units), phase, turn).Count);
    }

    [Fact]
    public void Assigning_Atomic_does_not_silently_enable_its_extra_options()
    {
        var api = Fixture(enableAtomic: false);
        var battle = BattleRoster.Build(api.Roster, api.Catalogue);
        var group = Assert.Single(battle.Units);

        Assert.Equal(5, battle.ShootingOptionsFor(group, BattlePhase.Shooting, BattleTurn.Player).Count);
        Assert.Empty(battle.ExtraShootingOptions(group));
        Assert.True(group.CombinedAbilities.Single(a => a.Ability.Name == "Atomic Disintegrators").IsShootingChoice);
    }

    [Fact]
    public void Atomic_never_uses_the_generic_bracketed_keyword_toggle()
    {
        var api = Fixture(enableAtomic: true);
        api.Roster.AbilitySchedules = api.Library.EffectiveFor(api.Roster);
        var battle = BattleRoster.Build(api.Roster, api.Catalogue);
        var ability = Assert.Single(battle.Units).CombinedAbilities.Single(a => a.IsShootingChoice);
        var method = typeof(PlaySession).GetMethod("HasEffectKeywords", BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.False((bool)method.Invoke(null, [ability])!);
    }

    [Fact]
    public async Task Atomic_is_absent_from_Fight_actions_and_lower_reference_blocks_even_with_a_legacy_Fight_schedule()
    {
        var api = Fixture(enableAtomic: false);
        api.Library.GetOrCreate(AbilityScheduleKeys.ForEnhancement("atomic-disintegrators"))
            .SetWindow(BattlePhase.Fight, BattleTurn.Opponent, true);
        await using var host = await PlayHost(api);
        await host.InvokeAsync("SelectPhase", BattlePhase.Fight);
        await host.InvokeAsync("SetTurn", BattleTurn.Opponent);
        var html = await host.HtmlAsync();

        Assert.DoesNotContain("Atomic Disintegrators", html);
        Assert.DoesNotContain("now-action ability choice", html);
        Assert.DoesNotContain("class=\"abilities\"", html);
        Assert.DoesNotContain("class=\"shoot-choice\"", html);
    }

    [Fact]
    public async Task Shooting_choice_and_Atomic_source_are_above_the_unit_card()
    {
        var api = Fixture(enableAtomic: true);
        await using var host = await PlayHost(api);
        await host.InvokeAsync("SelectPhase", BattlePhase.Shooting);
        var html = await host.HtmlAsync();

        Assert.Contains("now-action ability choice", html);
        Assert.Contains("Atomic Disintegrators", html);
        Assert.True(html.IndexOf("Atomic Disintegrators", StringComparison.Ordinal) < html.IndexOf("class=\"unit-list\"", StringComparison.Ordinal));
        Assert.DoesNotContain("class=\"shoot-choice\"", html);

        var unit = Assert.Single(host.Read<BattleRoster>("_battle").Units);
        await host.InvokeAsync("OpenShootingCard", unit);
        html = await host.HtmlAsync();
        Assert.Equal(7, Regex.Matches(html, "class=\"sc-opt(?: |\")").Count);
        Assert.Contains("Anti-MONSTER 5+", html);
        Assert.Contains("Anti-VEHICLE 5+", html);
        Assert.DoesNotContain("Active now — apply", html);
    }

    [Fact]
    public async Task Choosing_an_option_replaces_the_previous_one_and_expires_on_phase_or_turn_change()
    {
        var api = Fixture(enableAtomic: true);
        await using var host = await PlayHost(api);
        await host.InvokeAsync("SelectPhase", BattlePhase.Shooting);
        var unit = Assert.Single(host.Read<BattleRoster>("_battle").Units);
        await host.InvokeAsync("ChooseShootingOption", unit, "Anti-MONSTER 5+");
        await host.InvokeAsync("ChooseShootingOption", unit, "Anti-VEHICLE 5+");
        var choices = host.Read<Dictionary<string, string>>("_shootingChoice");
        Assert.Equal("Anti-VEHICLE 5+", Assert.Single(choices).Value);

        await host.InvokeAsync("SelectPhase", BattlePhase.Fight);
        Assert.Empty(choices);
        await host.InvokeAsync("ToggleChoice", unit, "Anti-MONSTER 5+");
        Assert.Empty(choices);
        await host.InvokeAsync("SelectPhase", BattlePhase.Shooting);
        await host.InvokeAsync("ToggleChoice", unit, "Heavy");
        await host.InvokeAsync("SetTurn", BattleTurn.Opponent);
        Assert.Empty(choices);
    }

    [Fact]
    public async Task Disabled_Atomic_remains_a_clear_setup_note_not_an_enabled_effect()
    {
        var api = Fixture(enableAtomic: false);
        await using var host = await PlayHost(api);
        await host.InvokeAsync("SelectPhase", BattlePhase.Shooting);
        var unit = Assert.Single(host.Read<BattleRoster>("_battle").Units);
        Assert.Contains("Atomic Disintegrators · needs setup", await host.HtmlAsync());
        await host.InvokeAsync("OpenShootingCard", unit);
        var html = await host.HtmlAsync();
        Assert.Equal(5, Regex.Matches(html, "class=\"sc-opt(?: |\")").Count);
        Assert.Contains("Enable this enhancement's extra shooting options in setup", html);
        Assert.False(api.Library.Find(AbilityScheduleKeys.ForEnhancement("atomic-disintegrators"))?.ApplyToUnit ?? false);
    }

    [Fact]
    public async Task Matrix_can_open_the_same_shooting_choice_without_a_generic_Atomic_column()
    {
        var api = Fixture(enableAtomic: true);
        await using var host = await PlayHost(api);
        await host.InvokeAsync("SelectPhase", BattlePhase.Shooting);
        await host.InvokeAsync("ShowOverview");
        var html = await host.HtmlAsync();
        Assert.Contains("Shooting choice", html);
        Assert.Contains("Technosorcerous Augmentations", html);
        Assert.DoesNotContain("Atomic Disintegrators", html);
    }

    [Fact]
    public async Task Unscheduled_rules_are_reference_only_inside_Now_and_army_rules_are_not_duplicated()
    {
        var api = Fixture(enableAtomic: false);
        api.Library.GetOrCreate(AbilityScheduleKeys.ForArmyRule("Reanimation Protocols"))
            .SetWindow(BattlePhase.Command, BattleTurn.Player, true);
        await using var host = await PlayHost(api);
        var html = await host.HtmlAsync();
        Assert.Contains("class=\"now-reference\"", html);
        Assert.True(html.IndexOf("Unscheduled rules", StringComparison.Ordinal) < html.IndexOf("class=\"unit-list\"", StringComparison.Ordinal));
        Assert.DoesNotContain("class=\"army-now\"", html);
        Assert.DoesNotContain("class=\"abilities\"", html);
    }

    [Fact]
    public async Task Setup_exposes_Atomic_enable_flag_without_a_generic_timing_grid()
    {
        var api = Fixture(enableAtomic: false);
        await using var host = await ComponentTestHost<RosterEditor>.CreateAsync(api, "rosters/" + api.Roster.Id, new() { ["Id"] = api.Roster.Id });
        var bearer = api.Roster.Units.Single(u => u.DatasheetId == "plasmancer");
        await host.InvokeAsync("OpenConfig", bearer);
        var html = await host.HtmlAsync();
        Assert.Contains("Enable extra shooting options", html);
        Assert.Contains("No separate timing or keyword chip is needed", html);
    }

    private static Task<ComponentTestHost<PlaySession>> PlayHost(TestApiClient api) =>
        ComponentTestHost<PlaySession>.CreateAsync(api, "play/" + api.Roster.Id, new() { ["Id"] = api.Roster.Id });

    internal static TestApiClient Fixture(bool enableAtomic)
    {
        var catalogue = CatalogueProvider.LoadEmbedded();
        var immortals = RosterUnit.FromDatasheet(catalogue.FindById("immortals")!);
        var plasmancer = RosterUnit.FromDatasheet(catalogue.FindById("plasmancer")!);
        plasmancer.AttachedToRosterUnitId = immortals.Id;
        plasmancer.AssignedEnhancementId = "atomic-disintegrators";
        plasmancer.IsWarlord = true;
        var api = new TestApiClient
        {
            Catalogue = catalogue,
            Roster = new Roster
            {
                Id = "test-roster", Name = "Immortals and Plasmancer", PointsLimit = 2000,
                DetachmentId = "cryptek-conclave", DetachmentIds = ["cryptek-conclave"], Units = [immortals, plasmancer],
            },
        };
        if (enableAtomic)
            api.Library.GetOrCreate(AbilityScheduleKeys.ForEnhancement("atomic-disintegrators")).ApplyToUnit = true;
        return api;
    }
}
