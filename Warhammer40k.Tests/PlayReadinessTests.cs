using System.Text.Json;
using Warhammer40k.Core;
using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Tests;

public class PlayReadinessTests
{
    [Fact]
    public void Legal_army_is_not_Play_ready_when_relevant_rules_have_no_setup()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: false);
        var result = Check(api);
        Assert.True(result.ListValidation.IsReady);
        Assert.False(result.IsReady);
        Assert.Contains(result.Issues, entry => entry.Name == "Atomic Disintegrators");
        Assert.Contains(result.Issues, entry => entry.Name == "Implacable Eradication");
        Assert.Equal(10, result.Windows.Count);
    }

    [Fact]
    public void Fully_configured_entered_army_has_a_successful_readiness_path()
    {
        var api = ConfiguredFixture();
        var result = Check(api);
        Assert.True(result.ListValidation.IsReady);
        Assert.Empty(result.Issues);
        Assert.True(result.IsReady);
        Assert.All(result.Windows, window => Assert.Equal(window.Expected, window.Projected));
    }

    [Fact]
    public void Unselected_unfilled_detachments_do_not_affect_the_check()
    {
        var result = Check(ConfiguredFixture());
        Assert.DoesNotContain(result.Entries, entry => entry.Source is "Skyshroud Spearhead" or "The Phaeron's Armoury" or "Annihilation Legion");
        Assert.True(result.IsReady);
    }

    [Fact]
    public void Selected_unfilled_content_is_reported_as_an_upload_gap_without_modification()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: false);
        api.Roster.DetachmentIds = ["skyshroud-spearhead"];
        api.Roster.Units.Single(unit => unit.DatasheetId == "plasmancer").AssignedEnhancementId = "deepening-madness";
        var before = JsonSerializer.Serialize(DetachmentCatalogue.FindById("skyshroud-spearhead"));
        var result = Check(api);
        Assert.Contains(result.Issues, entry => entry.Key == "detachment|skyshroud-spearhead" && entry.Message.Contains("not an application defect"));
        Assert.Contains(result.Issues, entry => entry.Name == "Deepening Madness" && entry.State == PlayReadinessState.NotEntered);
        Assert.Equal(before, JsonSerializer.Serialize(DetachmentCatalogue.FindById("skyshroud-spearhead")));
    }

    [Fact]
    public void Check_does_not_change_roster_or_global_schedules()
    {
        var api = ConfiguredFixture();
        var roster = JsonSerializer.Serialize(api.Roster);
        var library = JsonSerializer.Serialize(api.Library);
        Check(api);
        Assert.Equal(roster, JsonSerializer.Serialize(api.Roster));
        Assert.Equal(library, JsonSerializer.Serialize(api.Library));
    }

    [Fact]
    public void Microscarab_manual_windows_map_in_both_Fight_turns_without_using_the_authored_turn_marker()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        var key = AbilityScheduleKeys.ForDetachmentStratagem("cryptek-conclave", "microscarab-swarm");
        var schedule = api.Library.GetOrCreate(key);
        schedule.SetWindow(BattlePhase.Fight, BattleTurn.Player, true);
        schedule.SetWindow(BattlePhase.Fight, BattleTurn.Opponent, true);
        schedule.SetWindow(BattlePhase.Shooting, BattleTurn.Opponent, true);
        var entry = Check(api).Entries.Single(entry => entry.Key == key);
        Assert.Equal(PlayReadinessState.Mapped, entry.State);
        Assert.Equal(3, entry.ExpectedWindows.Count);
        Assert.Equal(3, entry.ProjectedWindows.Count);
    }

    [Fact]
    public void Empty_schedule_does_not_count_as_a_deliberate_reference_review()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        var key = AbilityScheduleKeys.ForUnitAbility("immortals", "Implacable Eradication");
        api.Library.GetOrCreate(key);
        Assert.Equal(PlayReadinessState.NeedsSetup, Check(api).Entries.Single(entry => entry.Key == key).State);
    }

    [Fact]
    public void Reference_acknowledgment_expires_when_the_rule_text_changes()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        var ability = api.Catalogue.FindById("immortals")!.Abilities.Single(ability => ability.Name == "Implacable Eradication");
        var key = AbilityScheduleKeys.ForUnitAbility("immortals", ability.Name);
        api.Library.GetOrCreate(key).ReviewedReferenceHash = PlayReadiness.ReviewHash(ability.Name, ability.Text);
        Assert.Equal(PlayReadinessState.ReferenceReviewed, Check(api).Entries.Single(entry => entry.Key == key).State);
        ability.Text += " Updated condition.";
        var entry = Check(api).Entries.Single(entry => entry.Key == key);
        Assert.Equal(PlayReadinessState.NeedsSetup, entry.State);
        Assert.Contains("changed", entry.Message);
    }

    [Fact]
    public void Reference_acknowledgment_round_trips_in_a_backup()
    {
        var library = new ScheduleLibrary();
        var key = AbilityScheduleKeys.ForArmyRule("Reanimation Protocols");
        library.GetOrCreate(key).ReviewedReferenceHash = PlayReadiness.ReviewHash("Reanimation Protocols", "Reference text");
        var json = JsonSerializer.Serialize(new BackupBundle { ScheduleLibrary = library });
        var restored = JsonSerializer.Deserialize<BackupBundle>(json)!;
        Assert.Equal(library.Find(key)!.ReviewedReferenceHash, restored.ScheduleLibrary!.Find(key)!.ReviewedReferenceHash);
    }

    [Fact]
    public void Stacked_layout_limitations_are_reported_not_silently_fixed()
    {
        var api = ConfiguredFixture();
        var result = PlayReadiness.Check(api.Roster, api.Catalogue, api.Library, focusMode: false);
        Assert.Contains(result.Issues, entry => entry.Key == "view|stacked");
        Assert.Contains(result.Entries, entry => entry.Name == "Implacable Eradication" && entry.State == PlayReadinessState.NotProjected);
        Assert.Contains(result.Windows, window => window.Expected > window.Projected);
    }

    [Fact]
    public void Atomic_requires_manual_enable_or_an_explicit_reference_only_review()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: false);
        var key = AbilityScheduleKeys.ForEnhancement("atomic-disintegrators");
        Assert.Equal(PlayReadinessState.NeedsSetup, Check(api).Entries.Single(entry => entry.Key == key).State);
        api.Library.GetOrCreate(key).ApplyToUnit = true;
        var enabled = Check(api).Entries.Single(entry => entry.Key == key);
        Assert.Equal(PlayReadinessState.Mapped, enabled.State);
        Assert.Single(enabled.ProjectedWindows);
    }

    [Fact]
    public void Missing_datasheet_and_wargear_references_are_not_discarded_from_the_inventory()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        api.Roster.Units[0].Wargear.Add(new WargearSelection { GroupId = "removed-group", OptionIds = ["removed-option"] });
        Assert.Contains(Check(api).Issues, entry => entry.Key.StartsWith("wargear|", StringComparison.Ordinal));
        api.Roster.Units[0].DatasheetId = "missing-datasheet";
        Assert.Contains(Check(api).Issues, entry => entry.Key == "unit|missing-datasheet");
    }

    [Fact]
    public void Raw_ability_inventory_catches_a_source_lost_by_name_deduplication()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        api.Catalogue.FindById("plasmancer")!.Abilities.Add(new Ability { Name = "Implacable Eradication", Text = "A different member-scoped rule." });
        var key = AbilityScheduleKeys.ForUnitAbility("plasmancer", "Implacable Eradication");
        Assert.Equal(PlayReadinessState.NotProjected, Check(api).Entries.Single(entry => entry.Key == key).State);
    }

    [Fact]
    public void Invalid_windows_require_review()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        var key = AbilityScheduleKeys.ForArmyRule("Reanimation Protocols");
        api.Library.GetOrCreate(key).Windows.Add(new AbilityWindow(BattlePhase.Any, BattleTurn.Player));
        var entry = Check(api).Entries.Single(entry => entry.Key == key);
        Assert.Equal(PlayReadinessState.NeedsSetup, entry.State);
        Assert.Contains("invalid", entry.Message);
    }

    [Fact]
    public void Only_orphaned_entries_related_to_this_army_are_reported()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        var relevant = AbilityScheduleKeys.ForUnitAbility("immortals", "Removed rule");
        var unrelated = AbilityScheduleKeys.ForUnitAbility("canoptek-doomstalker", "Removed rule");
        api.Library.GetOrCreate(relevant).SetWindow(BattlePhase.Fight, BattleTurn.Player, true);
        api.Library.GetOrCreate(unrelated).SetWindow(BattlePhase.Fight, BattleTurn.Player, true);
        var result = Check(api);
        Assert.Contains(result.Issues, entry => entry.Key == relevant);
        Assert.DoesNotContain(result.Issues, entry => entry.Key == unrelated);
    }

    [Fact]
    public void Keyword_inapplicable_core_stratagems_need_no_schedule()
    {
        var result = Check(ShootingChoiceTests.Fixture(enableAtomic: true));
        Assert.Equal(PlayReadinessState.NotApplicable, result.Entries.Single(entry => entry.Key == AbilityScheduleKeys.ForCoreStratagem("15.10")).State);
        Assert.Equal(PlayReadinessState.NotApplicable, result.Entries.Single(entry => entry.Key == AbilityScheduleKeys.ForCoreStratagem("15.05")).State);
    }

    [Fact]
    public void Stale_army_rules_are_reported_but_unassigned_enhancement_schedules_are_ignored()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        var oldRule = AbilityScheduleKeys.ForArmyRule("Retired army rule");
        var otherEnhancement = AbilityScheduleKeys.ForEnhancement("unassigned-enhancement");
        api.Library.GetOrCreate(oldRule).SetWindow(BattlePhase.Command, BattleTurn.Player, true);
        api.Library.GetOrCreate(otherEnhancement).SetWindow(BattlePhase.Shooting, BattleTurn.Player, true);
        var result = Check(api);
        Assert.Contains(result.Issues, entry => entry.Key == oldRule);
        Assert.DoesNotContain(result.Entries, entry => entry.Key == otherEnhancement);
    }

    [Fact]
    public void Reference_only_detachment_rules_need_an_explicit_review()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: false);
        api.Roster.DetachmentIds = ["hand-of-the-dynasty"];
        api.Roster.Units.Single(unit => unit.DatasheetId == "plasmancer").AssignedEnhancementId = null;
        var rule = DetachmentCatalogue.FindById("hand-of-the-dynasty")!.Rules.Single();
        var key = AbilityScheduleKeys.ForDetachmentRule("hand-of-the-dynasty", rule.Name);
        Assert.Equal(PlayReadinessState.NeedsSetup, Check(api).Entries.Single(entry => entry.Key == key).State);
        api.Library.GetOrCreate(key).ReviewedReferenceHash = PlayReadiness.ReviewHash(rule.Name, rule.Text);
        Assert.Equal(PlayReadinessState.ReferenceReviewed, Check(api).Entries.Single(entry => entry.Key == key).State);
    }

    private static PlayReadinessResult Check(TestApiClient api) => PlayReadiness.Check(api.Roster, api.Catalogue, api.Library);

    internal static TestApiClient ConfiguredFixture()
    {
        var api = ShootingChoiceTests.Fixture(enableAtomic: true);
        void Schedule(string key)
        {
            foreach (var phase in BattlePhases.Ordered)
                foreach (var turn in new[] { BattleTurn.Player, BattleTurn.Opponent })
                    api.Library.GetOrCreate(key).SetWindow(phase, turn, true);
        }
        foreach (var unit in api.Roster.Units)
            foreach (var ability in api.Catalogue.FindById(unit.DatasheetId)!.Abilities)
                Schedule(AbilityScheduleKeys.ForUnitAbility(unit.DatasheetId, ability.Name));
        foreach (var rule in ArmyRuleCatalogue.ForFaction(api.Roster.Faction))
            Schedule(AbilityScheduleKeys.ForArmyRule(rule.Name));
        foreach (var stratagem in CoreStratagemCatalogue.All)
            Schedule(AbilityScheduleKeys.ForCoreStratagem(stratagem.Id));
        foreach (var stratagem in DetachmentCatalogue.FindById("cryptek-conclave")!.Stratagems)
            Schedule(AbilityScheduleKeys.ForDetachmentStratagem("cryptek-conclave", stratagem.Id));
        return api;
    }
}
