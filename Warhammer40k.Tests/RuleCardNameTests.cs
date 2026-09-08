using System.Text.Json;
using System.Text.RegularExpressions;
using Warhammer40k.Core;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;
using Warhammer40k._11.Pages;
using SettingsPage = Warhammer40k._11.Pages.Settings;

namespace Warhammer40k.Tests;

public class RuleCardNameTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rule_cards_keep_printed_names_regardless_of_the_legacy_compact_preference(bool legacyCompact)
    {
        var api = PlayReadinessTests.ConfiguredFixture();
        api.Settings.PlayCompactRules = legacyCompact;
        await using var host = await Session(api);
        await host.SetAsync("_cp", 2);
        var names = Names(await host.HtmlAsync());

        Assert.Contains("Command Re-roll", names);
        Assert.Contains("Insane Bravery", names);
        Assert.Contains("Reanimation Protocols", names);
        Assert.Contains("Implacable Eradication", names);
        Assert.DoesNotContain("Re-roll", names);
        Assert.DoesNotContain("Auto-pass", names);
        Assert.DoesNotContain("Reanimate D3 wounds", names);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Microscarab_shows_its_name_not_an_ambiguous_save_summary(bool legacyCompact)
    {
        var api = PlayReadinessTests.ConfiguredFixture();
        api.Settings.PlayCompactRules = legacyCompact;
        await using var host = await Session(api);
        await host.InvokeAsync("SelectPhase", BattlePhase.Fight);
        await host.InvokeAsync("SetTurn", BattleTurn.Opponent);
        await host.SetAsync("_cp", 2);
        var names = Names(await host.HtmlAsync());

        Assert.Contains("Microscarab Swarm", names);
        Assert.DoesNotContain("5+ Inv", names);
        Assert.DoesNotContain("4+ Inv", names);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Enhancement_cards_keep_their_printed_name(bool legacyCompact)
    {
        var api = PlayReadinessTests.ConfiguredFixture();
        api.Settings.PlayCompactRules = legacyCompact;
        api.Roster.Units.Single(unit => unit.DatasheetId == "plasmancer").AssignedEnhancementId = "gravitic-bolas";
        api.Library.GetOrCreate(AbilityScheduleKeys.ForEnhancement("gravitic-bolas"))
            .SetWindow(BattlePhase.Shooting, BattleTurn.Player, true);
        await using var host = await Session(api);
        await host.InvokeAsync("SelectPhase", BattlePhase.Shooting);

        Assert.Contains("Gravitic Bolas", Names(await host.HtmlAsync()));
    }

    [Fact]
    public async Task Detachment_buff_keeps_its_authored_name()
    {
        var api = PlayReadinessTests.ConfiguredFixture();
        api.Settings.PlayCompactRules = true;
        api.Roster.DetachmentId = "starshatter-arsenal";
        api.Roster.DetachmentIds = ["starshatter-arsenal"];
        api.Roster.Units.Single(unit => unit.DatasheetId == "plasmancer").AssignedEnhancementId = null;
        var buff = DetachmentCatalogue.FindById("starshatter-arsenal")!.Rules.SelectMany(rule => rule.ConditionalBuffs).Single();
        api.Library.GetOrCreate(AbilityScheduleKeys.ForDetachmentBuff("starshatter-arsenal", buff.Label))
            .SetWindow(BattlePhase.Shooting, BattleTurn.Player, true);
        await using var host = await Session(api);
        await host.InvokeAsync("SelectPhase", BattlePhase.Shooting);

        Assert.Contains(buff.Label, Names(await host.HtmlAsync()));
    }

    [Fact]
    public async Task Aura_card_keeps_its_printed_name()
    {
        var api = PlayReadinessTests.ConfiguredFixture();
        api.Settings.PlayCompactRules = true;
        var keywords = api.Roster.Units.SelectMany(unit => api.Catalogue.FindById(unit.DatasheetId)!.Keywords).ToList();
        var source = api.Catalogue.Datasheets
            .Where(sheet => !api.Roster.Units.Any(unit => unit.DatasheetId == sheet.Id))
            .SelectMany(sheet => sheet.Abilities.Select(ability => (Sheet: sheet, Ability: ability, Aura: AuraParser.Parse(ability))))
            .First(item => item.Aura is not null && item.Aura.AppliesTo(keywords));
        api.Roster.Units.Add(RosterUnit.FromDatasheet(source.Sheet));
        api.Library.GetOrCreate(AbilityScheduleKeys.ForUnitAbility(source.Sheet.Id, source.Ability.Name))
            .SetWindow(BattlePhase.Shooting, BattleTurn.Player, true);
        await using var host = await Session(api);
        await host.InvokeAsync("SelectPhase", BattlePhase.Shooting);

        Assert.Contains(source.Ability.Name, Names(await host.HtmlAsync()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Selected_shooting_option_is_state_not_a_replacement_rule_name(bool legacyCompact)
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        api.Settings.PlayCompactRules = legacyCompact;
        await using var host = await Session(api);
        await host.InvokeAsync("SelectPhase", BattlePhase.Shooting);
        var unit = Assert.Single(host.Read<BattleRoster>("_battle").Units);
        await host.InvokeAsync("ChooseShootingOption", unit, "Anti-VEHICLE 5+");
        var html = await host.HtmlAsync();
        var names = Names(html);

        Assert.Contains("Technosorcerous Augmentations", names);
        Assert.DoesNotContain("Choose shooting ability", names);
        Assert.DoesNotContain("Anti-VEHICLE 5+", names);
        Assert.Contains("Selected: Anti-VEHICLE 5+", html);
        await host.InvokeAsync("ShowOverview");
        html = await host.HtmlAsync();
        Assert.Contains("Technosorcerous Augmentations", html);
        Assert.DoesNotContain("Choose shooting ability", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Settings_does_not_offer_the_retired_effect_title_switch(bool legacyCompact)
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        api.Settings.PlayCompactRules = legacyCompact;
        await using var host = await ComponentTestHost<SettingsPage>.CreateAsync(api, "settings");
        var html = await host.HtmlAsync();

        Assert.DoesNotContain("Compact rules", html);
        Assert.Contains("Rule cards show their printed names", html);
        Assert.Equal(legacyCompact, api.Settings.PlayCompactRules);
    }

    [Fact]
    public void Legacy_compact_preference_still_round_trips_in_backups()
    {
        var backup = new BackupBundle { Settings = new UserSettings { PlayCompactRules = true } };
        var restored = JsonSerializer.Deserialize<BackupBundle>(JsonSerializer.Serialize(backup))!;
        Assert.True(restored.Settings.PlayCompactRules);
    }

    [Fact]
    public void Title_extraction_accepts_Blazor_scoped_styling_attributes()
    {
        const string html = "<button class=\"now-action stratagem\" b-scope><strong b-scope>Command Re-roll</strong></button>";
        Assert.Equal("Command Re-roll", Assert.Single(Names(html)));
    }

    private static Task<ComponentTestHost<PlaySession>> Session(TestApiClient api) =>
        ComponentTestHost<PlaySession>.CreateAsync(api, "play/" + api.Roster.Id, new() { ["Id"] = api.Roster.Id });

    private static List<string> Names(string html) => Regex.Matches(html,
        "<button[^>]*class=\"now-action(?: [^\"]*)?\"[^>]*>.*?<strong\\b[^>]*>(.*?)</strong>", RegexOptions.Singleline)
        .Select(match => match.Groups[1].Value.Trim()).ToList();
}
