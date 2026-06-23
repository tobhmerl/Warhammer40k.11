using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;

namespace Warhammer40k.Core.Rosters;

/// <summary>
/// One of the seven Necron detachments (§2). A roster selects exactly one (rule R2). Carries the
/// Enhancements that may be assigned from it (rule R6), the detachment's Stratagems (reference), and a flag
/// for the Pantheon of Woe special case (rule R10).
/// </summary>
/// <remarks>
/// The eligibility + stratagem <b>machinery</b> is finalized (AB9); the remaining gap is <i>content</i>: the
/// 11th-edition enhancement points/eligibility and stratagem text for some detachments aren't available yet.
/// Where an <see cref="Enhancement"/> list is empty (Hand of the Dynasty, Skyshroud Spearhead, The Phaeron's
/// Armoury) R6 stays permissive; populate <see cref="DetachmentCatalogue"/> to switch it on.
/// </remarks>
public sealed class Detachment
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Detachment Points spent to field this detachment (11th edition). 0 until costed.</summary>
    public int DetachmentPoints { get; set; }

    /// <summary>Whether this detachment is selectable in setup. Only detachments with authored rules are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Exclusivity tags (e.g. "Dynasty"). A roster may take at most one detachment per tag — a "Unique: X"
    /// detachment cannot be taken with another X detachment (enforced by rule R2).
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Enhancements selectable from this detachment. Empty where 11th-edition points aren't authored yet.</summary>
    public List<Enhancement> Enhancements { get; set; } = [];

    /// <summary>The detachment's stratagems (reference only — they affect play, not roster validation).</summary>
    public List<Stratagem> Stratagems { get; set; } = [];

    /// <summary>The detachment rule(s), shown as reference text in Play Mode.</summary>
    public List<DetachmentRule> Rules { get; set; } = [];

    /// <summary>
    /// Passive weapon-ability grants applied in Play Mode (e.g. CRYPTEK <b>models'</b> ranged weapons gain
    /// the [ASSAULT] ability). Targets individual models by keyword, not the whole unit.
    /// </summary>
    public List<WeaponAbilityGrant> WeaponGrants { get; set; } = [];

    /// <summary>
    /// Selectable weapon abilities offered in Play Mode (e.g. a unit containing a CRYPTEK model picks one when
    /// it shoots; it applies to that unit's ranged weapons until the end of the phase).
    /// </summary>
    public List<WeaponAbilityChoice> WeaponChoices { get; set; } = [];

    /// <summary>
    /// Passive numeric stat buffs applied in Play Mode (e.g. "while a NECRONS CHARACTER leads this unit, add 1
    /// to the Hit roll" → the unit's weapons show an improved BS/WS). Empty for the built-in detachments until
    /// their 11th-edition rules are authored — the engine applies them automatically once present.
    /// </summary>
    public List<DetachmentStatBuff> StatBuffs { get; set; } = [];

    /// <summary>True only for Pantheon of Woe: every Necrons Monster auto-takes its Necrodermal Binding (rule R10).</summary>
    public bool AppliesPantheonBindings { get; set; }

    /// <summary>Finds an enhancement on this detachment by id.</summary>
    public Enhancement? FindEnhancement(string id) =>
        Enhancements.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A detachment Enhancement (§6/§8). Points feed rule R1; eligibility (§10/§11) is enforced by rule R6.</summary>
