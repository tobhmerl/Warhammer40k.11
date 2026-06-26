using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Core.Play;

/// <summary>
/// A roster reshaped for play: units resolved against the catalogue, attached Leaders merged into the
/// bodyguard they lead (so a led unit is commanded as one), and content pre-split by battle phase.
/// Pure and deterministic — built once from a <see cref="Roster"/> + <see cref="CatalogueData"/>.
/// </summary>
public sealed class BattleRoster
{
    private BattleRoster(IReadOnlyList<BattleUnit> units, IReadOnlyList<Detachment> detachments, Roster roster)
    {
        Units = units;
        Detachments = detachments;
        Source = roster;
        ArmyKeywords = units
            .SelectMany(u => u.Parts)
            .SelectMany(p => p.Datasheet.Keywords)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The combat groups, in roster order (a group is a unit plus any Leaders attached to it).</summary>
    public IReadOnlyList<BattleUnit> Units { get; }

    /// <summary>The detachments selected for this roster — drive the smart weapon-ability effects in Play Mode.</summary>
    public IReadOnlyList<Detachment> Detachments { get; }

    /// <summary>The underlying roster — carries the player's manual ability/stratagem <see cref="AbilitySchedule"/>s.</summary>
    public Roster Source { get; }

    /// <summary>
    /// Every unit keyword fielded anywhere in this army (case-insensitive). Drives the "need to know" filtering
    /// of stratagems: one whose <see cref="CoreStratagem.RequiredUnitKeywords"/> are all absent here cannot be
    /// used, so it is hidden in Play Mode (e.g. Smokescreen with no SMOKE unit, Explosives with no GRENADES).
    /// </summary>
    public IReadOnlySet<string> ArmyKeywords { get; }

    /// <summary>
    /// True when the army can field a unit eligible for a stratagem requiring <paramref name="requiredKeywords"/>:
    /// either the requirement is empty (any unit qualifies) or at least one required keyword is present in the army.
    /// </summary>
    public bool ArmyHasAnyKeyword(IReadOnlyList<string> requiredKeywords) =>
        requiredKeywords is null or { Count: 0 }
        || requiredKeywords.Any(ArmyKeywords.Contains);

    /// <summary>
    /// True when a Core Stratagem should surface right now: the army is eligible (has any required keyword)
    /// AND the player has manually scheduled it for the current <paramref name="phase"/> + <paramref name="turn"/>.
    /// Scheduling is entirely manual — an unconfigured stratagem never surfaces.
    /// </summary>
    public bool CoreStratagemUsable(CoreStratagem stratagem, BattlePhase phase, BattleTurn turn) =>
        ArmyHasAnyKeyword(stratagem.RequiredUnitKeywords)
        && Source.IsScheduledNow(AbilityScheduleKeys.ForCoreStratagem(stratagem.Id), phase, turn);

    /// <summary>
    /// True when a detachment stratagem should surface right now: the army is eligible AND the player has
    /// manually scheduled it for the current <paramref name="phase"/> + <paramref name="turn"/>.
    /// </summary>
    public bool DetachmentStratagemUsable(Detachment detachment, Stratagem stratagem, BattlePhase phase, BattleTurn turn) =>
        ArmyHasAnyKeyword(stratagem.RequiredUnitKeywords)
        && Source.IsScheduledNow(AbilityScheduleKeys.ForDetachmentStratagem(detachment.Id, stratagem.Id), phase, turn);

    /// <summary>Builds a battle roster, skipping units whose datasheet is missing from the catalogue.</summary>
    public static BattleRoster Build(Roster roster, CatalogueData catalogue)
    {
        ArgumentNullException.ThrowIfNull(roster);
        var detachments = roster.EffectiveDetachmentIds
            .Select(DetachmentCatalogue.FindById)
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList();
        return Build(roster, catalogue, detachments);
    }

    /// <summary>
    /// Builds a battle roster against an explicit set of <paramref name="detachments"/> (already resolved by
    /// the caller, or supplied by a test). The two-argument overload resolves them from
    /// <see cref="DetachmentCatalogue"/>.
    /// </summary>
    public static BattleRoster Build(Roster roster, CatalogueData catalogue, IReadOnlyList<Detachment> detachments)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(detachments);

        // First pass: a part for every roster unit that resolves to a datasheet, keyed by roster-unit id.
        var parts = new Dictionary<string, BattlePart>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var unit in roster.Units)
        {
            var sheet = catalogue.FindById(unit.DatasheetId);
            if (sheet is null)
                continue;
            parts[unit.Id] = new BattlePart(unit, sheet, isLeader: false);
            order.Add(unit.Id);
        }

