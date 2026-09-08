using System.Security.Cryptography;
using System.Text;
using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Rosters;
using Warhammer40k.Core.Rosters.Validation;

namespace Warhammer40k.Core.Play;

public enum PlayReadinessState
{
    Mapped,
    ReferenceReviewed,
    NotApplicable,
    NeedsSetup,
    NotEntered,
    NotProjected,
}

public sealed record PlayReadinessEntry(string Key, string Name, string Source, string? UnitId,
    PlayReadinessState State, string Message)
{
    public IReadOnlyList<AbilityWindow> ExpectedWindows { get; init; } = [];
    public IReadOnlyList<AbilityWindow> ProjectedWindows { get; init; } = [];
    public bool NeedsAttention => State is PlayReadinessState.NeedsSetup or PlayReadinessState.NotEntered or PlayReadinessState.NotProjected;
}

public sealed record PlayWindowCoverage(BattlePhase Phase, BattleTurn Turn, int Expected, int Projected);

public sealed record PlayReadinessResult(ValidationResult ListValidation, IReadOnlyList<PlayReadinessEntry> Entries)
{
    public IReadOnlyList<PlayReadinessEntry> Issues { get; } = Entries.Where(entry => entry.NeedsAttention).ToList();
    public bool IsReady => ListValidation.IsReady && Issues.Count == 0;
    public int ReferenceCount => Entries.Count(entry => entry.State == PlayReadinessState.ReferenceReviewed);
    public int AccountedCount => Entries.Count(entry => !entry.NeedsAttention);
    public IReadOnlyList<PlayWindowCoverage> Windows { get; } = BattlePhases.Ordered
        .SelectMany(phase => new[] { BattleTurn.Player, BattleTurn.Opponent }.Select(turn => new PlayWindowCoverage(
            phase, turn,
            Entries.Count(entry => entry.ExpectedWindows.Any(window => window.Phase == phase && window.Turn == turn)),
            Entries.Count(entry => entry.ProjectedWindows.Any(window => window.Phase == phase && window.Turn == turn)))))
        .ToList();
}

/// <summary>
/// Reconciles the selected army's entered rules against its manual setup and Play projections. It does not
/// infer legal timing from prose, mutate setup, or certify content that has not been entered.
/// </summary>
public static class PlayReadiness
{
    private static readonly AbilityWindow[] AllWindows = BattlePhases.Ordered
        .SelectMany(phase => new[] { new AbilityWindow(phase, BattleTurn.Player), new AbilityWindow(phase, BattleTurn.Opponent) })
        .ToArray();

