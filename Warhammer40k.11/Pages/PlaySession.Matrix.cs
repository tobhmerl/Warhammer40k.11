using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k._11.Pages;

// ---- Battle Matrix -------------------------------------------------------------------------------
// A second rendering of the army overview: units down the side, the rules that are relevant in the
// current phase + turn across the top, and an X where a rule applies. It answers "who can do what
// right now" in one glance instead of repeating the same stratagem card once per unit.
//
// It derives everything from the same helpers the Now ribbon and the card overview use
// (PhaseTurnStratagems, StratagemAppliesTo, CanAfford, CombinedAbilities + IsUsableNow/IsEffectNow,
// ConditionalBuffsFor, UsableArmyRules, NeedsBattleShock/NeedsReanimation), so the three surfaces
// can never disagree about what is relevant.
public partial class PlaySession
{
    // The kind of rule a column carries. Drives the group it sits under and its colour accent.
    private enum MatrixKind
    {
        Reminder,
        ArmyRule,
        Stratagem,
        Ability,
        Aura,
        Buff,
    }

    // One column: a single rule, listed once no matter how many units it covers.
    // Applies maps unit id -> the per-unit payload needed to open that rule focused on that unit
    // (abilities and buffs are per-unit objects; stratagems and army rules are army-wide).
    private sealed record MatrixColumn(
        string Key,
        MatrixKind Kind,
        string Label,
        string Detail,
        int? Cp,
        IReadOnlyDictionary<string, object?> Applies)
    {
        public int Count => Applies.Count;

        public bool AppliesTo(BattleUnit unit) => Applies.ContainsKey(unit.Id);
    }

    // A titled run of columns (Must resolve / Army rules / Stratagems / Unit abilities).
    private sealed record MatrixGroup(string Title, MatrixKind Kind, IReadOnlyList<MatrixColumn> Columns);

    // One row: a live unit plus how many of the columns apply to it.
    private sealed record MatrixRow(BattleUnit Unit, int Count);

    private sealed record MatrixModel(IReadOnlyList<MatrixGroup> Groups, IReadOnlyList<MatrixRow> Rows)
    {
        public IReadOnlyList<MatrixColumn> Columns { get; } = Groups.SelectMany(g => g.Columns).ToList();

        public int ColumnCount => Columns.Count;
    }

    // Builds the matrix for the current phase + turn. One pass over the live units per rule family.
    private MatrixModel BuildMatrix()
    {
        if (_battle is null)
            return new MatrixModel([], []);

        var live = OrderedUnits.Where(u => !IsDead(u)).ToList();
        var groups = new List<MatrixGroup>();

        // Must-resolve reminders — only ever in your Command phase, so the group vanishes elsewhere.
        var reminders = new List<MatrixColumn>();
        AddReminderColumn(reminders, "Battle-shock", "Take a Battle-shock test for this unit.", live, NeedsBattleShock);
        AddReminderColumn(reminders, "Reanimation", "Resolve Reanimation Protocols for this unit.", live, NeedsReanimation);
        if (reminders.Count > 0)
            groups.Add(new MatrixGroup("Must resolve", MatrixKind.Reminder, reminders));

        // Army rules are faction-wide: when scheduled for this window they apply to every live unit.
        var armyRules = UsableArmyRules
            .Select(rule => new MatrixColumn(
                "R|" + rule.Name,
                MatrixKind.ArmyRule,
                rule.Name,
                "Army rule",
                null,
                live.ToDictionary(u => u.Id, _ => (object?)rule, StringComparer.Ordinal)))
            .ToList();
        if (armyRules.Count > 0)
            groups.Add(new MatrixGroup("Army rules", MatrixKind.ArmyRule, armyRules));

        // Stratagems: one column each, holding every unit that can legally use it and can pay for it.
        var stratagems = BuildStratagemColumns(live);
        if (stratagems.Count > 0)
            groups.Add(new MatrixGroup("Stratagems", MatrixKind.Stratagem, stratagems));

        // Auras projected by one unit onto others: one column per aura, holding every unit it could cover.
        // They sit right after the stratagems and ahead of the ordinary abilities — an aura is resolved before
        // a unit's own rules and reaches much of the army, so it earns the more prominent spot.
        var auras = BuildAuraColumns(live);
        if (auras.Count > 0)
            groups.Add(new MatrixGroup("Auras", MatrixKind.Aura, auras));

        // Unit abilities and detachment buffs, merged by name so a shared ability is one column.
        var abilities = BuildAbilityColumns(live);
        if (abilities.Count > 0)
            groups.Add(new MatrixGroup("Unit abilities", MatrixKind.Ability, abilities));

        var all = groups.SelectMany(g => g.Columns).ToList();
        var rows = live
            .Select(u => new MatrixRow(u, all.Count(c => c.AppliesTo(u))))
            .ToList();

        return new MatrixModel(groups, rows);
    }