        // Resolve each character's setup-assigned Enhancement against the selected detachments, so Play Mode
        // can surface it on the bearer's card as an ability (live stat change or rules text).
        foreach (var unit in roster.Units)
        {
            if (string.IsNullOrEmpty(unit.AssignedEnhancementId))
                continue;
            if (!parts.TryGetValue(unit.Id, out var bearer))
                continue;
            bearer.Enhancement = detachments
                .Select(d => d.FindEnhancement(unit.AssignedEnhancementId))
                .FirstOrDefault(e => e is not null);
        }

        // Second pass: fold attached Leaders into their bodyguard; a dangling attachment stays standalone.
        var attachedToHost = new Dictionary<string, List<BattlePart>>(StringComparer.Ordinal);
        var absorbed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var unit in roster.Units)
        {
            if (string.IsNullOrEmpty(unit.AttachedToRosterUnitId))
                continue;
            if (!parts.TryGetValue(unit.Id, out var leaderPart))
                continue;
            if (!parts.ContainsKey(unit.AttachedToRosterUnitId))
                continue; // dangling → leave the leader as its own group

            leaderPart.IsLeader = true;
            if (!attachedToHost.TryGetValue(unit.AttachedToRosterUnitId, out var list))
                attachedToHost[unit.AttachedToRosterUnitId] = list = new List<BattlePart>();
            list.Add(leaderPart);
            absorbed.Add(unit.Id);
        }

        var units = new List<BattleUnit>();
        foreach (var id in order)
        {
            if (absorbed.Contains(id))
                continue;
            var primary = parts[id];
            var members = new List<BattlePart> { primary };
            if (attachedToHost.TryGetValue(id, out var leaders))
                members.AddRange(leaders);
            units.Add(new BattleUnit(members, roster));
        }

        return new BattleRoster(units, detachments, roster);
    }

    /// <summary>
    /// Parses a Wounds characteristic into a fixed number, or null when it is variable (e.g. "D6", "D3+1")
    /// and therefore cannot be tracked numerically.
    /// </summary>
    public static int? ParseWounds(string? wounds)
    {
        if (string.IsNullOrWhiteSpace(wounds))
            return null;
        return int.TryParse(wounds.Trim(), out var value) && value > 0 ? value : null;
    }

    /// <summary>
    /// Passive weapon abilities granted to this part's ranged (or melee) weapons, from two sources: a
    /// <b>detachment</b> grant matched by keyword (e.g. CRYPTEK models gain [ASSAULT]), and an attached
    /// <b>Leader's</b> conferral (e.g. a Skorpekh Lord gives the unit it leads [LETHAL HITS] on melee weapons).
    /// Detachment grants target the model by keyword so they never spill onto a bodyguard; a Leader's conferral
    /// applies to every model in the unit it leads.
    /// </summary>
    public IReadOnlyList<string> GrantedWeaponAbilities(BattleUnit unit, BattlePart part, bool ranged)
    {
        var result = new List<string>();
        foreach (var detachment in Detachments)
        {
            foreach (var grant in detachment.WeaponGrants)
            {
                if (!ClassMatches(grant.WeaponClass, ranged))
                    continue;

                var applies = grant.Scope == GrantScope.Unit
                    ? unit.Parts.Any(p => MatchesAny(p, grant.Keywords) && !HasAnyKeyword(p, grant.ExcludedKeywords))
                    : MatchesAny(part, grant.Keywords) && !HasAnyKeyword(part, grant.ExcludedKeywords);
                if (!applies)
                    continue;

                foreach (var ability in grant.Abilities)
                {
                    if (!result.Contains(ability, StringComparer.OrdinalIgnoreCase))
                        result.Add(ability);
                }
            }
        }

        // Leader-conferred weapon abilities apply to every model in the led unit (incl. the Leader itself),
        // but only when the player ticked "Apply to unit" for that Leader ability in setup.
        foreach (var leader in unit.Parts.Where(p => p.IsLeader))
        {
            foreach (var conferral in leader.Datasheet.LeaderConferrals)
            {
                if (!ClassMatches(conferral.WeaponClass, ranged))
                    continue;
                if (!ConferralApplied(leader, conferral))
                    continue;
                foreach (var ability in conferral.WeaponAbilities)
                {
                    if (!result.Contains(ability, StringComparer.OrdinalIgnoreCase))
                        result.Add(ability);
                }
            }
        }

        // A model's own permanent self-effects (e.g. Tomb Blades' Nebuloscope → ranged [IGNORES COVER]) apply
        // to its own weapons of the matching class only.
        foreach (var effect in part.Datasheet.SelfEffects)
        {
            if (!ClassMatches(effect.WeaponClass, ranged))
                continue;
            foreach (var ability in effect.WeaponAbilities)
                if (!result.Contains(ability, StringComparer.OrdinalIgnoreCase))
                    result.Add(ability);
        }

        return result;
    }

    /// <summary>
    /// Numeric buffs to apply to this part's weapon characteristics (Hit/A/S/D) of the given class, summed and
    /// applied by <see cref="StatMath"/> in the UI. Sourced from attached Leaders' conferrals and from
    /// detachment <see cref="DetachmentStatBuff"/>s.
    /// </summary>
    public IReadOnlyList<StatModifier> WeaponStatModifiers(BattleUnit unit, BattlePart part, bool ranged)
    {
        var result = new List<StatModifier>();

        foreach (var leader in unit.Parts.Where(p => p.IsLeader))
            foreach (var conferral in leader.Datasheet.LeaderConferrals)
                if (ConferralApplied(leader, conferral))
                    foreach (var mod in conferral.StatModifiers)
                        if (mod.IsWeaponStat && ClassMatches(mod.WeaponClass, ranged))
                            result.Add(mod);

        foreach (var detachment in Detachments)
            foreach (var buff in detachment.StatBuffs)
                if (buff.Modifier.IsWeaponStat && ClassMatches(buff.Modifier.WeaponClass, ranged) && BuffApplies(unit, part, buff))
                    result.Add(buff.Modifier);

        // Setup-assigned Enhancements buff the bearer's own weapons, or every model in the bearer's unit when
        // the enhancement says so (e.g. Gauntlet of Compression's +6" Range across the unit).
        foreach (var mod in EnhancementStatModifiers(unit, part))
            if (mod.IsWeaponStat && ClassMatches(mod.WeaponClass, ranged))
                result.Add(mod);

        return result;
    }

    /// <summary>
    /// Numeric buffs to apply to this part's unit statline (M/T/Sv/W/Ld/OC). Sourced from attached Leaders'
    /// conferrals and from detachment <see cref="DetachmentStatBuff"/>s.
    /// </summary>
    public IReadOnlyList<StatModifier> UnitStatModifiers(BattleUnit unit, BattlePart part)
    {
        var result = new List<StatModifier>();

        foreach (var leader in unit.Parts.Where(p => p.IsLeader))
            foreach (var conferral in leader.Datasheet.LeaderConferrals)
                if (ConferralApplied(leader, conferral))
                    foreach (var mod in conferral.StatModifiers)
                        if (!mod.IsWeaponStat)
                            result.Add(mod);

        foreach (var detachment in Detachments)
            foreach (var buff in detachment.StatBuffs)
                if (!buff.Modifier.IsWeaponStat && BuffApplies(unit, part, buff))
                    result.Add(buff.Modifier);

        // Setup-assigned Enhancements buff the bearer's own statline, or every model in the bearer's unit when
        // the enhancement is unit-wide.
        foreach (var mod in EnhancementStatModifiers(unit, part))
            if (!mod.IsWeaponStat)
                result.Add(mod);

        // A model's own permanent self-effects (e.g. Tomb Blades' Shieldvanes → Save 3+ / Move 8") rewrite
        // its own statline only — never an attached leader's or the rest of the group.
        foreach (var effect in part.Datasheet.SelfEffects)
            foreach (var mod in effect.StatModifiers)
                if (!mod.IsWeaponStat)
                    result.Add(mod);

        return result;
    }

    /// <summary>
    /// The stat modifiers from setup-assigned Enhancements that apply to <paramref name="part"/>: the part's
    /// own enhancement, plus any unit-wide (<see cref="Enhancement.AffectsWholeUnit"/>) enhancement carried by
    /// another model in the same combat group. Only enhancements the player ticked "Apply to unit" for count.
    /// </summary>
    private IEnumerable<StatModifier> EnhancementStatModifiers(BattleUnit unit, BattlePart part)
    {
        foreach (var member in unit.Parts)
        {
            if (member.Enhancement is not { } enh)
                continue;
            if (!Source.IsApplied(AbilityScheduleKeys.ForEnhancement(enh.Id)))
                continue;
            if (!enh.AffectsWholeUnit && !ReferenceEquals(member, part))
                continue;
            foreach (var mod in enh.StatModifiers)
                yield return mod;
        }
    }

    /// <summary>Unit-wide abilities granted to this group by attached Leaders (e.g. "Feel No Pain 5+").</summary>
    public IReadOnlyList<string> ConferredUnitAbilities(BattleUnit unit)
    {
        var result = new List<string>();
        foreach (var leader in unit.Parts.Where(p => p.IsLeader))
            foreach (var conferral in leader.Datasheet.LeaderConferrals)
                if (ConferralApplied(leader, conferral))
                    foreach (var ability in conferral.UnitAbilities)
                        if (!result.Contains(ability, StringComparer.OrdinalIgnoreCase))
                            result.Add(ability);
        return result;
    }

    /// <summary>
    /// True when the player ticked "Apply to unit" for the Leader ability that drives this conferral (keyed
    /// per the Leader's datasheet + the conferral's source ability). An empty conferral never applies.
    /// </summary>
    private bool ConferralApplied(BattlePart leader, ConferredEffect conferral)
    {
        if (conferral.IsEmpty || string.IsNullOrEmpty(conferral.SourceAbility))
            return false;
        return Source.IsApplied(AbilityScheduleKeys.ForUnitAbility(leader.Datasheet.Id, conferral.SourceAbility));
    }

    private static bool BuffApplies(BattleUnit unit, BattlePart part, DetachmentStatBuff buff)
    {
        if (buff.RequiresAttachedLeader && !unit.Parts.Any(p => p.IsLeader))
            return false;
        return buff.Scope == GrantScope.Unit
            ? unit.Parts.Any(p => MatchesAny(p, buff.Keywords))
            : MatchesAny(part, buff.Keywords);
    }

    /// <summary>The selectable weapon-ability choices a unit qualifies for (e.g. it contains a CRYPTEK model).</summary>
    public IReadOnlyList<WeaponAbilityChoice> WeaponChoicesFor(BattleUnit unit)
    {
        var result = new List<WeaponAbilityChoice>();
        foreach (var detachment in Detachments)
        {
            foreach (var choice in detachment.WeaponChoices)
            {
                if (unit.Parts.Any(p => ModelHasKeyword(p, choice.RequiresModelKeyword)))
                    result.Add(choice);
            }
        }
        return result;
    }

    /// <summary>
    /// Extra selectable shooting abilities granted by a unit member's setup-assigned Enhancement, on top of the
    /// detachment's own weapon-ability choice — e.g. Atomic Disintegrators adds [ANTI-MONSTER 5+] /
    /// [ANTI-VEHICLE 5+] to the bearer's CRYPTEK unit. De-duplicated, in bearer order.
    /// </summary>
    public IReadOnlyList<string> ExtraShootingOptions(BattleUnit unit)
    {
        var result = new List<string>();
        foreach (var part in unit.Parts)
            if (part.Enhancement is { } enh && Source.IsApplied(AbilityScheduleKeys.ForEnhancement(enh.Id)))
                foreach (var option in enh.ShootingAbilityOptions)
                    if (!result.Contains(option, StringComparer.OrdinalIgnoreCase))
                        result.Add(option);
        return result;
    }

    private static bool ModelHasKeyword(BattlePart part, string keyword) =>
        string.IsNullOrEmpty(keyword)
        || part.Datasheet.Keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase);

    private static bool MatchesAny(BattlePart part, IReadOnlyList<string> keywords) =>
        keywords.Count == 0
        || keywords.Any(k => part.Datasheet.Keywords.Contains(k, StringComparer.OrdinalIgnoreCase));

    // Like MatchesAny but an empty list matches NOTHING — used for excluded-keyword checks (e.g. TITANIC),
    // where "no exclusions authored" must mean "exclude nobody" rather than "exclude everybody".
    private static bool HasAnyKeyword(BattlePart part, IReadOnlyList<string> keywords) =>
        keywords.Count > 0
        && keywords.Any(k => part.Datasheet.Keywords.Contains(k, StringComparer.OrdinalIgnoreCase));

    private static bool ClassMatches(DetachmentWeaponClass weaponClass, bool ranged) =>
        weaponClass == DetachmentWeaponClass.Any
        || (ranged && weaponClass == DetachmentWeaponClass.Ranged)
        || (!ranged && weaponClass == DetachmentWeaponClass.Melee);

    private static bool ClassMatches(WeaponClass weaponClass, bool ranged) =>
        weaponClass == WeaponClass.Any
        || (ranged && weaponClass == WeaponClass.Ranged)
        || (!ranged && weaponClass == WeaponClass.Melee);
}