public sealed class Enhancement
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Points cost (10th-MFM where known, §8). Expose as editable; fill 11th-edition values later.</summary>
    public int Points { get; set; }

    /// <summary>Which models may take this Enhancement (§10/§11). Empty constraints = any eligible Character.</summary>
    public EnhancementEligibility Eligibility { get; set; } = new();

    /// <summary>
    /// Whether this is a classic Character Enhancement or an 11th-edition unit <b>Upgrade</b> (assigned to a
    /// whole non-Character unit, e.g. NECRON WARRIORS). Drives rule R6's target check and the configurator.
    /// </summary>
    public EnhancementScope Scope { get; set; } = EnhancementScope.Character;

    /// <summary>
    /// The rules text shown on the bearer's card in Play Mode (treated as an ability). Empty until authored —
    /// when present and it names a phase ("…in your Command phase…"), Play Mode highlights it in that phase.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Numeric characteristic buffs this Enhancement confers on its bearer (e.g. <c>+1</c> Wounds). When set,
    /// Play Mode applies them to the bearer's live stat line and shows the change as an "Applied" note rather
    /// than prose, mirroring leader conferrals / detachment buffs.
    /// </summary>
    public List<StatModifier> StatModifiers { get; set; } = [];

    /// <summary>
    /// When true, <see cref="StatModifiers"/> apply to <b>every model in the bearer's unit</b> (the whole
    /// combat group in Play Mode), not just the bearer — e.g. Gauntlet of Compression's <c>+6"</c> Range.
    /// </summary>
    public bool AffectsWholeUnit { get; set; }

    /// <summary>A compact one-line summary of <see cref="StatModifiers"/> for the "Applied: …" note; empty when none.</summary>
    public string EffectSummary => string.Join("; ", StatModifiers.Select(m => m.Describe()));

    /// <summary>
    /// True when <paramref name="datasheet"/> satisfies this enhancement's keyword constraints. This is only
    /// the per-enhancement constraint; whether the unit can take Enhancements at all
    /// (<see cref="Catalogue.Datasheet.CanTakeEnhancements"/>) is checked separately by rule R6.
    /// </summary>
    public bool IsAvailableTo(Datasheet datasheet) => Eligibility.IsSatisfiedBy(datasheet);
}

/// <summary>Whether an <see cref="Enhancement"/> is assigned to a Character, or to a whole unit (an Upgrade).</summary>
public enum EnhancementScope
{
    /// <summary>A classic Enhancement assigned to a Character model (the default).</summary>
    Character = 0,

    /// <summary>An 11th-edition Upgrade assigned to a whole non-Character unit (e.g. NECRON WARRIORS).</summary>
    Unit = 1,
}

/// <summary>
/// Structured per-enhancement eligibility (§10/§11): the target model must carry every keyword in
/// <see cref="RequiredKeywords"/> and none in <see cref="ExcludedKeywords"/>. Empty lists impose no constraint.
/// </summary>
public sealed class EnhancementEligibility
{
    /// <summary>Keywords the model must have (all of them), e.g. "Cryptek".</summary>
    public List<string> RequiredKeywords { get; set; } = [];

    /// <summary>Keywords the model must not have (none of them).</summary>
    public List<string> ExcludedKeywords { get; set; } = [];

    /// <summary>True when no constraints are set.</summary>
    public bool IsUnconstrained => RequiredKeywords.Count == 0 && ExcludedKeywords.Count == 0;

