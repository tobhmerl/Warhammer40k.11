using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Routing;
using Warhammer40k.Core.Play;
using Warhammer40k._11.Pages;

namespace Warhammer40k.Tests;

public class PlayReadinessUiTests
{
    [Fact]
    public async Task Timing_only_save_failure_keeps_Save_enabled_and_prevents_a_readiness_launch()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: false);
        api.SaveLibrary = _ => Task.FromException<ScheduleLibrary>(new HttpRequestException("Offline"));
        await using var host = await Editor(api);
        await host.InvokeAsync("OpenConfig", api.Roster.Units.Single(unit => unit.DatasheetId == "plasmancer"));
        await host.InvokeAsync("SetApplied", AbilityScheduleKeys.ForEnhancement("atomic-disintegrators"), true);
        Assert.Matches(@">\s*Save\s*</button>", SaveButton(await host.HtmlAsync()));
        await host.InvokeAsync("SaveAsync");
        var html = await host.HtmlAsync();
        Assert.Contains("Could not save your changes", html);
        Assert.DoesNotContain("disabled", SaveButton(html));
        Assert.True(host.Read<bool>("_libraryDirty"));

        await host.InvokeAsync("CheckPlayAsync");
        html = await host.HtmlAsync();
        Assert.Null(host.Read<PlayReadinessResult?>("_readiness"));
        Assert.DoesNotContain("Play with warnings</a>", html);
        Assert.DoesNotContain("Start Play</a>", html);

        api.SaveLibrary = null;
        await host.InvokeAsync("SaveAsync");
        Assert.False(host.Read<bool>("_libraryDirty"));
        Assert.Matches(@">\s*Saved\s*</button>", SaveButton(await host.HtmlAsync()));
    }

    [Fact]
    public async Task In_flight_save_is_visible_and_its_response_does_not_erase_newer_timing_edits()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: false);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var complete = new TaskCompletionSource<ScheduleLibrary>(TaskCreationOptions.RunContinuationsAsynchronously);
        ScheduleLibrary? snapshot = null;
        api.SaveLibrary = library =>
        {
            snapshot = JsonSerializer.Deserialize<ScheduleLibrary>(JsonSerializer.Serialize(library));
            entered.TrySetResult();
            return complete.Task;
        };
        await using var host = await Editor(api);
        await host.InvokeAsync("SetApplied", AbilityScheduleKeys.ForEnhancement("atomic-disintegrators"), true);
        var saving = host.InvokeAsync("SaveAsync");
        var newKey = AbilityScheduleKeys.ForUnitAbility("immortals", "Implacable Eradication");
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var html = await host.HtmlAsync();
            Assert.Contains("Saving", SaveButton(html));
            Assert.Contains("disabled", SaveButton(html));
            await host.InvokeAsync("ToggleWindow", newKey, BattlePhase.Fight, BattleTurn.Player, true);
        }
        finally
        {
            complete.TrySetResult(snapshot ?? new ScheduleLibrary());
        }
        await saving;
        Assert.True(host.Read<ScheduleLibrary>("_library").Find(newKey)!.Covers(BattlePhase.Fight, BattleTurn.Player));
        Assert.True(host.Read<bool>("_libraryDirty"));
        api.SaveLibrary = null;
        await host.InvokeAsync("SaveAsync");
    }

    [Fact]
    public async Task Failed_load_is_not_an_editable_empty_library_and_retry_preserves_the_fix_link()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: false);
        api.LoadLibrary = () => Task.FromException<ScheduleLibrary>(new HttpRequestException("503"));
        var bearer = api.Roster.Units.Single(unit => unit.DatasheetId == "plasmancer");
        var key = AbilityScheduleKeys.ForEnhancement("atomic-disintegrators");
        var path = "rosters/" + api.Roster.Id + "?unit=" + bearer.Id + "&rule=" + Uri.EscapeDataString(key);
        await using var host = await ComponentTestHost<RosterEditor>.CreateAsync(api, path, new() { ["Id"] = api.Roster.Id });
        var html = await host.HtmlAsync();
        Assert.Contains("Retry loading setup", html);
        Assert.DoesNotContain("class=\"editor-actions\"", html);
        Assert.Equal(0, api.LibrarySaveCount);
        api.LoadLibrary = null;
        await host.InvokeAsync("LoadAsync");
        html = await host.HtmlAsync();
        Assert.Contains("Enable extra shooting options", html);
        Assert.Contains("No separate timing", html);
    }

    [Fact]
    public async Task Failed_Play_load_shows_retry_not_Nothing_scheduled()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        api.LoadLibrary = () => Task.FromException<ScheduleLibrary>(new HttpRequestException("503"));
        await using var host = await Session(api);
        var html = await host.HtmlAsync();
        Assert.Contains("Retry loading Play", html);
        Assert.DoesNotContain("Nothing scheduled for this battle window", html);
        Assert.DoesNotContain("class=\"now-ribbon\"", html);
    }

    [Fact]
    public async Task Failed_picker_check_does_not_offer_Start_or_Play_with_warnings()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        api.LoadLibrary = () => Task.FromException<ScheduleLibrary>(new HttpRequestException("503"));
        await using var host = await ComponentTestHost<Warhammer40k._11.Pages.Play>.CreateAsync(api, "play");
        await host.InvokeAsync("CheckPlayAsync", api.Roster);
        var html = await host.HtmlAsync();
        Assert.Contains("Retry check", html);
        Assert.DoesNotContain("Play with warnings</a>", html);
        Assert.DoesNotContain("Start Play</a>", html);
    }

    [Fact]
    public async Task Verified_setup_has_a_short_success_result_and_a_launch_link()
    {
        var api = PlayReadinessTests.ConfiguredFixture();
        await using var host = await Editor(api);
        await host.InvokeAsync("CheckPlayAsync");
        var html = await host.HtmlAsync();
        Assert.Contains("Play ready", html);
        Assert.Contains("List legal", html);
        Assert.Contains("Start Play</a>", html);
        Assert.Contains("Five phases × both turns", html);
    }

    [Fact]
    public async Task Timing_fix_link_opens_the_correct_section_and_rule()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        var key = AbilityScheduleKeys.ForDetachmentStratagem("cryptek-conclave", "microscarab-swarm");
        var path = "rosters/" + api.Roster.Id + "?timing=1&rule=" + Uri.EscapeDataString(key);
        await using var host = await ComponentTestHost<RosterEditor>.CreateAsync(api, path, new() { ["Id"] = api.Roster.Id });
        var html = await host.HtmlAsync();
        Assert.True(host.Read<bool>("_timingOpen"));
        Assert.Contains(key, host.Read<HashSet<string>>("_openSchedText"));
        Assert.Contains("id=\"schedule-" + Uri.EscapeDataString(key), html);
        Assert.Contains("One CRYPTEK INFANTRY unit", html);
    }

    [Fact]
    public async Task Readiness_panels_generate_links_with_the_actual_roster_id()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: false);
        var expected = "href=\"rosters/" + api.Roster.Id + "?timing=1&rule=";
        await using var session = await Session(api);
        await session.InvokeAsync("OpenReadiness");
        var html = await session.HtmlAsync();
        Assert.Contains(expected, html);
        Assert.DoesNotContain("rosters/Id?", html);

        await using var editor = await Editor(api);
        await editor.InvokeAsync("CheckPlayAsync");
        Assert.Contains(expected, await editor.HtmlAsync());

        await using var picker = await ComponentTestHost<Warhammer40k._11.Pages.Play>.CreateAsync(api, "play");
        await picker.InvokeAsync("CheckPlayAsync", api.Roster);
        Assert.Contains(expected, await picker.HtmlAsync());
    }

    [Fact]
    public async Task Microscarab_card_and_trigger_render_in_the_screenshot_window_with_two_CP()
    {
        var api = PlayReadinessTests.ConfiguredFixture();
        await using var host = await Session(api);
        await host.InvokeAsync("SelectPhase", BattlePhase.Fight);
        await host.InvokeAsync("SetTurn", BattleTurn.Opponent);
        await host.SetAsync("_cp", 2);
        var html = await host.HtmlAsync();
        Assert.Contains("title=\"Microscarab Swarm\"", html);
        var stratagem = host.Read<IEnumerable>("AffordableNowStratagems").Cast<object>()
            .Single(item => (string)item.GetType().GetProperty("Name")!.GetValue(item)! == "Microscarab Swarm");
        await host.InvokeAsync("OpenNowStratagem", stratagem);
        html = await host.HtmlAsync();
        Assert.Contains("just after an enemy unit has selected its targets", html);
        Assert.Contains("4+ invulnerable save", html);
    }

    [Fact]
    public async Task Navigation_into_Play_is_prevented_when_pending_timing_cannot_be_saved()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: false);
        api.SaveLibrary = _ => Task.FromException<ScheduleLibrary>(new HttpRequestException("503"));
        await using var host = await Editor(api);
        await host.InvokeAsync("SetApplied", AbilityScheduleKeys.ForEnhancement("atomic-disintegrators"), true);
        var context = new LocationChangingContext { TargetLocation = "http://localhost/play/test-roster" };
        await host.InvokeAsync("BeforePlayNavigation", context);
        Assert.True((bool)typeof(LocationChangingContext).GetProperty("DidPreventNavigation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context)!);
        Assert.True(host.Read<bool>("_libraryDirty"));
        api.SaveLibrary = null;
        await host.InvokeAsync("SaveAsync");
    }

    private static Task<ComponentTestHost<RosterEditor>> Editor(TestApiClient api) =>
        ComponentTestHost<RosterEditor>.CreateAsync(api, "rosters/" + api.Roster.Id, new() { ["Id"] = api.Roster.Id });

    private static Task<ComponentTestHost<PlaySession>> Session(TestApiClient api) =>
        ComponentTestHost<PlaySession>.CreateAsync(api, "play/" + api.Roster.Id, new() { ["Id"] = api.Roster.Id });

    private static string SaveButton(string html) => Regex.Match(html,
        "<button[^>]*class=\"btn small primary\"[^>]*>.*?</button>", RegexOptions.Singleline).Value;
}