    private void AddReminderColumn(
        List<MatrixColumn> into,
        string label,
        string detail,
        IReadOnlyList<BattleUnit> live,
        Func<BattleUnit, bool> needed)
    {
        if (!MyCommandPhase)
            return;
        var applies = live
            .Where(needed)
            .ToDictionary(u => u.Id, _ => (object?)null, StringComparer.Ordinal);
        if (applies.Count > 0)
            into.Add(new MatrixColumn("M|" + label, MatrixKind.Reminder, label, detail, null, applies));
    }

    private List<MatrixColumn> BuildStratagemColumns(IReadOnlyList<BattleUnit> live)
    {
        // Same starting set as the card overview: this phase + turn, minus any already spent once-per-battle.
        var phaseStrats = PhaseTurnStratagems()
            .Where(s => !(IsOncePerBattleStrat(s) && IsOncePerBattleUsed(OncePerBattleStratKey(s))))
            .ToList();

        var columns = new List<MatrixColumn>();
        foreach (var strat in phaseStrats)
        {
            var applies = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var unit in live)
                if (StratagemAppliesTo(unit, strat.Target) && CanAfford(strat, unit))
                    applies[unit.Id] = strat;
            if (applies.Count == 0)
                continue;
            columns.Add(new MatrixColumn(
                StratKey(strat),
                MatrixKind.Stratagem,
                strat.Name,
                strat.Source,
                strat.Cost,
                applies));
        }