/// <summary>
/// A combat group on the table: a primary unit plus any Leaders attached to it. Tracks the merged model
/// count and total wound pool for the in-game trackers.
/// </summary>
public sealed class BattleUnit
{
    private readonly Roster _roster;

    internal BattleUnit(IReadOnlyList<BattlePart> parts, Roster roster)
    {
        Parts = parts;
        Primary = parts[0];
        _roster = roster;
    }

    /// <summary>Stable id for tracker state — the primary (bodyguard) roster-unit id.</summary>
    public string Id => Primary.Unit.Id;

    /// <summary>The primary unit; Leaders (if any) follow in <see cref="Parts"/>.</summary>
    public BattlePart Primary { get; }

    /// <summary>The primary part plus any attached Leader parts, primary first.</summary>
    public IReadOnlyList<BattlePart> Parts { get; }

    /// <summary>Display name: the primary unit, with attached Leaders appended ("Warriors + Overlord").</summary>
    public string Name => Parts.Count == 1
        ? Primary.Datasheet.Name
        : Primary.Datasheet.Name + " + " + string.Join(" + ", Parts.Skip(1).Select(p => p.Datasheet.Name));

    /// <summary>True when any part of this group is the army Warlord.</summary>
    public bool IsWarlord => Parts.Any(p => p.Unit.IsWarlord);