    public static string ReviewHash(string name, string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name + "\n" + text.Replace("\r\n", "\n", StringComparison.Ordinal))));

    public static string StratagemText(int cost, string when, string target, string effect, string? restrictions = null) =>
        $"{cost} CP\n{when}\n{target}\n{effect}\n{restrictions}";

    public static bool HasConfiguration(AbilitySchedule? schedule) => schedule is not null
        && (schedule.Windows.Count > 0 || schedule.ApplyToUnit || !string.IsNullOrWhiteSpace(schedule.ManualKeyword));

    public static bool IsReviewedReference(AbilitySchedule? schedule, string name, string text) =>
        schedule is not null && !HasConfiguration(schedule)
        && string.Equals(schedule.ReviewedReferenceHash, ReviewHash(name, text), StringComparison.Ordinal);

    public static PlayReadinessResult Check(Roster roster, CatalogueData catalogue, ScheduleLibrary library, bool focusMode = true)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(library);
        var validation = new RosterValidator().Validate(roster, catalogue);
        var entries = new List<PlayReadinessEntry>();
        var knownKeys = new HashSet<string>(StringComparer.Ordinal);
        var selectedSheets = roster.Units.Select(unit => unit.DatasheetId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedDetachments = roster.EffectiveDetachmentIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedEnhancements = roster.Units.Select(unit => unit.AssignedEnhancementId).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var effective = new Roster
        {
            Id = roster.Id, Name = roster.Name, Faction = roster.Faction, PointsLimit = roster.PointsLimit,
            DetachmentId = roster.DetachmentId, DetachmentIds = [.. roster.EffectiveDetachmentIds],
            Units = roster.Units, AbilitySchedules = library.EffectiveFor(roster),
        };

        void Add(string key, string name, string source, string? unitId, PlayReadinessState state, string message)
        {
            knownKeys.Add(key);
            entries.Add(new(key, name, source, unitId, state, message));
        }

        if (roster.Units.Count == 0)
        {
            Add("roster|empty", "Army", "Setup", null, PlayReadinessState.NeedsSetup, "Add units before checking Play setup.");
            return new(validation, entries);
        }
        if (roster.Units.GroupBy(unit => unit.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            Add("roster|ids", "Unit identities", "Setup", null, PlayReadinessState.NotProjected, "Duplicate unit identifiers prevent a reliable Play projection.");
            return new(validation, entries);
        }

        var battle = BattleRoster.Build(effective, catalogue);
        var parts = battle.Units.SelectMany(unit => unit.Parts.Select(part => (Unit: unit, Part: part)))
            .ToDictionary(item => item.Part.Unit.Id, StringComparer.Ordinal);
        var vocabulary = catalogue.Datasheets.SelectMany(sheet => sheet.Keywords).ToArray();

        void Scheduled(string key, string name, string text, string source, string? unitId, Func<BattlePhase, BattleTurn, bool> projects)
        {
            knownKeys.Add(key);
            var schedule = effective.FindSchedule(key);
            if (string.IsNullOrWhiteSpace(text))
            {
                Add(key, name, source, unitId, PlayReadinessState.NotEntered, "No rule text has been entered for this selected content.");
                return;
            }
            var expected = (schedule?.Windows ?? [])
                .Where(window => BattlePhases.Ordered.Contains(window.Phase) && Enum.IsDefined(window.Turn))
                .DistinctBy(window => (window.Phase, window.Turn)).ToList();
            var projected = AllWindows.Where(window => projects(window.Phase, window.Turn)).ToList();
            var state = PlayReadinessState.Mapped;
            var message = "Configured windows reach Play.";
            if (schedule?.Windows.Any(window => !BattlePhases.Ordered.Contains(window.Phase) || !Enum.IsDefined(window.Turn)) == true)
            {
                state = PlayReadinessState.NeedsSetup;
                message = "This schedule contains an invalid phase or turn. Review its timing.";
            }
            else if (expected.Count == 0)
            {
                state = IsReviewedReference(schedule, name, text) ? PlayReadinessState.ReferenceReviewed : PlayReadinessState.NeedsSetup;
                message = state == PlayReadinessState.ReferenceReviewed
                    ? "Explicitly reviewed as reference only; no Now prompt."
                    : schedule?.ReviewedReferenceHash is not null
                        ? "The rule changed since its reference-only review. Review it again or configure timing."
                        : "No timing configured. Set its windows or explicitly review it as reference only.";
            }
            else if (expected.Count != projected.Count || expected.Any(window => !projected.Any(actual => actual.Phase == window.Phase && actual.Turn == window.Turn)))
            {
                state = PlayReadinessState.NotProjected;
                message = "Configured timing does not match the available Play presentation. Review setup or use focused cards.";
            }
            entries.Add(new(key, name, source, unitId, state, message) { ExpectedWindows = expected, ProjectedWindows = projected });
        }

        void AbilityEntry(BattleUnit unit, BattlePart part, string key, string name, string text, BattleAbility? projected)
        {
            knownKeys.Add(key);
            if (string.IsNullOrWhiteSpace(text))
            {
                Add(key, name, part.Datasheet.Name, part.Unit.Id, PlayReadinessState.NotEntered, "No rules text has been entered for this selected ability or enhancement.");
                return;
            }
            if (projected is null)
            {
                Add(key, name, part.Datasheet.Name, part.Unit.Id, PlayReadinessState.NotProjected, "The entered rule is missing from the merged Play unit. Check duplicate names and attachment configuration.");
                return;
            }
            if (projected.IsShootingChoice)
            {
                var enabled = effective.IsApplied(key);
                var mapped = AllWindows.Where(window => enabled && battle.ShootingOptionsFor(unit, window.Phase, window.Turn).Count > 0).ToList();
                var reviewed = IsReviewedReference(effective.FindSchedule(key), name, text);
                entries.Add(new(key, name, part.Datasheet.Name, part.Unit.Id,
                    enabled && mapped.Count > 0 ? PlayReadinessState.Mapped : reviewed ? PlayReadinessState.ReferenceReviewed : PlayReadinessState.NeedsSetup,
                    enabled ? "Extra options are in the choose-one shooting card." : reviewed ? "Extra options intentionally kept as reference only." : "Enable extra shooting options in setup; assigning the enhancement alone does not enable them.")
                {
                    ExpectedWindows = reviewed ? [] : [new(BattlePhase.Shooting, BattleTurn.Player)], ProjectedWindows = mapped,
                });
                return;
            }
            if (AuraParser.Parse(projected.Ability) is { } aura)
            {
                var recipients = battle.Units.Where(other => other.Id != unit.Id
                    && aura.AppliesTo(other.Parts.SelectMany(member => member.Datasheet.Keywords))).ToList();
                if (recipients.Count == 0)
                {
                    var self = aura.AppliesTo(unit.Parts.SelectMany(member => member.Datasheet.Keywords));
                    Add(key, name, part.Datasheet.Name, part.Unit.Id, self ? PlayReadinessState.Mapped : PlayReadinessState.NotApplicable,
                        self ? "The self-aura is applied to the unit; no other eligible recipients are fielded." : "No eligible aura recipients are fielded.");
                    return;
                }
                Scheduled(key, name, text, part.Datasheet.Name, part.Unit.Id,
                    (phase, turn) => focusMode && projected.Windows.Any(window => window.Phase == phase && window.Turn == turn));
                return;
            }
            if (projected.HasManualKeyword || projected.AppliedSummary is not null)
            {
                Add(key, name, part.Datasheet.Name, part.Unit.Id, PlayReadinessState.Mapped,
                    projected.HasManualKeyword ? "Represented by a manual keyword chip." : "Represented by the applied unit or weapon effect.");
                return;
            }
            if (projected.ApplyToUnit && !projected.CanApplyToUnit)
            {
                Add(key, name, part.Datasheet.Name, part.Unit.Id, PlayReadinessState.NeedsSetup, "Apply is enabled, but this rule has no applicable automatic effect. Review attachment or use manual timing.");
                return;
            }
            Scheduled(key, name, text, part.Datasheet.Name, part.Unit.Id,
                (phase, turn) => focusMode && BattleUnit.IsNowAction(projected, phase, turn));
        }

        foreach (var rosterUnit in roster.Units)
        {
            var sheet = catalogue.FindById(rosterUnit.DatasheetId);
            if (sheet is null || !parts.TryGetValue(rosterUnit.Id, out var item))
            {
                Add("unit|" + rosterUnit.DatasheetId, sheet?.Name ?? rosterUnit.DatasheetId, "Catalogue", rosterUnit.Id,
                    PlayReadinessState.NotEntered, "This unit could not be resolved into Play. Its datasheet or attachment data needs review.");
                continue;
            }
            var (unit, part) = item;
            foreach (var group in sheet.Abilities.GroupBy(ability => ability.Name, StringComparer.OrdinalIgnoreCase))
            {
                var ability = group.First();
                var key = AbilityScheduleKeys.ForUnitAbility(sheet.Id, ability.Name);
                knownKeys.Add(key);
                if (string.Equals(ability.Name, "Leader", StringComparison.OrdinalIgnoreCase))
                {
                    Add(key, ability.Name, sheet.Name, rosterUnit.Id, PlayReadinessState.Mapped, "Attachment information is represented by army setup.");
                    continue;
                }
                if (!WargearResolver.IsAbilityActive(sheet, rosterUnit, ability.Name))
                {
                    Add(key, ability.Name, sheet.Name, rosterUnit.Id, PlayReadinessState.NotApplicable, "The granting wargear is not selected.");
                    continue;
                }
                if (group.Count() > 1)
                {
                    Add(key, ability.Name, sheet.Name, rosterUnit.Id, PlayReadinessState.NotProjected, "Duplicate printed ability names make the source ambiguous.");
                    continue;
                }
                if (PhaseClassifier.IsOwnSaveRule(ability))
                {
                    var inv = PhaseClassifier.InvulnerableSaveScoped(ability);
                    var fnp = PhaseClassifier.FeelNoPainScoped(ability);
                    var mapped = (inv is { } invSave && unit.InvulnerableSaves.Any(save => save.Value == invSave.Value))
                        || (fnp is { } fnpSave && unit.FeelNoPains.Any(save => save.Value == fnpSave.Value));
                    Add(key, ability.Name, sheet.Name, rosterUnit.Id, mapped ? PlayReadinessState.Mapped : PlayReadinessState.NotProjected,
                        mapped ? "Represented by a save chip." : "The entered save rule is not represented by a save chip.");
                    continue;
                }
                if (part.ActiveSelfEffects.Any(effect => string.Equals(effect.SourceAbility, ability.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    Add(key, ability.Name, sheet.Name, rosterUnit.Id, PlayReadinessState.Mapped, "Represented by the permanent self-effect on the profile or weapons.");
                    continue;
                }
                AbilityEntry(unit, part, key, ability.Name, ability.Text, unit.CombinedAbilities.FirstOrDefault(projected => projected.Key == key));
            }

            foreach (var rule in sheet.FactionRules.Where(PhaseClassifier.IsUnitCoreAbility).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (sheet.Abilities.Any(ability => string.Equals(ability.Name, rule, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var mapped = unit.CoreAbilities.Any(ability => string.Equals(ability.Ability.Name, rule, StringComparison.OrdinalIgnoreCase));
                Add("core|" + sheet.Id + "|" + rule, rule, sheet.Name, rosterUnit.Id,
                    mapped ? PlayReadinessState.Mapped : PlayReadinessState.NotProjected, mapped ? "Represented by a core-ability chip." : "The core ability is not represented in Play.");
            }
            foreach (var weapon in part.Weapons.Where(weapon => weapon.Keywords.Count > 0))
                Add("weapon|" + rosterUnit.Id + "|" + weapon.Name, weapon.Name, sheet.Name, rosterUnit.Id, PlayReadinessState.Mapped, "Selected weapon abilities are displayed on the weapon profile.");
            foreach (var selection in rosterUnit.Wargear)
            {
                var group = sheet.WargearGroups.FirstOrDefault(group => group.Id == selection.GroupId);
                var ids = selection.OptionIds.Concat(selection.Counts.Select(count => count.OptionId));
                if (group is null || ids.Any(id => !group.Options.Any(option => option.Id == id)))
                    Add("wargear|" + rosterUnit.Id + "|" + selection.GroupId, "Wargear selection", sheet.Name, rosterUnit.Id, PlayReadinessState.NotEntered, "A selected wargear group or option is no longer in the catalogue.");
            }
            if (!string.IsNullOrEmpty(rosterUnit.AssignedEnhancementId))
            {
                var key = AbilityScheduleKeys.ForEnhancement(rosterUnit.AssignedEnhancementId);
                if (part.Enhancement is not { } enhancement)
                    Add(key, rosterUnit.AssignedEnhancementId, sheet.Name, rosterUnit.Id, PlayReadinessState.NotEntered, "The assigned enhancement has no definition in the selected detachments.");
                else
                    AbilityEntry(unit, part, key, enhancement.Name, enhancement.Text, unit.CombinedAbilities.FirstOrDefault(ability => ability.Key == key));
            }
        }

        foreach (var rule in ArmyRuleCatalogue.ForFaction(roster.Faction))
        {
            var key = AbilityScheduleKeys.ForArmyRule(rule.Name);
            Scheduled(key, rule.Name, rule.Text, "Army rule", null, (phase, turn) => effective.IsScheduledNow(key, phase, turn));
        }

        void StratagemEntry(string key, string name, string source, int cost, string when, string target, string effect,
            string? restrictions, IReadOnlyList<string> required, Func<BattlePhase, BattleTurn, bool> usable)
        {
            knownKeys.Add(key);
            if (!battle.ArmyHasAnyKeyword(required))
            {
                Add(key, name, source, null, PlayReadinessState.NotApplicable, "The army has none of the required unit keywords.");
                return;
            }
            if (string.IsNullOrWhiteSpace(when) || string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(effect))
            {
                Add(key, name, source, null, PlayReadinessState.NotEntered, "A required stratagem clause has not been entered.");
                return;
            }
            var recipients = battle.Units.Where(unit => StratagemTargeting.AppliesTo(target,
                unit.Parts.SelectMany(part => part.Datasheet.Keywords), vocabulary)).ToList();
            Scheduled(key, name, StratagemText(cost, when, target, effect, restrictions), source, null,
                (phase, turn) => usable(phase, turn) && (!focusMode || recipients.Count > 0));
            if (recipients.Count == 0)
                Add(key + "|target", name + " targets", source, null, PlayReadinessState.NotProjected,
                    "Army-level eligibility passes, but the target filter finds no compatible unit. Review target keywords and exclusions.");
            if (HasConfiguration(effective.FindSchedule(key)) && (when + target + effect + restrictions).Contains("once per battle round", StringComparison.OrdinalIgnoreCase))
                Add(key + "|usage", name + " usage limit", source, null, PlayReadinessState.NotProjected,
                    "The current spent tracker treats this per-round wording as once-per-battle. Review this limitation before play.");
        }

        foreach (var stratagem in CoreStratagemCatalogue.All)
            StratagemEntry(AbilityScheduleKeys.ForCoreStratagem(stratagem.Id), stratagem.Name, "Core stratagem", stratagem.Cost,
                stratagem.When, stratagem.Target, stratagem.Effect, stratagem.Restrictions, stratagem.RequiredUnitKeywords,
                (phase, turn) => battle.CoreStratagemUsable(stratagem, phase, turn));

        foreach (var id in selectedDetachments)
        {
            var detachment = battle.Detachments.FirstOrDefault(detachment => string.Equals(detachment.Id, id, StringComparison.OrdinalIgnoreCase));
            if (detachment is null)
            {
                Add("detachment|" + id, id, "Detachment", null, PlayReadinessState.NotEntered, "The selected detachment has no definition.");
                continue;
            }
            if (detachment.Stratagems.Count == 0)
                Add("detachment|" + id, detachment.Name, "Selected content", null, PlayReadinessState.NotEntered,
                    "No stratagem content is entered for this selected detachment. This is an upload gap, not an application defect.");
            foreach (var stratagem in detachment.Stratagems)
                StratagemEntry(AbilityScheduleKeys.ForDetachmentStratagem(id, stratagem.Id), stratagem.Name, detachment.Name, stratagem.CpCost,
                    stratagem.When, stratagem.Target, stratagem.Effect, null, stratagem.RequiredUnitKeywords,
                    (phase, turn) => battle.DetachmentStratagemUsable(detachment, stratagem, phase, turn));
            foreach (var rule in detachment.Rules)
            {
                var key = AbilityScheduleKeys.ForDetachmentRule(id, rule.Name);
                knownKeys.Add(key);
                var choices = detachment.WeaponChoices.Where(choice => choice.Name == rule.Name).ToList();
                if (choices.Count > 0)
                {
                    var eligible = battle.Units.Any(unit => battle.WeaponChoicesFor(unit).Any(choice => choices.Contains(choice)));
                    entries.Add(new(key, rule.Name, detachment.Name, null, eligible ? PlayReadinessState.Mapped : PlayReadinessState.NotApplicable,
                        eligible ? "Represented by the choose-one shooting card and passive weapon grants." : "No unit qualifies for this shooting choice.")
                    {
                        ExpectedWindows = eligible ? [new(BattlePhase.Shooting, BattleTurn.Player)] : [],
                        ProjectedWindows = eligible ? [new(BattlePhase.Shooting, BattleTurn.Player)] : [],
                    });
                }
                else if (rule.ConditionalBuffs.Count == 0)
                {
                    var reviewed = IsReviewedReference(effective.FindSchedule(key), rule.Name, rule.Text);
                    Add(key, rule.Name, detachment.Name, null, string.IsNullOrWhiteSpace(rule.Text) ? PlayReadinessState.NotEntered
                        : reviewed ? PlayReadinessState.ReferenceReviewed : PlayReadinessState.NeedsSetup,
                        reviewed ? "Reviewed as reference only; any existing passive modifiers remain represented on profiles."
                            : "This rule is available as reference, not as a complete Now action. Review it explicitly in setup.");
                }
                foreach (var buff in rule.ConditionalBuffs)
                {
                    var buffKey = AbilityScheduleKeys.ForDetachmentBuff(id, buff.Label);
                    knownKeys.Add(buffKey);
                    var recipients = battle.Units.Where(unit => BattleRoster.BuffAppliesTo(unit, buff)).ToList();
                    if (recipients.Count == 0)
                        Add(buffKey, buff.Label, detachment.Name, null, PlayReadinessState.NotApplicable, "No unit matches this buff's required/excluded keywords.");
                    else
                        Scheduled(buffKey, buff.Label, buff.Effect, detachment.Name, recipients[0].Id,
                            (phase, turn) => focusMode && recipients.All(unit => battle.ConditionalBuffsFor(unit, phase, turn).Contains(buff)));
                }
            }
        }

        foreach (var schedule in effective.AbilitySchedules)
        {
            var segments = schedule.Key.Split('|');
            var inScope = segments.Length >= 2 && ((segments[0] == "enh" && selectedEnhancements.Contains(segments[1]))
                || (segments[0] == "armyrule" && roster.Faction == Roster.NecronsFaction)
                || (segments.Length >= 3 && (segments[0] == "unit" && selectedSheets.Contains(segments[1])
                    || segments[0] is "strat" or "detbuff" or "detrule" && (segments[1] == "core" || selectedDetachments.Contains(segments[1])))));
            if (inScope && !knownKeys.Contains(schedule.Key))
                Add(schedule.Key, "Unmatched setup entry", "Scheduling library", null, PlayReadinessState.NotEntered, "This selected army's schedule key no longer matches entered rule content. Review the old entry.");
        }
        if (!focusMode)
            Add("view|stacked", "Stacked-list coverage", "Play layout", null, PlayReadinessState.NotProjected,
                "This layout currently omits some unit actions and command reminders. Use focused cards for complete configured unit coverage.");

        return new(validation, entries);
    }
}
