namespace Warhammer40k._11.Features.CombatSimulator.Domain;

/// <summary>Where a unit's data came from. Part of the removable Combat Simulator feature.</summary>
public enum CombatSource
{
    Native,
    Imported,
}

/// <summary>A unit-level ability: raw text plus a best-effort parsed defensive effect (§6b.7).</summary>
public sealed record UnitAbility
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";

    /// <summary>A detected invulnerable save target, or null.</summary>
    public int? InvulnSave { get; init; }

    /// <summary>A detected Feel No Pain target, or null.</summary>
    public int? FeelNoPain { get; init; }

    /// <summary>True when the detected FNP is restricted to mortal wounds (or a similar condition).</summary>
    public bool FnpMortalOnly { get; init; }

    /// <summary>A detected flat damage reduction (e.g. "subtract 1 from the Damage characteristic").</summary>
    public int DamageReductionFlat { get; init; }

    /// <summary>True when the ability halves incoming damage.</summary>
    public bool DamageHalved { get; init; }

    /// <summary>True when this ability changed any defensive field (so the UI can pre-tick it).</summary>
    public bool HasParsedEffect =>
        InvulnSave is not null || FeelNoPain is not null || DamageReductionFlat > 0 || DamageHalved;
}

/// <summary>A group of identical models within a unit (supports mixed-model units).</summary>
public sealed record CombatModelGroup
{
    public CombatModelProfile Profile { get; init; } = new();
    public int Count { get; init; } = 1;
    public List<CombatWeapon> Weapons { get; init; } = [];
}

/// <summary>
/// A unit normalized for the engine: model-groups (each a profile + count + weapons) and unit abilities.
/// Both native and imported data normalize into this single representation.
/// </summary>
public sealed record CombatUnit
{
    public string Name { get; init; } = "";
    public string Faction { get; init; } = "";
    public List<CombatModelGroup> ModelGroups { get; init; } = [];
    public List<UnitAbility> UnitAbilities { get; init; } = [];
    public CombatSource Source { get; init; } = CombatSource.Native;
    public bool IsAttachedUnit { get; init; }

    /// <summary>When set, all incoming attacks use this Toughness (an attached unit's bodyguard T).</summary>
    public int? BodyguardToughness { get; init; }

    /// <summary>Total models across all groups.</summary>
    public int TotalModels => ModelGroups.Sum(g => g.Count);

    /// <summary>Total wounds across all groups (used to cap "effective damage").</summary>
    public int TotalWounds => ModelGroups.Sum(g => g.Count * Math.Max(1, g.Profile.Wounds));

    /// <summary>Every weapon across the unit's model-groups, in order.</summary>
    public IEnumerable<CombatWeapon> AllWeapons => ModelGroups.SelectMany(g => g.Weapons);
}
