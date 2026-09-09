using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;
using Warhammer40k._11.Pages;

namespace Warhammer40k.Tests;

public class PlayViewParityTests
{
    public static IEnumerable<object[]> Windows => BattlePhases.Ordered.SelectMany(phase =>
        new[] { BattleTurn.Player, BattleTurn.Opponent }.SelectMany(turn =>
            new[] { new object[] { phase, turn, false }, new object[] { phase, turn, true } }));

    [Theory]
    [MemberData(nameof(Windows))]
    public async Task Focus_list_and_matrix_preserve_the_same_scoped_actions(BattlePhase phase, BattleTurn turn, bool withAuraAndBuff)
    {
        var api = Army(withAuraAndBuff);
        await using var host = await Session(api);
        var battle = host.Read<BattleRoster>("_battle");
        var primary = battle.Units[0].Primary;
        await host.InvokeAsync("StepPart", primary, 1 - primary.TrackMax);
        await host.InvokeAsync("SelectPhase", phase);
        await host.InvokeAsync("SetTurn", turn);
        await host.SetAsync("_cp", 3);

        var focused = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < battle.Units.Count; index++)
        {
            await host.SetAsync("_activeCard", index);
            focused.UnionWith(NowTokens(host));
        }
        await host.SetAsync("_cardSwipe", false);
        Assert.Equal(focused.Order(), NowTokens(host).Order());
        var html = await host.HtmlAsync();
        Assert.Equal(host.Read<int>("AvailableNowTotal"), Regex.Matches(html, "<button[^>]*class=\"now-action(?: [^\"]*)?\"").Count);
        await host.InvokeAsync("ShowOverview");
        await host.InvokeAsync("SetOverviewMatrix", true);
        Assert.Equal(focused.Order(), MatrixTokens(host).Order());
    }

    [Fact]
    public async Task Shared_names_from_different_sources_remain_distinct_matrix_columns()
    {
        var api = Army();
        var warriors = api.Catalogue.FindById("necron-warriors")!;
        var ability = warriors.Abilities.First(ability => ability.Name != "Leader");
        ability.Name = "Implacable Eradication";
        ability.Text = "A distinct rule for this source.";
        Schedule(api, AbilityScheduleKeys.ForUnitAbility(warriors.Id, ability.Name));
        await using var host = await Session(api);
        await host.InvokeAsync("ShowOverview");
        await host.InvokeAsync("SetOverviewMatrix", true);
        var columns = Items(Value(Call(host.Component, "BuildMatrix")!, "Columns"));
        Assert.Equal(2, columns.Count(column => (string)Value(column, "Label")! == ability.Name));
    }

    [Fact]
    public async Task Whole_army_use_requires_a_target_and_charges_that_targets_cost()
    {
        var api = Army();
        await using var host = await Session(api);
        await host.SetAsync("_cardSwipe", false);
        await host.SetAsync("_cp", 1);
        var stratagem = Stratagem(host, "Command Re-roll");
        await host.InvokeAsync("OpenNowStratagem", stratagem);
        Assert.Null(host.Read<BattleUnit?>("StratContextUnit"));
        Assert.Contains("Choose a target to use", await host.HtmlAsync());
        await host.InvokeAsync("UseNowStratagem");
        Assert.Equal(1, host.Read<int>("_cp"));

        var normalCostUnit = host.Read<BattleRoster>("_battle").Units.Single(unit => unit.Primary.Datasheet.Id == "immortals");
        await host.InvokeAsync("SelectStratagemTarget", normalCostUnit);
        Assert.Equal(1, (int)Call(host.Component, "OpenStratagemCost", stratagem)!);
        await host.InvokeAsync("UseNowStratagem");
        Assert.Equal(0, host.Read<int>("_cp"));
    }

    [Fact]
    public async Task A_discounted_target_remains_available_at_zero_CP_in_every_view()
    {
        var api = Army();
        await using var host = await Session(api);
        var units = host.Read<BattleRoster>("_battle").Units;
        var discounted = units.Single(unit => unit.Primary.Datasheet.Id == "necron-warriors");
        await host.SetAsync("_activeCard", units.ToList().IndexOf(discounted));
        await host.SetAsync("_cp", 0);
        var stratagem = Stratagem(host, "Command Re-roll");
        Assert.Contains("S|Core|15.02|" + discounted.Id, NowTokens(host));
        await host.SetAsync("_cardSwipe", false);
        Assert.Contains("S|Core|15.02|" + discounted.Id, NowTokens(host));
        Assert.Contains("0–1 CP", await host.HtmlAsync());
        await host.InvokeAsync("OpenNowStratagem", stratagem);
        Assert.Equal(discounted.Id, host.Read<BattleUnit>("StratContextUnit").Id);
        Assert.Equal(0, (int)Call(host.Component, "OpenStratagemCost", stratagem)!);
        await host.InvokeAsync("UseNowStratagem");
        Assert.Equal(0, host.Read<int>("_cp"));
        await host.InvokeAsync("ShowOverview");
        await host.InvokeAsync("SetOverviewMatrix", true);
        Assert.Contains("S|Core|15.02|" + discounted.Id, MatrixTokens(host));
        await host.InvokeAsync("OpenGeneralStratagem", stratagem);
        Assert.Equal(discounted.Id, host.Read<BattleUnit>("StratContextUnit").Id);
    }

    [Fact]
    public async Task Spending_rechecks_current_CP_and_phase_after_a_sheet_was_opened()
    {
        var api = Army();
        var key = AbilityScheduleKeys.ForCoreStratagem("15.04");
        api.Library.Find(key)!.Windows = [new(BattlePhase.Command, BattleTurn.Player)];
        await using var host = await Session(api);
        await host.SetAsync("_cp", 1);
        var stratagem = Stratagem(host, "Insane Bravery");
        await host.InvokeAsync("OpenNowStratagem", stratagem);
        await host.SetAsync("_cp", 0);
        await host.InvokeAsync("UseNowStratagem");
        Assert.DoesNotContain("S|15.04", host.Read<HashSet<string>>("_usedOncePerBattle"));
        await host.SetAsync("_cp", 1);
        await host.InvokeAsync("SelectPhase", BattlePhase.Shooting);
        await host.InvokeAsync("UseNowStratagem");
        Assert.Equal(1, host.Read<int>("_cp"));
        Assert.DoesNotContain("S|15.04", host.Read<HashSet<string>>("_usedOncePerBattle"));
    }

    [Fact]
    public async Task Spent_battle_stratagems_disappear_from_every_view_and_the_badge_count()
    {
        var api = Army();
        await using var host = await Session(api);
        await host.SetAsync("_cp", 3);
        var unit = host.Read<BattleRoster>("_battle").Units[0];
        var before = (int)Call(host.Component, "StratagemsNowCount", unit)!;
        await host.InvokeAsync("OpenNowStratagem", Stratagem(host, "Insane Bravery"));
        await host.InvokeAsync("UseNowStratagem");
        Assert.Equal(2, host.Read<int>("_cp"));
        Assert.Contains("S|15.04", host.Read<HashSet<string>>("_usedOncePerBattle"));
        Assert.Equal(before - 1, (int)Call(host.Component, "StratagemsNowCount", unit)!);
        Assert.DoesNotContain(NowTokens(host), token => token.StartsWith("S|Core|15.04|", StringComparison.Ordinal));
        await host.SetAsync("_cardSwipe", false);
        Assert.DoesNotContain(NowTokens(host), token => token.StartsWith("S|Core|15.04|", StringComparison.Ordinal));
        await host.InvokeAsync("ShowOverview");
        await host.InvokeAsync("SetOverviewMatrix", true);
        Assert.DoesNotContain(MatrixTokens(host), token => token.StartsWith("S|Core|15.04|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Destroyed_model_reactions_remain_visible_without_exposing_ordinary_dead_unit_actions()
    {
        var api = PlayReadinessTests.ConfiguredFixture();
        var bearer = api.Roster.Units.Single(unit => unit.DatasheetId == "plasmancer");
        bearer.AttachedToRosterUnitId = null;
        await using var host = await Session(api);
        var units = host.Read<BattleRoster>("_battle").Units;
        var dead = units.Single(unit => unit.Primary.Datasheet.Id == "plasmancer");
        await host.InvokeAsync("StepPart", dead.Primary, -dead.Primary.TrackMax);
        await host.SetAsync("_activeCard", units.ToList().IndexOf(dead));
        await host.SetAsync("_cp", 2);
        await host.InvokeAsync("SelectPhase", BattlePhase.Fight);
        await host.InvokeAsync("SetTurn", BattleTurn.Opponent);
        var reaction = "S|Cryptek Conclave|animus-curse|" + dead.Id;
        Assert.Contains(reaction, NowTokens(host));
        Assert.DoesNotContain("S|Core|15.02|" + dead.Id, NowTokens(host));
        Assert.True((int)Call(host.Component, "StratagemsNowCount", dead)! > 0);
        await host.SetAsync("_cardSwipe", false);
        Assert.Contains(reaction, NowTokens(host));
        await host.InvokeAsync("ShowOverview");
        await host.InvokeAsync("SetOverviewMatrix", true);
        Assert.Contains(reaction, MatrixTokens(host));
    }

    [Fact]
    public async Task A_loss_clause_does_not_grant_permission_to_target_a_destroyed_unit()
    {
        var api = Army();
        api.Roster.DetachmentId = "awakened-dynasty";
        api.Roster.DetachmentIds = ["awakened-dynasty"];
        api.Roster.Units.Single(unit => unit.DatasheetId == "plasmancer").AssignedEnhancementId = null;
        foreach (var stratagem in DetachmentCatalogue.FindById("awakened-dynasty")!.Stratagems)
            Schedule(api, AbilityScheduleKeys.ForDetachmentStratagem("awakened-dynasty", stratagem.Id));
        await using var host = await Session(api);
        var unit = host.Read<BattleRoster>("_battle").Units[0];
        foreach (var part in unit.Parts)
            await host.InvokeAsync("StepPart", part, -part.TrackMax);
        await host.SetAsync("_cp", 3);
        var stratagemWithLosses = Stratagem(host, "Protocol of the Undying Legions");
        Assert.False((bool)Call(host.Component, "CanUseStratagem", unit, stratagemWithLosses)!);
        var permittedReaction = Stratagem(host, "Protocol of the Eternal Revenant");
        Assert.True((bool)Call(host.Component, "CanUseStratagem", unit, permittedReaction)!);
    }

    [Theory]
    [InlineData("Reminder")]
    [InlineData("Aura")]
    [InlineData("Choice")]
    public async Task Unit_scoped_matrix_headers_open_the_corresponding_action(string kind)
    {
        var api = Army(withAuraAndBuff: kind == "Aura");
        await using var host = await Session(api);
        var primary = host.Read<BattleRoster>("_battle").Units[0].Primary;
        await host.InvokeAsync("StepPart", primary, 1 - primary.TrackMax);
        await host.InvokeAsync("SelectPhase", kind == "Reminder" ? BattlePhase.Command : BattlePhase.Shooting);
        await host.InvokeAsync("ShowOverview");
        await host.InvokeAsync("SetOverviewMatrix", true);
        var column = Items(Value(Call(host.Component, "BuildMatrix")!, "Columns"))
            .First(column => Value(column, "Kind")!.ToString() == kind);
        await host.InvokeAsync("OpenMatrixColumn", column);
        if (kind == "Reminder")
            Assert.False(host.Read<bool>("_overview"));
        else
            Assert.NotNull(host.Read<object?>(kind == "Aura" ? "_auraCard" : "_shootingCardUnit"));
    }

    internal static TestApiClient Army(bool withAuraAndBuff = false)
    {
        var api = PlayReadinessTests.ConfiguredFixture();
        var warriors = RosterUnit.FromDatasheet(api.Catalogue.FindById("necron-warriors")!);
        var overlord = RosterUnit.FromDatasheet(api.Catalogue.FindById("overlord")!);
        overlord.AttachedToRosterUnitId = warriors.Id;
        api.Roster.Units.AddRange([warriors, overlord]);
        api.Library.GetOrCreate(AbilityScheduleKeys.ForUnitAbility("overlord", "My Will Be Done")).ApplyToUnit = true;
        if (withAuraAndBuff)
        {
            api.Roster.DetachmentId = "starshatter-arsenal";
            api.Roster.DetachmentIds = ["starshatter-arsenal"];
            api.Roster.Units.Single(unit => unit.DatasheetId == "plasmancer").AssignedEnhancementId = null;
            var keywords = api.Roster.Units.SelectMany(unit => api.Catalogue.FindById(unit.DatasheetId)!.Keywords).ToList();
            var source = api.Catalogue.Datasheets.Where(sheet => !api.Roster.Units.Any(unit => unit.DatasheetId == sheet.Id))
                .First(sheet => sheet.Abilities.Any(ability => AuraParser.Parse(ability) is { } aura && aura.AppliesTo(keywords)));
            api.Roster.Units.Add(RosterUnit.FromDatasheet(source));
        }
        foreach (var unit in api.Roster.Units)
            foreach (var ability in api.Catalogue.FindById(unit.DatasheetId)!.Abilities)
                Schedule(api, AbilityScheduleKeys.ForUnitAbility(unit.DatasheetId, ability.Name));
        foreach (var id in api.Roster.EffectiveDetachmentIds)
        {
            var detachment = DetachmentCatalogue.FindById(id)!;
            foreach (var stratagem in detachment.Stratagems)
                Schedule(api, AbilityScheduleKeys.ForDetachmentStratagem(id, stratagem.Id));
            foreach (var buff in detachment.Rules.SelectMany(rule => rule.ConditionalBuffs))
                Schedule(api, AbilityScheduleKeys.ForDetachmentBuff(id, buff.Label));
        }
        return api;
    }

    internal static void Schedule(TestApiClient api, string key)
    {
        foreach (var phase in BattlePhases.Ordered)
            foreach (var turn in new[] { BattleTurn.Player, BattleTurn.Opponent })
                api.Library.GetOrCreate(key).SetWindow(phase, turn, true);
    }

    internal static Task<ComponentTestHost<PlaySession>> Session(TestApiClient api) =>
        ComponentTestHost<PlaySession>.CreateAsync(api, "play/" + api.Roster.Id, new() { ["Id"] = api.Roster.Id });

    internal static object Stratagem(ComponentTestHost<PlaySession> host, string name) =>
        Items(Call(host.Component, "PhaseTurnStratagems")).Single(item => (string)Value(item, "Name")! == name);

    internal static object? Call(object target, string method, params object?[] arguments) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(target, arguments);

    internal static object? Value(object target, string name) =>
        target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target)
        ?? target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);

    private static IEnumerable<object> Items(object? value) => value is IEnumerable items ? items.Cast<object>() : [];

    internal static HashSet<string> NowTokens(ComponentTestHost<PlaySession> host)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var units = host.Read<BattleUnit?>("NowUnit") is { } focus ? new[] { focus } : host.Read<BattleRoster>("_battle").Units.ToArray();
        foreach (var pair in host.Read<IReadOnlyList<(BattleUnit Unit, BattleAbility Ability)>>("NowAbilities"))
            tokens.Add("A|" + pair.Ability.Key + "|" + pair.Unit.Id);
        foreach (var pair in host.Read<IReadOnlyList<(BattleUnit Unit, ConditionalUnitBuff Buff)>>("NowBuffs"))
            tokens.Add("B|" + Call(host.Component, "BuffIdentity", pair.Buff) + "|" + pair.Unit.Id);
        foreach (var pair in Items(host.Read<object>("NowAuras")))
        {
            var unit = (BattleUnit)Value(pair, "Item1")!;
            var offer = Value(pair, "Item2")!;
            tokens.Add("U|" + ((BattleUnit)Value(offer, "Source")!).Id + "|" + ((BattleAbility)Value(offer, "Ability")!).Ability.Name + "|" + unit.Id);
        }
        foreach (var reminder in Items(host.Read<object>("NowReminders")))
            tokens.Add("M|" + ((string)Value(reminder, "Label")! == "Take Battle-shock test" ? "Battle-shock" : "Reanimation") + "|" + ((BattleUnit)Value(reminder, "Unit")!).Id);
        foreach (var unit in host.Read<IReadOnlyList<BattleUnit>>("NowShootingChoices"))
            tokens.Add("C|" + Call(host.Component, "ShootingChoiceName", unit) + "|" + unit.Id);
        foreach (var rule in host.Read<IReadOnlyList<ArmyRule>>("UsableArmyRules"))
            foreach (var unit in units.Where(unit => !(bool)Call(host.Component, "IsDead", unit)!))
                tokens.Add("R|" + rule.Name + "|" + unit.Id);
        foreach (var stratagem in Items(host.Read<object>("AffordableNowStratagems")))
            foreach (var unit in units.Where(unit => (bool)Call(host.Component, "CanUseStratagem", unit, stratagem)!))
                tokens.Add("S|" + Value(stratagem, "Source") + "|" + Value(stratagem, "Id") + "|" + unit.Id);
        return tokens;
    }

    internal static HashSet<string> MatrixTokens(ComponentTestHost<PlaySession> host)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var column in Items(Value(Call(host.Component, "BuildMatrix")!, "Columns")))
        {
            var key = (string)Value(column, "Key")!;
            if (Value(column, "Kind")!.ToString() == "Stratagem")
                key = "S|" + key;
            foreach (var unitId in ((IReadOnlyDictionary<string, object?>)Value(column, "Applies")!).Keys)
                tokens.Add(key + "|" + unitId);
        }
        return tokens;
    }
}