    /// <summary>Evaluates the keyword constraints against a datasheet's keywords.</summary>
    public bool IsSatisfiedBy(Datasheet datasheet)
    {
        foreach (var required in RequiredKeywords)
        {
            if (!datasheet.Keywords.Contains(required, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        foreach (var excluded in ExcludedKeywords)
        {
            if (datasheet.Keywords.Contains(excluded, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}

/// <summary>
/// A detachment Stratagem (§11) shown contextually in Play Mode <b>alongside</b> the Core Stratagems. Like
/// <see cref="Play.CoreStratagem"/>, its phase(s) and "Used in" turn drive the "need to know now" filter.
/// </summary>
public sealed class Stratagem
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Stratagem category, e.g. "Battle Tactic", "Wargear", "Strategic Ploy".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Command Point cost.</summary>
    public int CpCost { get; set; }

    /// <summary>Whose turn this may be used in (the coloured "Used in" marker).</summary>
    public StratagemTurn Turn { get; set; } = StratagemTurn.Either;

    /// <summary>The phase(s) this stratagem is used in. An empty list means it applies in any phase.</summary>
    public List<BattlePhase> Phases { get; set; } = [];

    /// <summary>The "WHEN" clause.</summary>
    public string When { get; set; } = string.Empty;

    /// <summary>The "TARGET" clause.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>The "EFFECT" clause.</summary>
    public string Effect { get; set; } = string.Empty;

    /// <summary>True when this stratagem is relevant in the given phase.</summary>
    public bool AppliesInPhase(BattlePhase phase) => Phases.Count == 0 || Phases.Contains(phase);

    /// <summary>True when this stratagem can be used during the given turn.</summary>
    public bool AppliesInTurn(BattleTurn turn) => Turn switch
    {
        StratagemTurn.Either => true,
        StratagemTurn.Your => turn == BattleTurn.Player,
        StratagemTurn.Opponent => turn == BattleTurn.Opponent,
        _ => false,
    };
}

/// <summary>Which of a model's/unit's weapons a detachment effect targets.</summary>
public enum DetachmentWeaponClass
{
    Any = 0,
    Ranged = 1,
    Melee = 2,
}

/// <summary>A detachment rule shown as reference text in Play Mode.</summary>
public sealed class DetachmentRule
{
    public string Name { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

/// <summary>Whether a weapon-ability grant targets the matching <b>model</b>, or the whole <b>unit</b>.</summary>
public enum GrantScope
{
    /// <summary>Only models that carry one of the keywords gain the abilities (e.g. CRYPTEK models).</summary>
    Model = 0,

    /// <summary>Every model in a unit that contains a keyword match gains the abilities (e.g. NECRON WARRIORS units).</summary>
    Unit = 1,
}

/// <summary>
/// A passive detachment buff: grants the listed weapon <see cref="Abilities"/> (to weapons of the given
/// <see cref="WeaponClass"/>) to models matching one of <see cref="Keywords"/>. <see cref="Scope"/> decides
/// whether only the matching model benefits (e.g. CRYPTEK models) or every model in a unit that contains a
/// match (e.g. NECRON WARRIORS units — so an attached Leader benefits too).
/// </summary>
public sealed class WeaponAbilityGrant
{
    /// <summary>Trigger keywords (any one matches), e.g. ["Cryptek"] or ["Immortals", "Necron Warriors"]. Empty = every model.</summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>Model-scoped (only the matching model) or unit-scoped (every model in a matching unit).</summary>
    public GrantScope Scope { get; set; } = GrantScope.Model;

    /// <summary>Which weapons receive the abilities.</summary>
    public DetachmentWeaponClass WeaponClass { get; set; } = DetachmentWeaponClass.Ranged;

    /// <summary>The weapon abilities granted, in catalogue spelling (e.g. "Assault").</summary>
    public List<string> Abilities { get; set; } = [];
}

/// <summary>
/// A selectable detachment buff: a unit that contains a model with <see cref="RequiresModelKeyword"/> may pick
/// one of <see cref="Options"/>; it applies to the unit's weapons of <see cref="WeaponClass"/> for the phase.
/// </summary>
public sealed class WeaponAbilityChoice
{
    /// <summary>Display label for the chooser, e.g. "Technosorcerous Augmentations".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A unit qualifies when it contains a model with this keyword, e.g. "Cryptek".</summary>
    public string RequiresModelKeyword { get; set; } = string.Empty;

    /// <summary>Which of the unit's weapons the chosen ability applies to.</summary>
    public DetachmentWeaponClass WeaponClass { get; set; } = DetachmentWeaponClass.Ranged;

    /// <summary>The selectable abilities, in catalogue spelling.</summary>
    public List<string> Options { get; set; } = [];
}

/// <summary>
/// A passive detachment numeric buff applied in Play Mode: applies <see cref="Modifier"/> to models matching
/// one of <see cref="Keywords"/> (or every model when empty). <see cref="Scope"/> decides whether only the
/// matching model benefits or every model in a unit that contains a match. Set
/// <see cref="RequiresAttachedLeader"/> for buffs that only apply while a Leader is attached to the unit
/// (e.g. "while a NECRONS CHARACTER model is leading this unit, add 1 to the Hit roll").
/// </summary>
public sealed class DetachmentStatBuff
{
    /// <summary>Trigger keywords (any one matches); empty = applies to every unit.</summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>Model-scoped (only the matching model) or unit-scoped (every model in a matching unit).</summary>
    public GrantScope Scope { get; set; } = GrantScope.Unit;

    /// <summary>When true, applies only to a unit that currently has a Leader attached.</summary>
    public bool RequiresAttachedLeader { get; set; }

    /// <summary>The characteristic change to apply (target, delta, and weapon class for weapon stats).</summary>
    public StatModifier Modifier { get; set; } = new();
}
