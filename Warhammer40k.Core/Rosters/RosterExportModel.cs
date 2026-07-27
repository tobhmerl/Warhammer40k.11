using System.Text.Json.Serialization;

namespace Warhammer40k.Core.Rosters;

/// <summary>Root of the JSON roster export produced by <see cref="RosterExporter"/>.</summary>
/// <remarks>
/// The shapes below are a presentation contract, not a storage format: names are spelled out and values are
/// pre-resolved (buffed statlines, granted weapon abilities) so a reader needs no rules knowledge to use them.
/// Empty collections are emitted as <c>null</c> and dropped, keeping the document free of meaningless noise.
/// </remarks>
public sealed class RosterExport
{
    [JsonPropertyName("schema")] public string Schema { get; init; } = "tombworld.roster-export/1";
    [JsonPropertyName("note")] public string Note { get; init; } =
        "All conferrals (leader abilities, enhancements, detachment rules) are shown as active. "
        + "Statlines and weapon profiles are the buffed values; 'baseStatline' holds the printed datasheet values.";
    [JsonPropertyName("roster")] public required RosterHeaderExport Roster { get; init; }
    [JsonPropertyName("detachments")] public required List<DetachmentExport> Detachments { get; init; }
    [JsonPropertyName("armyRules")] public required List<ArmyRuleExport> ArmyRules { get; init; }
    [JsonPropertyName("coreStratagems")] public required List<StratagemExport> CoreStratagems { get; init; }
    [JsonPropertyName("units")] public required List<UnitExport> Units { get; init; }
}

public sealed class RosterHeaderExport
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("faction")] public required string Faction { get; init; }
    [JsonPropertyName("pointsLimit")] public int PointsLimit { get; init; }
    [JsonPropertyName("totalPoints")] public int TotalPoints { get; init; }
    [JsonPropertyName("detachmentPointsSpent")] public int DetachmentPointsSpent { get; init; }
    [JsonPropertyName("detachmentPointsBudget")] public int DetachmentPointsBudget { get; init; }
}

public sealed class DetachmentExport
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("detachmentPoints")] public int DetachmentPoints { get; init; }
    [JsonPropertyName("disposition")] public string? Disposition { get; init; }
    [JsonPropertyName("uniqueTags")] public IReadOnlyList<string>? UniqueTags { get; init; }
    [JsonPropertyName("rules")] public required List<NamedTextExport> Rules { get; init; }
    [JsonPropertyName("enhancements")] public required List<EnhancementExport> Enhancements { get; init; }
    [JsonPropertyName("stratagems")] public required List<StratagemExport> Stratagems { get; init; }
}

public sealed class NamedTextExport
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("text")] public required string Text { get; init; }
}

public sealed class ArmyRuleExport
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("text")] public required string Text { get; init; }
    [JsonPropertyName("example")] public string? Example { get; init; }
}

public sealed class EnhancementExport
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("points")] public int Points { get; init; }
    [JsonPropertyName("text")] public required string Text { get; init; }
    [JsonPropertyName("scope")] public string? Scope { get; init; }
}

public sealed class StratagemExport
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("commandPoints")] public int CommandPoints { get; init; }
    [JsonPropertyName("usedIn")] public required string UsedIn { get; init; }
    [JsonPropertyName("phases")] public List<string>? Phases { get; init; }
    [JsonPropertyName("requiresKeywords")] public IReadOnlyList<string>? RequiresKeywords { get; init; }
    [JsonPropertyName("when")] public required string When { get; init; }
    [JsonPropertyName("target")] public required string Target { get; init; }
    [JsonPropertyName("effect")] public required string Effect { get; init; }
    [JsonPropertyName("restrictions")] public string? Restrictions { get; init; }
}

public sealed class UnitExport
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("points")] public int Points { get; init; }
    [JsonPropertyName("isWarlord")] public bool? IsWarlord { get; init; }
    [JsonPropertyName("isAttachedUnit")] public bool? IsAttachedUnit { get; init; }
    [JsonPropertyName("totalModels")] public int TotalModels { get; init; }
    [JsonPropertyName("keywords")] public required List<string> Keywords { get; init; }
    [JsonPropertyName("invulnerableSaves")] public required List<SaveExport> InvulnerableSaves { get; init; }
    [JsonPropertyName("feelNoPains")] public required List<SaveExport> FeelNoPains { get; init; }
    [JsonPropertyName("conferredUnitAbilities")] public List<string>? ConferredUnitAbilities { get; init; }
    [JsonPropertyName("abilities")] public required List<AbilityExport> Abilities { get; init; }
    [JsonPropertyName("coreAbilities")] public List<string>? CoreAbilities { get; init; }
    [JsonPropertyName("models")] public required List<ModelGroupExport> Models { get; init; }
    [JsonPropertyName("appliedModifiers")] public List<string>? AppliedModifiers { get; init; }
}

/// <summary>An invulnerable save or Feel No Pain, and whether it covers the whole unit or one model.</summary>
public sealed class SaveExport
{
    [JsonPropertyName("value")] public required string Value { get; init; }
    [JsonPropertyName("appliesTo")] public required string AppliesTo { get; init; }
}

public sealed class AbilityExport
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("text")] public required string Text { get; init; }
    [JsonPropertyName("from")] public string? From { get; init; }
    [JsonPropertyName("isEnhancement")] public bool? IsEnhancement { get; init; }
    [JsonPropertyName("applies")] public string? Applies { get; init; }
}

/// <summary>One datasheet's worth of models inside a unit — an attached unit has several of these.</summary>
public sealed class ModelGroupExport
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("isLeader")] public bool? IsLeader { get; init; }
    [JsonPropertyName("models")] public int Models { get; init; }
    [JsonPropertyName("woundsPerModel")] public int WoundsPerModel { get; init; }
    [JsonPropertyName("enhancement")] public EnhancementExport? Enhancement { get; init; }
    [JsonPropertyName("statline")] public required StatlineExport Statline { get; init; }
    [JsonPropertyName("baseStatline")] public StatlineExport? BaseStatline { get; init; }
    [JsonPropertyName("weapons")] public required List<WeaponExport> Weapons { get; init; }
}

public sealed class StatlineExport
{
    [JsonPropertyName("m")] public required string Move { get; init; }
    [JsonPropertyName("t")] public required string Toughness { get; init; }
    [JsonPropertyName("sv")] public required string Save { get; init; }
    [JsonPropertyName("w")] public required string Wounds { get; init; }
    [JsonPropertyName("ld")] public required string Leadership { get; init; }
    [JsonPropertyName("oc")] public required string ObjectiveControl { get; init; }
}

public sealed class WeaponExport
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("range")] public required string Range { get; init; }
    [JsonPropertyName("attacks")] public required string Attacks { get; init; }
    [JsonPropertyName("skill")] public required string Skill { get; init; }
    [JsonPropertyName("strength")] public required string Strength { get; init; }
    [JsonPropertyName("ap")] public required string ArmourPenetration { get; init; }
    [JsonPropertyName("damage")] public required string Damage { get; init; }
    [JsonPropertyName("abilities")] public List<string>? Abilities { get; init; }
    [JsonPropertyName("criticalHitOn")] public int? CriticalHitOn { get; init; }
    [JsonPropertyName("modelsCarrying")] public int ModelsCarrying { get; init; }
    [JsonPropertyName("weaponsPerModel")] public int? WeaponsPerModel { get; init; }
}