        return columns
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<MatrixColumn> BuildAbilityColumns(IReadOnlyList<BattleUnit> live)
    {
        // Keyed by name so an ability several units share becomes one column, while the payload map
        // still holds each unit's own BattleAbility for the focused detail sheet.
        var byKey = new Dictionary<string, (MatrixKind Kind, string Label, string Detail, Dictionary<string, object?> Applies)>(StringComparer.Ordinal);

        void Add(string key, MatrixKind kind, string label, string detail, BattleUnit unit, object payload)
        {
            if (!byKey.TryGetValue(key, out var entry))
                byKey[key] = entry = (kind, label, detail, new Dictionary<string, object?>(StringComparer.Ordinal));
            entry.Applies[unit.Id] = payload;
        }

        foreach (var unit in live)
        {
            foreach (var ability in unit.CombinedAbilities)
            {
                if (!(IsEffectNow(ability) || IsUsableNow(ability)) || IsOncePerBattleUsedAbility(unit, ability))
                    continue;
                var kind = HasEffectKeywords(ability) ? "Effect" : ability.IsEnhancement ? "Enhancement" : "Ability";
                Add("A|" + ability.Ability.Name, MatrixKind.Ability, ability.Ability.Name, kind, unit, ability);
            }

            foreach (var buff in ConditionalBuffsFor(unit))
                Add("B|" + buff.Label, MatrixKind.Buff, buff.Label, "Detachment", unit, buff);
        }

        return byKey
            .Select(kv => new MatrixColumn(kv.Key, kv.Value.Kind, kv.Value.Label, kv.Value.Detail, null, kv.Value.Applies))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // One column per aura in the army, listing every unit it could cover. The bearer's own unit is left out:
    // it stands in its own bubble, so the effect is already applied to it and needs no confirmation.
    private List<MatrixColumn> BuildAuraColumns(IReadOnlyList<BattleUnit> live)
    {
        var byKey = new Dictionary<string, (string Label, string Detail, Dictionary<string, object?> Applies)>(StringComparer.Ordinal);

        foreach (var unit in live)
            foreach (var offer in ForeignAuraOffersFor(unit))
            {
                var key = string.Join('|', "U", offer.Source.Id, offer.Ability.Ability.Name);
                if (!byKey.TryGetValue(key, out var entry))
                    byKey[key] = entry = (offer.Ability.Ability.Name, AuraSource(offer), new Dictionary<string, object?>(StringComparer.Ordinal));
                entry.Applies[unit.Id] = offer;
            }

        return byKey
            .Select(kv => new MatrixColumn(kv.Key, MatrixKind.Aura, kv.Value.Label, kv.Value.Detail, null, kv.Value.Applies))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ---- View state ----------------------------------------------------------------------------

    // The colour accent a group / column / cell carries, so a rule's type reads without a legend.
    private static string GroupClass(MatrixKind kind) => kind switch
    {
        MatrixKind.Reminder => "k-must",
        MatrixKind.ArmyRule => "k-rule",
        MatrixKind.Stratagem => "k-strat",
        MatrixKind.Aura => "k-aura",
        _ => "k-ability",
    };

    // The overview always renders as the matrix — it is the faster surface to scan, and the layout toggle
    // was dropped to keep the page uncluttered. The card layout stays behind this flag for reference.
    private readonly bool _overviewMatrix = true;

    // Quick filter: hide units that have nothing at all in this battle window.
    private bool _matrixOnlyActive;

    // Row / column focus, so a tapped unit or rule stays highlighted while reading across the grid.
    private string? _matrixRowUnitId;
    private string? _matrixColumnKey;

    private void ToggleMatrixOnlyActive() => _matrixOnlyActive = !_matrixOnlyActive;

    private void FocusMatrixColumn(MatrixColumn column) =>
        _matrixColumnKey = _matrixColumnKey == column.Key ? null : column.Key;

    private IReadOnlyList<MatrixRow> VisibleMatrixRows(MatrixModel matrix) =>
        _matrixOnlyActive ? matrix.Rows.Where(r => r.Count > 0).ToList() : matrix.Rows;

    // ---- Interaction ---------------------------------------------------------------------------

    // A column header opens the rule on its own: no unit context, so a stratagem shows its printed cost.
    private void OpenMatrixColumn(MatrixColumn column)
    {
        _matrixColumnKey = column.Key;
        switch (column.Kind)
        {
            case MatrixKind.ArmyRule when column.Applies.Values.FirstOrDefault() is ArmyRule rule:
                OpenNowRule(rule);
                break;
            case MatrixKind.Stratagem when column.Applies.Values.FirstOrDefault() is StratView strat:
                OpenGeneralStratagem(strat);
                break;
            case MatrixKind.Ability or MatrixKind.Buff:
                // Abilities and buffs only ever read in the context of a bearer, so the sheet opens for
                // the first unit that has it; the cells open it for the unit you actually tapped.
                OpenMatrixCellFor(column, column.Applies.Keys.First());
                break;
        }
    }

    // A cell opens the same rule, but focused on the unit whose row it sits in.
    private void OpenMatrixCell(MatrixColumn column, BattleUnit unit)
    {
        _matrixRowUnitId = unit.Id;
        _matrixColumnKey = column.Key;
        switch (column.Applies.TryGetValue(unit.Id, out var payload) ? payload : null)
        {
            case ArmyRule rule:
                OpenNowRule(rule);
                break;
            case StratView strat:
                OpenOverviewStratagem(unit, strat);
                break;
            case BattleAbility ability:
                OpenAbilityCard(unit, ability);
                break;
            case AuraOffer aura:
                OpenAuraCard(unit, aura);
                break;
            case ConditionalUnitBuff buff:
                OpenBuffCard(unit, buff);
                break;
            default:
                // A reminder has no sheet of its own — jump to the unit's card to resolve it there.
                FocusReminder(unit);
                break;
        }
    }

    private void OpenMatrixCellFor(MatrixColumn column, string unitId)
    {
        if (_battle?.Units.FirstOrDefault(u => u.Id == unitId) is { } unit)
            OpenMatrixCell(column, unit);
    }

    // Tapping a unit name leaves the overview and opens that unit's card.
    private void OpenMatrixUnit(BattleUnit unit)
    {
        _matrixRowUnitId = unit.Id;
        FocusReminder(unit);
    }
}