    /// <summary>Total models across the group.</summary>
    public int ModelCount => Parts.Sum(p => p.ModelCount);

    /// <summary>
    /// The group's invulnerable saves, each tagged unit-wide or model-only — so the card can distinguish a
    /// save the whole unit shares (incl. one a Leader confers, e.g. Master Chronomancer) from a single
    /// model's own save (e.g. a Character's). Best (lowest) unit-wide value first.
    /// </summary>
    public IReadOnlyList<SaveBadge> InvulnerableSaves => CollectSaves(PhaseClassifier.InvulnerableSaveScoped);

    /// <summary>The group's Feel No Pain saves, each tagged unit-wide (incl. a Leader's conferral) or model-only.</summary>
    public IReadOnlyList<SaveBadge> FeelNoPains => CollectSaves(PhaseClassifier.FeelNoPainScoped);

    /// <summary>The group's headline invulnerable save value (first badge), or null when none.</summary>
    public string? InvulnerableSave => InvulnerableSaves.Count > 0 ? InvulnerableSaves[0].Value : null;

    /// <summary>The group's headline Feel No Pain value (first badge), or null when none.</summary>
    public string? FeelNoPain => FeelNoPains.Count > 0 ? FeelNoPains[0].Value : null;

    // Builds the save badges across the group. A model's / unit's own always-on save rule (e.g. "This model
    // has a 4+ invulnerable save") is always shown — it is profile data. A save that a *conferring* ability
    // grants (e.g. a Leader's Master Chronomancer) only counts once the player has applied that ability in
    // setup. One unit-wide badge (best/lowest value), plus a model-only badge per part with its own value.
    private IReadOnlyList<SaveBadge> CollectSaves(Func<Ability, (string Value, SaveScope Scope)?> parse)
    {
        string? unitValue = null;
        var models = new List<SaveBadge>();
        foreach (var part in Parts)
        {
            foreach (var ability in part.Datasheet.Abilities)
            {
                if (parse(ability) is not { } save)
                    continue;
                // Own save rule → always-on; conferring ability → only when the player ticked "Apply to unit".
                if (!PhaseClassifier.IsOwnSaveRule(ability)
                    && !_roster.IsApplied(AbilityScheduleKeys.ForUnitAbility(part.Datasheet.Id, ability.Name)))
                    continue;
                if (save.Scope == SaveScope.Unit)
                {
                    if (unitValue is null || string.CompareOrdinal(save.Value, unitValue) < 0)
                        unitValue = save.Value;
                }
                else
                {
                    models.Add(new SaveBadge(save.Value, UnitWide: false, ModelName: part.Datasheet.Name));
                }
            }
        }

        var result = new List<SaveBadge>();
        if (unitValue is not null)
            result.Add(new SaveBadge(unitValue, UnitWide: true, ModelName: null));
        foreach (var badge in models)
            if (badge.Value != unitValue && !result.Any(r => r.Value == badge.Value && r.ModelName == badge.ModelName))
                result.Add(badge);
        return result;
    }

