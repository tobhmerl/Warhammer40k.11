using System.Text.Json;
using System.Text.RegularExpressions;
using Warhammer40k.Core.Play;
using static Warhammer40k.Tests.PlayViewParityTests;

namespace Warhammer40k.Tests;

public class PlayPhoneLayoutTests
{
    [Theory]
    [MemberData(nameof(PlayViewParityTests.Windows), MemberType = typeof(PlayViewParityTests))]
    public async Task Wrapped_overview_preserves_every_focused_action_in_each_window(BattlePhase phase, BattleTurn turn, bool withAuraAndBuff)
    {
        await using var host = await Session(Army(withAuraAndBuff));
        var battle = host.Read<BattleRoster>("_battle");
        var primary = battle.Units[0].Primary;
        await host.InvokeAsync("StepPart", primary, 1 - primary.TrackMax);
        await host.InvokeAsync("SelectPhase", phase);
        await host.InvokeAsync("SetTurn", turn);
        await host.SetAsync("_cp", 3);
        var expected = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < battle.Units.Count; index++)
        {
            await host.SetAsync("_activeCard", index);
            expected.UnionWith(NowTokens(host));
        }

        await host.InvokeAsync("ShowOverview");

        Assert.False(host.Read<bool>("_overviewMatrix"));
        Assert.Equal(expected.Order(), NowTokens(host).Order());
        var html = await host.HtmlAsync();
        Assert.Equal(1, Regex.Matches(html, "class=\"now-ribbon\"").Count);
        Assert.Equal(host.Read<int>("AvailableNowTotal"), Regex.Matches(html, "<button[^>]*class=\"now-action(?: [^\"]*)?\"").Count);
        Assert.DoesNotContain("class=\"mx-scroll\"", html);
        Assert.DoesNotContain("class=\"ov-block\"", html);
        Assert.DoesNotContain("class=\"unit-list\"", html);
        Assert.Contains("Now cards", html);
        Assert.Contains("Matrix (wide)", html);

        await host.InvokeAsync("SetOverviewMatrix", true);

        Assert.Equal(expected.Order(), MatrixTokens(host).Order());
        Assert.DoesNotContain("class=\"now-ribbon\"", await host.HtmlAsync());

        await host.InvokeAsync("SetOverviewMatrix", false);

        Assert.Equal(expected.Order(), NowTokens(host).Order());
        Assert.Contains("class=\"now-ribbon\"", await host.HtmlAsync());
    }

    [Fact]
    public async Task Matrix_is_opt_in_and_layout_changes_preserve_battle_state_and_setup()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        await using var host = await Session(api);
        await host.InvokeAsync("SelectPhase", BattlePhase.Shooting);
        await host.SetAsync("_cp", 2);
        var unit = Assert.Single(host.Read<BattleRoster>("_battle").Units);
        await host.InvokeAsync("StepPart", unit.Primary, -1);
        await host.InvokeAsync("ChooseShootingOption", unit, "Anti-VEHICLE 5+");
        var remaining = (int)Call(host.Component, "PartCurrent", unit.Primary)!;
        var rosterBefore = JsonSerializer.Serialize(api.Roster);
        var libraryBefore = JsonSerializer.Serialize(api.Library);
        Assert.DoesNotContain("Army overview layout", await host.HtmlAsync());

        await host.InvokeAsync("ShowOverview");
        Assert.False(host.Read<bool>("_overviewMatrix"));
        await host.InvokeAsync("SetOverviewMatrix", true);
        Assert.True(host.Read<bool>("_overviewMatrix"));
        Assert.Contains("class=\"mx-scroll\"", await host.HtmlAsync());
        await host.InvokeAsync("FocusReminder", unit);
        Assert.False(host.Read<bool>("_overview"));
        await host.InvokeAsync("ShowOverview");

        Assert.False(host.Read<bool>("_overviewMatrix"));
        Assert.Equal(2, host.Read<int>("_cp"));
        Assert.Equal(BattlePhase.Shooting, host.Read<BattlePhase>("_phase"));
        Assert.Equal(BattleTurn.Player, host.Read<BattleTurn>("_turn"));
        Assert.Equal(remaining, (int)Call(host.Component, "PartCurrent", unit.Primary)!);
        Assert.Equal("Anti-VEHICLE 5+", (string?)Call(host.Component, "SelectedChoice", unit));
        Assert.Equal(rosterBefore, JsonSerializer.Serialize(api.Roster));
        Assert.Equal(libraryBefore, JsonSerializer.Serialize(api.Library));
        Assert.Contains("Selected: Anti-VEHICLE 5+", await host.HtmlAsync());
    }

    [Fact]
    public async Task Wrapped_overview_retains_full_stratagem_conditions_in_the_sheet()
    {
        await using var host = await Session(PlayReadinessTests.ConfiguredFixture());
        await host.InvokeAsync("SelectPhase", BattlePhase.Fight);
        await host.InvokeAsync("SetTurn", BattleTurn.Opponent);
        await host.SetAsync("_cp", 2);
        await host.InvokeAsync("ShowOverview");
        var stratagem = Stratagem(host, "Microscarab Swarm");

        await host.InvokeAsync("OpenNowStratagem", stratagem);

        var html = await host.HtmlAsync();
        Assert.Contains("Microscarab Swarm", html);
        Assert.Contains("class=\"s-body now-detail\"", html);
        foreach (var field in new[] { "When", "Target", "Effect" })
        {
            var text = Assert.IsType<string>(Value(stratagem, field));
            Assert.NotEmpty(text);
            Assert.Contains(text, html);
        }
        if (Value(stratagem, "Restrictions") is string restrictions && !string.IsNullOrWhiteSpace(restrictions))
            Assert.Contains(restrictions, html);
        Assert.Equal(2, host.Read<int>("_cp"));
    }

    [Fact]
    public async Task Overview_layout_controls_do_not_expand_the_minimal_HUD()
    {
        await using var host = await Session(Army());
        await host.InvokeAsync("ShowOverview");
        foreach (var matrix in new[] { false, true })
        {
            await host.InvokeAsync("SetOverviewMatrix", matrix);
            var html = await host.HtmlAsync();
            var hud = Regex.Match(html, """<div\b[^>]*class="hud(?: [^"]*)?"[^>]*>(?<hud>.*?)</nav>""", RegexOptions.Singleline).Groups["hud"].Value;
            Assert.NotEmpty(hud);
            Assert.Equal(9, Regex.Matches(hud, @"<button\b").Count);
            Assert.Contains("Command points", hud);
            Assert.Contains("Whose turn it is", hud);
            Assert.Contains("Battle phases", hud);
            Assert.DoesNotContain("Matrix", hud);
            Assert.DoesNotContain("Next", hud);
            Assert.DoesNotContain("Round", hud);
            var phases = Regex.Matches(hud, """<button\b[^>]*class="phase(?:\s+on)?\s*"[^>]*>""").Cast<Match>().ToList();
            Assert.Equal(5, phases.Count);
            var selected = Assert.Single(phases, match => match.Value.Contains("aria-pressed=\"true\"", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("aria-label=\"Command\"", selected.Value);
            Assert.All(phases.Where(phase => phase != selected), phase => Assert.Contains("aria-pressed=\"false\"", phase.Value));
        }
    }
}
