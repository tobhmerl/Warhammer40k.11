using System.Text.Json.Serialization;

namespace Warhammer40k.Core.Catalogue;

/// <summary>
/// A single unit datasheet. Properties up to <see cref="PointsOptions"/> map directly to the seed file
/// (<c>*-catalogue-seed.json</c>); the remaining properties are <b>derived once at load time</b> by
/// <see cref="CatalogueSeedLoader"/> so the rules engine never re-parses ability text at runtime.
/// </summary>
public sealed class Datasheet
{
    // ---- Seed fields (§7 field map) ----
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("points")] public int Points { get; set; }
    [JsonPropertyName("primaryRole")] public string PrimaryRole { get; set; } = "";
    [JsonPropertyName("isEpicHero")] public bool IsEpicHero { get; set; }
    [JsonPropertyName("isBattleline")] public bool IsBattleline { get; set; }
    [JsonPropertyName("isDedicatedTransport")] public bool IsDedicatedTransport { get; set; }
    [JsonPropertyName("isCharacter")] public bool IsCharacter { get; set; }
    [JsonPropertyName("keywords")] public List<string> Keywords { get; set; } = [];
    [JsonPropertyName("factionRules")] public List<string> FactionRules { get; set; } = [];
    [JsonPropertyName("statProfiles")] public List<StatProfile> StatProfiles { get; set; } = [];
    [JsonPropertyName("abilities")] public List<Ability> Abilities { get; set; } = [];
    [JsonPropertyName("weapons")] public List<WeaponProfile> Weapons { get; set; } = [];
    [JsonPropertyName("pointsOptions")] public List<PointsOption> PointsOptions { get; set; } = [];

    /// <summary>
    /// 1-based copy rank at which this datasheet's <see cref="PointsOption.EscalatedPoints"/> takes over
    /// (e.g. <c>2</c> = "your 2nd+ unit costs more", <c>3</c> = "your 3rd+ unit costs more"). <c>0</c> means
    /// the price never escalates and every copy pays <see cref="PointsOption.Points"/>.
    /// </summary>
    [JsonPropertyName("escalationRank")] public int EscalationRank { get; set; }

    /// <summary>
    /// Authored wargear option-groups (§12: empty-but-present until authored in the Catalogue editor).
    /// A unit's selections (<see cref="Warhammer40k.Core.Rosters.RosterUnit.Wargear"/>) are validated against these by rule R8.
    /// </summary>
    [JsonPropertyName("wargearGroups")] public List<WargearGroup> WargearGroups { get; set; } = [];

    // ---- Derived at load time (not authored in the seed) ----

    /// <summary>Stable slug derived from <see cref="Name"/>; used as the catalogue key and in rosters.</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    /// <summary>True when the datasheet has the <c>Monster</c> keyword (drives Pantheon rule R10).</summary>
    [JsonPropertyName("isMonster")] public bool IsMonster { get; set; }

    /// <summary>Unique units may only appear once. Epic Heroes are treated as unique.</summary>
    [JsonPropertyName("isUnique")] public bool IsUnique { get; set; }

    /// <summary>Copy cap for a 1250–2000 list: Epic Hero/Unique = 1, Battleline/Dedicated Transport = 6, else 3 (§3).</summary>
    [JsonPropertyName("maxCopies")] public int MaxCopies { get; set; }

    /// <summary>A Character that does not state it "cannot be your Warlord" (§3 note).</summary>
    [JsonPropertyName("warlordEligible")] public bool WarlordEligible { get; set; }

    /// <summary>True when the datasheet has the Leader ability.</summary>
    [JsonPropertyName("hasLeaderAbility")] public bool HasLeaderAbility { get; set; }

    /// <summary>True when the Leader text permits co-leading a unit that already has a Leader (e.g. Crypteks, Orikan).</summary>
    [JsonPropertyName("allowsCoLeader")] public bool AllowsCoLeader { get; set; }

    /// <summary>Character, not an Epic Hero, and not forbidden Enhancements by ability text (e.g. C'tan).</summary>
    [JsonPropertyName("canTakeEnhancements")] public bool CanTakeEnhancements { get; set; }

    /// <summary>Datasheet ids this Leader can attach to (parsed from the Leader ability text against known unit names).</summary>
    [JsonPropertyName("leaderTargetIds")] public List<string> LeaderTargetIds { get; set; } = [];

    /// <summary>
    /// Effects this Leader confers on the unit it leads, parsed once at load from its "While this model is
    /// leading a unit, …" abilities (see <see cref="LeaderConferralParser"/>). Drives Play Mode's applied
    /// weapon abilities / stat buffs instead of showing the raw ability text.
    /// </summary>
    [JsonPropertyName("leaderConferrals")] public List<ConferredEffect> LeaderConferrals { get; set; } = [];
}
