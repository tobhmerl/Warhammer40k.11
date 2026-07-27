using System.Text.Json;
using System.Text.Json.Serialization;
using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters.Validation;

namespace Warhammer40k.Core.Rosters;

/// <summary>
/// Serialises a roster as a single self-describing JSON document: every unit with its attached leaders,
/// resolved abilities, invulnerable / Feel No Pain saves, enhancements, weapon profiles (base and buffed),
/// plus the detachment rules, stratagems and army rules that apply. Intended for sharing the army with a
/// human or a language model, so it favours completeness and plain wording over compactness.
/// </summary>
/// <remarks>
/// Built on <see cref="BattleRoster"/> so the export shows exactly what Play Mode resolves — there is no
/// second interpretation of the rules to keep in sync. Conferrals are all treated as active
/// (<see cref="RosterConferrals.WithAllApplied"/>), so the numbers are the unit's full potential.
/// </remarks>
public static class RosterExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Builds the export document for a roster.</summary>
    public static RosterExport Build(Roster roster, CatalogueData catalogue)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(catalogue);

        var detachments = roster.EffectiveDetachmentIds
            .Select(DetachmentCatalogue.FindById)
            .OfType<Detachment>()
            .ToList();

        var battle = BattleRoster.Build(RosterConferrals.WithAllApplied(roster, catalogue), catalogue, detachments);

        return new RosterExport
        {
            Roster = new RosterHeaderExport
            {
                Name = roster.Name,
                Faction = roster.Faction,
                PointsLimit = roster.PointsLimit,
                TotalPoints = RosterCalculator.TotalPoints(roster, catalogue, detachments),
                DetachmentPointsSpent = detachments.Sum(d => d.DetachmentPoints),
                DetachmentPointsBudget = DetachmentCatalogue.Budget(roster.PointsLimit),
            },
            Detachments = detachments.Select(ToExport).ToList(),
            ArmyRules = ArmyRuleCatalogue.ForFaction(roster.Faction)
                .Select(r => new ArmyRuleExport { Name = r.Name, Text = r.Text, Example = string.IsNullOrWhiteSpace(r.Example) ? null : r.Example })
                .ToList(),
            CoreStratagems = CoreStratagemCatalogue.All
                .Where(s => battle.ArmyHasAnyKeyword(s.RequiredUnitKeywords))
                .Select(ToExport)
                .ToList(),
            Units = battle.Units.Select(u => ToExport(battle, u, roster, catalogue)).ToList(),
        };
    }

    /// <summary>Builds the export document and serialises it to indented JSON.</summary>
    public static string ToJson(Roster roster, CatalogueData catalogue) =>
        JsonSerializer.Serialize(Build(roster, catalogue), Options);

    private static DetachmentExport ToExport(Detachment d) => new()
    {
        Name = d.Name,
        DetachmentPoints = d.DetachmentPoints,
        Disposition = string.IsNullOrWhiteSpace(d.Disposition) ? null : d.Disposition,
        UniqueTags = d.Tags.Count > 0 ? d.Tags : null,
        Rules = d.Rules.Select(r => new NamedTextExport { Name = r.Name, Text = r.Text }).ToList(),
        Enhancements = d.Enhancements
            .Select(e => new EnhancementExport { Name = e.Name, Points = e.Points, Text = e.Text, Scope = e.Scope.ToString() })
            .ToList(),
        Stratagems = d.Stratagems.Select(ToExport).ToList(),
    };

    private static StratagemExport ToExport(Stratagem s) => new()
    {
        Name = s.Name,
        Type = string.IsNullOrWhiteSpace(s.Type) ? null : s.Type,
        CommandPoints = s.CpCost,
        UsedIn = TurnLabel(s.Turn),
        Phases = PhaseLabels(s.Phases),
        RequiresKeywords = s.RequiredUnitKeywords.Count > 0 ? s.RequiredUnitKeywords : null,
        When = s.When,
        Target = s.Target,
        Effect = s.Effect,
    };

    private static StratagemExport ToExport(CoreStratagem s) => new()
    {
        Name = s.Name,
        CommandPoints = s.Cost,
        UsedIn = TurnLabel(s.Turn),
        Phases = PhaseLabels(s.Phases),
        RequiresKeywords = s.RequiredUnitKeywords.Count > 0 ? [.. s.RequiredUnitKeywords] : null,
        When = s.When,
        Target = s.Target,
        Effect = s.Effect,
        Restrictions = s.Restrictions,
    };

    private static UnitExport ToExport(BattleRoster battle, BattleUnit unit, Roster roster, CatalogueData catalogue)
    {
        var applied = new List<string>();

        return new UnitExport
        {
            Name = unit.Name,
            Points = unit.Parts.Sum(p => PointsFor(p, roster, catalogue)),
            IsWarlord = unit.IsWarlord ? true : null,
            IsAttachedUnit = unit.Parts.Count > 1 ? true : null,
            TotalModels = unit.ModelCount,
            Keywords = unit.Parts.SelectMany(p => p.Datasheet.Keywords).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            InvulnerableSaves = unit.InvulnerableSaves.Select(ToExport).ToList(),
            FeelNoPains = unit.FeelNoPains.Select(ToExport).ToList(),
            ConferredUnitAbilities = Nullable(battle.ConferredUnitAbilities(unit)),
            Abilities = unit.CombinedAbilities.Select(a => new AbilityExport
            {
                Name = a.Ability.Name,
                Text = a.Ability.Text,
                From = a.Source,
                IsEnhancement = a.IsEnhancement ? true : null,
                Applies = a.ConferredSummary,
            }).ToList(),
            CoreAbilities = Nullable(unit.CoreAbilities.Select(a => a.Ability.Name).ToList()),
            Models = unit.Parts.Select(p => ToExport(battle, unit, p, applied)).ToList(),
            AppliedModifiers = Nullable(applied),
        };
    }

    private static ModelGroupExport ToExport(BattleRoster battle, BattleUnit unit, BattlePart part, List<string> applied)
    {
        var profile = part.Profile;
        var unitMods = battle.UnitStatModifiers(unit, part);
        foreach (var mod in unitMods)
            AddOnce(applied, $"{mod.Describe()} ({part.Datasheet.Name})");

        return new ModelGroupExport
        {
            Name = part.Datasheet.Name,
            IsLeader = part.IsLeader ? true : null,
            Models = part.ModelCount,
            WoundsPerModel = part.WoundsPerModel ?? 0,
            Enhancement = part.Enhancement is { } e
                ? new EnhancementExport { Name = e.Name, Points = e.Points, Text = e.Text, Scope = e.Scope.ToString() }
                : null,
            Statline = new StatlineExport
            {
                Move = Buffed(profile?.Move, unitMods, StatTarget.Move),
                Toughness = Buffed(profile?.Toughness, unitMods, StatTarget.Toughness),
                Save = Buffed(profile?.Save, unitMods, StatTarget.Save),
                Wounds = profile?.Wounds ?? "",
                Leadership = Buffed(profile?.Leadership, unitMods, StatTarget.Leadership),
                ObjectiveControl = Buffed(profile?.ObjectiveControl, unitMods, StatTarget.ObjectiveControl),
            },
            BaseStatline = profile is null ? null : new StatlineExport
            {
                Move = profile.Move,
                Toughness = profile.Toughness,
                Save = profile.Save,
                Wounds = profile.Wounds,
                Leadership = profile.Leadership,
                ObjectiveControl = profile.ObjectiveControl,
            },
            Weapons = part.Weapons.Select(w => ToExport(battle, unit, part, w, applied)).ToList(),
        };
    }

    private static WeaponExport ToExport(BattleRoster battle, BattleUnit unit, BattlePart part, WeaponProfile w, List<string> applied)
    {
        var ranged = !w.Type.Equals("Melee", StringComparison.OrdinalIgnoreCase);
        var mods = battle.WeaponStatModifiers(unit, part, ranged);
        foreach (var mod in mods)
            AddOnce(applied, $"{mod.Describe()} ({(ranged ? "ranged" : "melee")})");

        var granted = battle.GrantedWeaponAbilities(unit, part, ranged);
        foreach (var ability in granted)
            AddOnce(applied, $"[{ability.ToUpperInvariant()}] ({(ranged ? "ranged" : "melee")})");

        var critOn = battle.CriticalHitOn(unit, part, ranged);
        if (critOn is { } crit)
            AddOnce(applied, $"Critical hit {crit}+ ({(ranged ? "ranged" : "melee")})");

        return new WeaponExport
        {
            Name = w.Name,
            Type = ranged ? "Ranged" : "Melee",
            Range = w.Range,
            Attacks = Buffed(w.Attacks, mods, StatTarget.Attacks),
            Skill = Buffed(w.Skill, mods, StatTarget.Skill),
            Strength = Buffed(w.Strength, mods, StatTarget.Strength),
            ArmourPenetration = w.ArmourPenetration,
            Damage = Buffed(w.Damage, mods, StatTarget.Damage),
            Abilities = Nullable(w.Keywords.Concat(granted).Distinct(StringComparer.OrdinalIgnoreCase).ToList()),
            CriticalHitOn = critOn,
            ModelsCarrying = part.ModelsCarrying(w),
            WeaponsPerModel = w.Count > 1 ? w.Count : null,
        };
    }

    private static SaveExport ToExport(SaveBadge badge) => new()
    {
        Value = badge.Value,
        AppliesTo = badge.UnitWide ? "unit" : badge.ModelName ?? "model",
    };

    private static int PointsFor(BattlePart part, Roster roster, CatalogueData catalogue)
    {
        var rank = RosterCalculator.CopyRank(roster, part.Unit);
        return RosterCalculator.UnitPoints(part.Unit, catalogue.FindById(part.Unit.DatasheetId), rank)
            + RosterCalculator.EnhancementPoints(part.Unit, roster.EffectiveDetachmentIds.Select(DetachmentCatalogue.FindById).OfType<Detachment>().ToList())
            + part.Unit.BindingSurcharge;
    }

    private static string Buffed(string? raw, IReadOnlyList<StatModifier> mods, StatTarget target)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw ?? "";
        var applicable = mods.Where(m => m.Target == target).ToList();
        return applicable.Count == 0 ? raw : StatMath.ApplyAll(raw, applicable);
    }

    private static void AddOnce(List<string> list, string value)
    {
        if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
            list.Add(value);
    }

    private static List<string>? Nullable(IReadOnlyList<string> values) =>
        values.Count == 0 ? null : [.. values];

    private static string TurnLabel(StratagemTurn turn) => turn switch
    {
        StratagemTurn.Your => "your turn",
        StratagemTurn.Opponent => "opponent's turn",
        _ => "either turn",
    };

    private static List<string>? PhaseLabels(IReadOnlyList<BattlePhase> phases) =>
        phases.Count == 0 ? null : phases.Select(BattlePhases.Label).ToList();
}