    /// <summary>Total trackable wound pool (sum of parts with a fixed Wounds value), or null when none are fixed.</summary>
    public int? MaxWounds
    {
        get
        {
            var total = 0;
            var any = false;
            foreach (var part in Parts)
            {
                if (part.MaxWounds is { } w)
                {
                    total += w;
                    any = true;
                }
            }
            return any ? total : null;
        }
    }

    /// <summary>
    /// Every ability across the group (primary unit first, then attached Leaders), de-duplicated by name so
    /// shared rules aren't repeated, followed by any setup-assigned Enhancements on the group's characters.
    /// Each entry carries its manual <see cref="AbilitySchedule"/> (windows + apply flag), the member it came
    /// from, whether it is an Enhancement, and — when it confers an effect — the summary that would be applied.
    /// </summary>
    public IReadOnlyList<BattleAbility> CombinedAbilities
    {
        get
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<BattleAbility>();
            foreach (var part in Parts)
            {
                foreach (var ability in part.Datasheet.Abilities)
                    if (!HiddenInPlay(ability) && !AbsorbedBySelfEffect(part.Datasheet, ability) && seen.Add(ability.Name))
                    {
                        var key = AbilityScheduleKeys.ForUnitAbility(part.Datasheet.Id, ability.Name);
                        var schedule = _roster.FindSchedule(key);
                        result.Add(new BattleAbility(ability, part.Datasheet.Name)
                        {
                            Key = key,
                            ConferredSummary = ConferredSummaryFor(part, ability.Name, ability),
                            Windows = schedule?.Windows ?? [],
                            ApplyToUnit = schedule?.ApplyToUnit ?? false,
                            ManualKeyword = schedule?.ManualKeyword,
                        });
                    }

                // A character's setup-assigned Enhancement is shown on its card as an ability: as the applied
                // effect ("Applied: …") when the player ticks "Apply to unit", otherwise as its rules text.
                if (part.Enhancement is { } enh && seen.Add(enh.Name))
                {
                    var key = AbilityScheduleKeys.ForEnhancement(enh.Id);
                    var schedule = _roster.FindSchedule(key);
                    var summary = enh.EffectSummary;
                    result.Add(new BattleAbility(new Ability { Name = enh.Name, Text = enh.Text }, part.Datasheet.Name)
                    {
                        IsEnhancement = true,
                        Key = key,
                        ConferredSummary = string.IsNullOrWhiteSpace(summary) ? null : summary,
                        Windows = schedule?.Windows ?? [],
                        ApplyToUnit = schedule?.ApplyToUnit ?? false,
                        ManualKeyword = schedule?.ManualKeyword,
                    });
                }
            }
            // Plain text abilities first, then abilities applied straight to the card ("Applied: …") — stable
            // so each group keeps its in-roster order.
            return result.OrderBy(a => a.AppliedSummary is null ? 0 : 1).ToList();
        }
    }

    /// <summary>
    /// The short summary of the effect an ability would apply when "Apply to unit" is ticked: a leader's
    /// conferral (e.g. "United In Destruction" → [LETHAL HITS]), or the save granted by a conditional ability
    /// that confers one (e.g. a Leader's Master Chronomancer → "Invulnerable 4+"). A model's own always-on
    /// save rule is hidden from the list, so it never reaches here. Null when the ability applies nothing.
    /// </summary>
    private static string? ConferredSummaryFor(BattlePart part, string abilityName, Ability ability)
    {
        if (part.IsLeader)
            foreach (var conferral in part.Datasheet.LeaderConferrals)
                if (!conferral.IsEmpty
                    && string.Equals(conferral.SourceAbility, abilityName, StringComparison.OrdinalIgnoreCase))
                    return conferral.Summary;

        // A conditional save-granting ability (not a plain own-save rule) can be applied as a save.
        if (!PhaseClassifier.IsOwnSaveRule(ability))
        {
            if (PhaseClassifier.InvulnerableSaveScoped(ability) is { } inv)
                return $"Invulnerable {inv.Value}";
            if (PhaseClassifier.FeelNoPainScoped(ability) is { } fnp)
                return $"Feel No Pain {fnp.Value}";
        }
        return null;
    }

    // Abilities not shown in the Play-Mode ability list: the setup-only "Leader" attach list, and a model's /
    // unit's own always-on save rule (e.g. "This model has a 4+ invulnerable save") — that is profile data
    // surfaced as a chip, not a schedulable ability. Conditional save abilities that *confer* a save (e.g. a
    // Leader's Master Chronomancer) are real abilities and stay in the list.
    private static bool HiddenInPlay(Ability ability) =>
        string.Equals(ability.Name, "Leader", StringComparison.OrdinalIgnoreCase)
        || PhaseClassifier.IsOwnSaveRule(ability);

    // An ability whose permanent self-effect is now reflected in the bearer's statline / weapon chips (parsed
    // into SelfEffects) is redundant as prose, so it is dropped from the listed abilities.
    private static bool AbsorbedBySelfEffect(Datasheet datasheet, Ability ability) =>
        datasheet.SelfEffects.Any(e =>
            string.Equals(e.SourceAbility, ability.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True when an ability is "usable now": its manual schedule has a window ticked for <paramref name="phase"/>
    /// in <paramref name="turn"/>. An ability whose conferred effect is applied to the unit
    /// (<see cref="BattleAbility.AppliedSummary"/>) is always-on and excluded, as is one ticked for <i>every</i>
    /// phase and turn (<see cref="BattleAbility.IsAlwaysAvailable"/>) — those are listed calmly rather than
    /// highlighted each phase. Scheduling is entirely manual: an unconfigured ability is never active.
    /// </summary>
    public static bool IsAbilityActiveInPhase(BattleAbility ability, BattlePhase phase, BattleTurn turn)
    {
        if (ability.AppliedSummary is not null || ability.IsAlwaysAvailable)
            return false;
        return ability.Windows.Any(w => w.Phase == phase && w.Turn == turn);
    }

    /// <summary>How many of this group's text abilities are usable in <paramref name="phase"/> during
    /// <paramref name="turn"/> (drives the phase markers).</summary>
    public int ActiveAbilityCount(BattlePhase phase, BattleTurn turn) =>
        CombinedAbilities.Count(a => IsAbilityActiveInPhase(a, phase, turn));

    /// <summary>
    /// When an ability is from an attached Leader and confers an effect on the led unit (e.g. "United In
    /// Destruction" → [LETHAL HITS]), returns a short summary for the "Applied: …" note; otherwise null, so
    /// the ability is shown as ordinary (collapsible) text.
    /// </summary>
    public string? AppliedSummaryFor(string abilityName)
    {
        foreach (var part in Parts.Where(p => p.IsLeader))
            foreach (var conferral in part.Datasheet.LeaderConferrals)
                if (!conferral.IsEmpty
                    && string.Equals(conferral.SourceAbility, abilityName, StringComparison.OrdinalIgnoreCase))
                    return conferral.Summary;
        return null;
    }
}

/// <summary>An ability shown on a battle card, tagged with the member datasheet it belongs to.</summary>
public sealed record BattleAbility(Ability Ability, string Source)
{
    /// <summary>True when this entry is a setup-assigned Enhancement rather than a printed datasheet ability.</summary>
    public bool IsEnhancement { get; init; }

    /// <summary>The manual-schedule key (<see cref="AbilityScheduleKeys"/>) used to configure this in setup.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>The phase + turn windows the player ticked for this ability. Empty = never "usable now".</summary>
    public IReadOnlyList<AbilityWindow> Windows { get; init; } = [];

    /// <summary>Whether the player ticked "Apply to unit": when true the conferred effect is applied and prose hidden.</summary>
    public bool ApplyToUnit { get; init; }

    /// <summary>
    /// A player-authored short keyword chip (≤3 words, e.g. "Stealth") for an ability the engine can't summarise.
    /// When set, Play Mode shows it as a passive keyword chip (alongside Inv/FNP), always visible regardless of
    /// the phase/turn windows, with the original ability text available on tap. Null = none.
    /// </summary>
    public string? ManualKeyword { get; init; }

    /// <summary>True when the player tagged this ability with a manual keyword chip.</summary>
    public bool HasManualKeyword => !string.IsNullOrWhiteSpace(ManualKeyword);

    /// <summary>
    /// The short summary of this ability's conferred effect (e.g. "[LETHAL HITS]"), when it has one. Independent
    /// of whether the player chose to apply it — use <see cref="AppliedSummary"/> for the displayed value.
    /// </summary>
    public string? ConferredSummary { get; init; }

    /// <summary>True when this ability has a conferable effect that the player <i>could</i> apply to the unit.</summary>
    public bool CanApplyToUnit => ConferredSummary is not null;

    /// <summary>
    /// True when the player ticked <b>every</b> phase + turn window (all 10 cells) for a text ability — it is
    /// usable in any phase of either turn. Such abilities are listed in a calm "always available" section
    /// rather than highlighted as "usable now" in each phase. Applied abilities are excluded (they show as the
    /// applied effect instead).
    /// </summary>
    public bool IsAlwaysAvailable =>
        AppliedSummary is null
        && BattlePhases.Ordered.All(p =>
            Windows.Any(w => w.Phase == p && w.Turn == BattleTurn.Player)
            && Windows.Any(w => w.Phase == p && w.Turn == BattleTurn.Opponent));

    /// <summary>
    /// Non-null only when the player ticked "Apply to unit" for an ability that confers an effect: the short
    /// "Applied: …" summary to show instead of prose. Such applied abilities are always-on, so they are
    /// excluded from the per-phase "usable now" markers.
    /// </summary>
    public string? AppliedSummary => ApplyToUnit ? ConferredSummary : null;
}

/// <summary>
/// An invulnerable / Feel No Pain save shown on a battle card. <see cref="UnitWide"/> means every model in
/// the unit has it (incl. a save conferred by an attached Leader); otherwise it is a single model's save and
/// <see cref="ModelName"/> names that model.
/// </summary>
public sealed record SaveBadge(string Value, bool UnitWide, string? ModelName);

/// <summary>One datasheet's contribution to a <see cref="BattleUnit"/> (the unit itself, or an attached Leader).</summary>
public sealed class BattlePart
{
    internal BattlePart(RosterUnit unit, Datasheet datasheet, bool isLeader)
    {
        Unit = unit;
        Datasheet = datasheet;
        IsLeader = isLeader;
        // Only the weapons actually selected in setup are in play (always-on weapons are always included).
        Weapons = WargearResolver.SelectedWeapons(datasheet, unit);
        RangedWeapons = Weapons.Where(w => PhaseClassifier.PhaseForWeapon(w) == BattlePhase.Shooting).ToList();
        MeleeWeapons = Weapons.Where(w => PhaseClassifier.PhaseForWeapon(w) == BattlePhase.Fight).ToList();
    }

    /// <summary>The underlying roster unit (size, warlord flag, wargear, …).</summary>
    public RosterUnit Unit { get; }

    /// <summary>The resolved catalogue datasheet.</summary>
    public Datasheet Datasheet { get; }

    /// <summary>True when this part is a Leader attached to the group's primary unit.</summary>
    public bool IsLeader { get; internal set; }

    /// <summary>
    /// The Enhancement assigned to this part's character in setup (resolved from the selected detachment), or
    /// null. Drives Play Mode's enhancement ability / live stat change on the bearer's card.
    /// </summary>
    public Enhancement? Enhancement { get; internal set; }

    /// <summary>Models in this part.</summary>
    public int ModelCount => Unit.ModelCount;

    /// <summary>The full-health statline (first profile), or null when the datasheet has none.</summary>
    public StatProfile? Profile => Datasheet.StatProfiles.FirstOrDefault();

    /// <summary>Wounds per model, or null when variable.</summary>
    public int? WoundsPerModel => BattleRoster.ParseWounds(Profile?.Wounds);

    /// <summary>This part's trackable wound pool (per-model wounds × models), or null when variable.</summary>
    public int? MaxWounds => WoundsPerModel is { } w ? w * Math.Max(1, ModelCount) : null;

    /// <summary>
    /// True when this part is a single multi-wound model (e.g. a Character): in Play Mode its <b>wounds</b>
    /// are tracked rather than a 1/1 model count — even when it is attached as a Leader.
    /// </summary>
    public bool TracksWounds => ModelCount == 1 && WoundsPerModel is > 1;

    /// <summary>
    /// The trackable maximum for the Play-Mode counter: this part's wounds when it is a single multi-wound
    /// model, otherwise its model count.
    /// </summary>
    public int TrackMax => TracksWounds ? MaxWounds!.Value : Math.Max(1, ModelCount);

    /// <summary>The weapons actually in play for this part (always-on + wargear selected in setup), datasheet order.</summary>
    public IReadOnlyList<WeaponProfile> Weapons { get; }

    /// <summary>Ranged weapons in play (used in the Shooting phase).</summary>
    public IReadOnlyList<WeaponProfile> RangedWeapons { get; }

    /// <summary>Melee weapons in play (used in the Fight phase).</summary>
    public IReadOnlyList<WeaponProfile> MeleeWeapons { get; }
}
