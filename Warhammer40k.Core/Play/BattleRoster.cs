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
    private BattleRoster(IReadOnlyList<BattleUnit> units, IReadOnlyList<Detachment> detachments)
    {
        Units = units;
        Detachments = detachments;
    }

    /// <summary>The combat groups, in roster order (a group is a unit plus any Leaders attached to it).</summary>
    public IReadOnlyList<BattleUnit> Units { get; }

    /// <summary>The detachments selected for this roster — drive the smart weapon-ability effects in Play Mode.</summary>
    public IReadOnlyList<Detachment> Detachments { get; }

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
            units.Add(new BattleUnit(members));
        }

        return new BattleRoster(units, detachments);
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
                    ? unit.Parts.Any(p => MatchesAny(p, grant.Keywords))
                    : MatchesAny(part, grant.Keywords);
                if (!applies)
                    continue;

                foreach (var ability in grant.Abilities)
                {
                    if (!result.Contains(ability, StringComparer.OrdinalIgnoreCase))
                        result.Add(ability);
                }
            }
        }

        // Leader-conferred weapon abilities apply to every model in the led unit (incl. the Leader itself).
        foreach (var leader in unit.Parts.Where(p => p.IsLeader))
        {
            foreach (var conferral in leader.Datasheet.LeaderConferrals)
            {
                if (!ClassMatches(conferral.WeaponClass, ranged))
                    continue;
                foreach (var ability in conferral.WeaponAbilities)
                {
                    if (!result.Contains(ability, StringComparer.OrdinalIgnoreCase))
                        result.Add(ability);
                }
            }
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
                foreach (var mod in conferral.StatModifiers)
                    if (mod.IsWeaponStat && ClassMatches(mod.WeaponClass, ranged))
                        result.Add(mod);

        foreach (var detachment in Detachments)
            foreach (var buff in detachment.StatBuffs)
                if (buff.Modifier.IsWeaponStat && ClassMatches(buff.Modifier.WeaponClass, ranged) && BuffApplies(unit, part, buff))
                    result.Add(buff.Modifier);

        // The bearer's setup-assigned Enhancement buffs its own weapons.
        if (part.Enhancement is { } enh)
            foreach (var mod in enh.StatModifiers)
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
                foreach (var mod in conferral.StatModifiers)
                    if (!mod.IsWeaponStat)
                        result.Add(mod);

        foreach (var detachment in Detachments)
            foreach (var buff in detachment.StatBuffs)
                if (!buff.Modifier.IsWeaponStat && BuffApplies(unit, part, buff))
                    result.Add(buff.Modifier);

        // The bearer's setup-assigned Enhancement buffs its own statline.
        if (part.Enhancement is { } enh)
            foreach (var mod in enh.StatModifiers)
                if (!mod.IsWeaponStat)
                    result.Add(mod);

        return result;
    }

    /// <summary>Unit-wide abilities granted to this group by attached Leaders (e.g. "Feel No Pain 5+").</summary>
    public IReadOnlyList<string> ConferredUnitAbilities(BattleUnit unit)
    {
        var result = new List<string>();
        foreach (var leader in unit.Parts.Where(p => p.IsLeader))
            foreach (var conferral in leader.Datasheet.LeaderConferrals)
                foreach (var ability in conferral.UnitAbilities)
                    if (!result.Contains(ability, StringComparer.OrdinalIgnoreCase))
                        result.Add(ability);
        return result;
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

    private static bool ModelHasKeyword(BattlePart part, string keyword) =>
        string.IsNullOrEmpty(keyword)
        || part.Datasheet.Keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase);

    private static bool MatchesAny(BattlePart part, IReadOnlyList<string> keywords) =>
        keywords.Count == 0
        || keywords.Any(k => part.Datasheet.Keywords.Contains(k, StringComparer.OrdinalIgnoreCase));

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
    internal BattleUnit(IReadOnlyList<BattlePart> parts)
    {
        Parts = parts;
        Primary = parts[0];
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

    /// <summary>The group's invulnerable save (first found across parts), or null when none.</summary>
    public string? InvulnerableSave =>
        Parts.Select(p => PhaseClassifier.InvulnerableSave(p.Datasheet.Abilities)).FirstOrDefault(s => s is not null);

    /// <summary>The group's Feel No Pain value (first found across parts), or null when none.</summary>
    public string? FeelNoPain =>
        Parts.Select(p => PhaseClassifier.FeelNoPain(p.Datasheet.Abilities)).FirstOrDefault(s => s is not null);

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

    /// <summary>True when any part has content (weapons or abilities) to show in the given phase.</summary>
    public bool HasContentIn(BattlePhase phase) => Parts.Any(p => p.HasContentIn(phase));

    /// <summary>
    /// Every ability across the group (primary unit first, then attached Leaders), de-duplicated by name so
    /// shared rules aren't repeated, followed by any setup-assigned Enhancements on the group's characters.
    /// Each entry records which member it came from, whether it is an Enhancement, and — for leader conferrals
    /// or stat-changing enhancements — its applied-effect summary.
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
                    if (seen.Add(ability.Name))
                        result.Add(new BattleAbility(ability, part.Datasheet.Name)
                        {
                            AppliedSummary = ConferredSummaryFor(part, ability.Name),
                        });

                // A character's setup-assigned Enhancement is shown on its card as an ability: as a live stat
                // change ("Applied: …") when it buffs stats, otherwise as its rules text.
                if (part.Enhancement is { } enh && seen.Add(enh.Name))
                {
                    var summary = enh.EffectSummary;
                    result.Add(new BattleAbility(new Ability { Name = enh.Name, Text = enh.Text }, part.Datasheet.Name)
                    {
                        IsEnhancement = true,
                        AppliedSummary = string.IsNullOrWhiteSpace(summary) ? null : summary,
                    });
                }
            }
            return result;
        }
    }

    /// <summary>
    /// The "Applied: …" summary for a leader's conferral ability (e.g. "United In Destruction" → [LETHAL HITS]),
    /// or null when the ability is ordinary (collapsible) text.
    /// </summary>
    private static string? ConferredSummaryFor(BattlePart part, string abilityName)
    {
        if (!part.IsLeader)
            return null;
        foreach (var conferral in part.Datasheet.LeaderConferrals)
            if (!conferral.IsEmpty
                && string.Equals(conferral.SourceAbility, abilityName, StringComparison.OrdinalIgnoreCase))
                return conferral.Summary;
        return null;
    }

    /// <summary>
    /// True when an ability is a <i>text</i> ability whose rules text is relevant to <paramref name="phase"/>,
    /// so Play Mode should highlight it as "usable now". Abilities whose effect is applied straight to the card
    /// (<see cref="BattleAbility.AppliedSummary"/> — leader conferrals and stat-changing enhancements) are
    /// always-on and are never phase-marked: stat abilities simply change the stats while they apply.
    /// </summary>
    public static bool IsAbilityActiveInPhase(BattleAbility ability, BattlePhase phase) =>
        ability.AppliedSummary is null && PhaseClassifier.Classify(ability.Ability).Contains(phase);

    /// <summary>How many of this group's text abilities are usable in <paramref name="phase"/> (drives the phase markers).</summary>
    public int ActiveAbilityCount(BattlePhase phase) =>
        CombinedAbilities.Count(a => IsAbilityActiveInPhase(a, phase));

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

    /// <summary>
    /// Non-null when this ability's effect is applied straight to the card (a leader conferral or an
    /// enhancement's stat buff): the short "Applied: …" summary to show instead of, or alongside, prose.
    /// Such "stat" abilities are always-on and are excluded from the per-phase "usable now" markers.
    /// </summary>
    public string? AppliedSummary { get; init; }
}

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

    /// <summary>
    /// Abilities relevant to a phase. Passive/always-on abilities are surfaced under
    /// <see cref="BattlePhase.Command"/> so they are reviewed at the top of the round.
    /// </summary>
    public IReadOnlyList<Ability> AbilitiesIn(BattlePhase phase) =>
        Datasheet.Abilities
            .Where(a =>
            {
                var phases = PhaseClassifier.Classify(a);
                return phases.Contains(phase) || (phase == BattlePhase.Command && phases.Count == 0);
            })
            .ToList();

    /// <summary>True when this part has weapons or abilities to display in the given phase.</summary>
    public bool HasContentIn(BattlePhase phase)
    {
        if (phase == BattlePhase.Shooting && RangedWeapons.Count > 0)
            return true;
        if (phase == BattlePhase.Fight && MeleeWeapons.Count > 0)
            return true;
        return AbilitiesIn(phase).Count > 0;
    }
}
