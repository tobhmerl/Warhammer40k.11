using Warhammer40k.Core.Catalogue;

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

    /// <summary>Enhancements selectable from this detachment. Empty where 11th-edition points aren't authored yet.</summary>
    public List<Enhancement> Enhancements { get; set; } = [];

    /// <summary>The detachment's stratagems (reference only — they affect play, not roster validation).</summary>
    public List<Stratagem> Stratagems { get; set; } = [];

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
    /// True when <paramref name="datasheet"/> satisfies this enhancement's keyword constraints. This is only
    /// the per-enhancement constraint; whether the unit can take Enhancements at all
    /// (<see cref="Catalogue.Datasheet.CanTakeEnhancements"/>) is checked separately by rule R6.
    /// </summary>
    public bool IsAvailableTo(Datasheet datasheet) => Eligibility.IsSatisfiedBy(datasheet);
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

/// <summary>A detachment Stratagem (§11). Reference data shown in the UI; not part of roster validation.</summary>
public sealed class Stratagem
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Stratagem category, e.g. "Battle Tactic", "Epic Deed".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Command Point cost.</summary>
    public int CpCost { get; set; }

    /// <summary>Rules text.</summary>
    public string Text { get; set; } = string.Empty;
}
