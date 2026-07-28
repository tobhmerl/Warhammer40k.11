using Warhammer40k.Core.Catalogue;

namespace Warhammer40k.Core.Tactical;

/// <summary>Which player a token belongs to on the tactical map.</summary>
public enum MapSide
{
    /// <summary>The signed-in player's own units.</summary>
    Player,

    /// <summary>The opponent's units (planned/anticipated).</summary>
    Opponent,
}

/// <summary>
/// A single draggable token on the tactical map — one model of a unit. Positions are stored in board
/// inches (origin = top-left of the play area) so a saved plan renders identically at any zoom or screen
/// size. Base size drives the token's on-board diameter and later measuring/coherency checks.
/// </summary>
public sealed class MapToken
{
    /// <summary>Stable id (unique within a plan).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    /// <summary>The roster unit this model belongs to (groups a unit's models for coherency).</summary>
    public string RosterUnitId { get; set; } = string.Empty;

    /// <summary>Short label shown on/under the token (usually the unit name).</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Which side the token belongs to (drives its color family).</summary>
    public MapSide Side { get; set; }

    /// <summary>Short unit abbreviation shown on the token (e.g. "WR", "TB"). Editable per unit.</summary>
    public string Abbrev { get; set; } = string.Empty;

    /// <summary>Index into the side's color palette; one per distinct unit so units read apart.</summary>
    public int ColorIndex { get; set; }

    /// <summary>The model's round-base diameter in millimetres (drives on-board size and spacing).</summary>
    public int BaseMm { get; set; } = 32;

    /// <summary>Horizontal position on the board, in inches from the left edge.</summary>
    public double XInches { get; set; }

    /// <summary>Vertical position on the board, in inches from the top edge.</summary>
    public double YInches { get; set; }

    /// <summary>The model's Move characteristic in inches, when known (drives the move/charge range rings).</summary>
    public double? MoveInches { get; set; }

    /// <summary>The model's longest ranged-weapon range in inches, when known (drives the gun range ring).</summary>
    public double? WeaponRangeInches { get; set; }
}

/// <summary>
/// A saved tactical plan: a set of tokens placed on a named map for a given roster. Persisted per user
/// (server-side, like rosters) so setups can be revisited and refined before a game.
/// </summary>
public sealed class TacticalPlan
{
    /// <summary>Server-assigned id (empty until first save).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Player-facing plan name.</summary>
    public string Name { get; set; } = "Untitled plan";

    /// <summary>The roster whose units seed this plan's player-side tokens.</summary>
    public string RosterId { get; set; } = string.Empty;

    /// <summary>The map layout id this plan is built on (e.g. <c>layout-a</c>).</summary>
    public string MapId { get; set; } = TacticalMaps.DefaultMapId;

    /// <summary>Every token currently placed on the board.</summary>
    public List<MapToken> Tokens { get; set; } = [];

    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ModifiedUtc { get; set; }
}

/// <summary>A rectangular deployment zone (in board inches) belonging to one side.</summary>
public sealed record DeploymentZone(MapSide Side, double XInches, double YInches, double WidthInches, double HeightInches);

/// <summary>
/// A predefined battlefield: board size, background image, and (optionally) deployment zones. v1 ships the
/// single "Layout A" map; more layouts (and traced terrain for line-of-sight) come later.
/// </summary>
public sealed class MapDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Board width in inches (standard matched play is 44).</summary>
    public double WidthInches { get; set; } = 44;

    /// <summary>Board height in inches (standard matched play is 60).</summary>
    public double HeightInches { get; set; } = 60;

    /// <summary>App-relative URL of the background image.</summary>
    public string BackgroundUrl { get; set; } = string.Empty;

    /// <summary>Deployment zones drawn as overlays (may be empty when the background already shows them).</summary>
    public List<DeploymentZone> DeploymentZones { get; set; } = [];
}

/// <summary>The built-in map layouts. v1 has one: "Layout A" (44"x60").</summary>
public static class TacticalMaps
{
    public const string DefaultMapId = "layout-a";

    public static IReadOnlyList<MapDefinition> All { get; } =
    [
        new MapDefinition
        {
            Id = DefaultMapId,
            Name = "Layout A",
            WidthInches = 44,
            HeightInches = 60,
            BackgroundUrl = "maps/Layout A.jpg",
        },
    ];

    /// <summary>The map for an id, or the default map when unknown.</summary>
    public static MapDefinition Resolve(string? id) =>
        All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}

/// <summary>
/// Round-base diameters (mm) used to draw a model to scale on the tactical map, where the board is measured
/// in inches. Positioning is only trustworthy when the token matches the real base: coherency, engagement
/// range and "does this fit through the gap" all depend on it.
/// </summary>
/// <remarks>
/// Necron datasheets are listed explicitly because keywords alone cannot separate, say, a 32 mm Necron
/// Warrior from a 50 mm Skorpekh Destroyer — both are simply INFANTRY. Other factions still fall back to the
/// keyword heuristic, which is deliberately conservative rather than precise.
/// Oval and rectangular bases are entered as their <b>longer</b> dimension: a round token of that size is the
/// safe approximation, since it never claims a model fits somewhere it would not.
/// The value stays editable and is stored on the plan, so these are a starting point, not a constraint.
/// </remarks>
public static class BaseSizeDefaults
{
    /// <summary>Fallback when nothing matches (standard infantry).</summary>
    public const int Default = 32;

    // Keyed by datasheet slug (the catalogue id). Values are the current GW base sizes; ovals use their
    // long axis, noted in the comment where that applies.
    private static readonly Dictionary<string, int> ByDatasheetId = new(StringComparer.OrdinalIgnoreCase)
    {
        // ---- Infantry ----
        ["necron-warriors"] = 32,
        ["immortals"] = 32,
        ["deathmarks"] = 32,
        ["flayed-ones"] = 32,
        ["cryptothralls"] = 40,
        ["lychguard"] = 40,
        ["triarch-praetorians"] = 40,
        ["skorpekh-destroyers"] = 50,
        ["ophydian-destroyers"] = 50,

        // ---- Characters ----
        ["overlord"] = 40,
        ["overlord-with-translocation-shroud"] = 40,
        ["royal-warden"] = 40,
        ["plasmancer"] = 40,
        ["chronomancer"] = 40,
        ["geomancer"] = 40,
        ["psychomancer"] = 40,
        ["technomancer"] = 40,
        ["hexmark-destroyer"] = 40,
        ["orikan-the-diviner"] = 40,
        ["trazyn-the-infinite"] = 40,
        ["imotekh-the-stormlord"] = 40,
        ["nekrosor-ammentar"] = 40,
        ["illuminor-szeras"] = 80,
        ["skorpekh-lord"] = 60,
        ["lokhust-lord"] = 60,          // 60 mm oval (long axis)
        ["catacomb-command-barge"] = 120, // 120 x 92 mm oval
        ["the-silent-king"] = 130,      // 130 x 80 mm oval

        // ---- C'tan ----
        ["ctan-shard-of-the-deceiver"] = 60,
        ["ctan-shard-of-the-nightbringer"] = 60,
        ["ctan-shard-of-the-void-dragon"] = 100,
        ["transcendent-ctan"] = 60,

        // ---- Mounted / Beasts / Swarms ----
        ["canoptek-scarab-swarms"] = 40,
        ["tomb-blades"] = 60,           // 60 mm oval
        ["lokhust-destroyers"] = 60,    // 60 mm oval
        ["lokhust-heavy-destroyers"] = 60,
        ["canoptek-wraiths"] = 60,      // 60 mm oval
        ["canoptek-tomb-crawlers"] = 90,
        ["canoptek-macrocytes"] = 90,

        // ---- Vehicles ----
        ["canoptek-spyders"] = 60,
        ["canoptek-reanimator"] = 80,
        ["canoptek-doomstalker"] = 80,
        ["triarch-stalker"] = 80,
        ["annihilation-barge"] = 120,   // 120 x 92 mm oval
        ["ghost-ark"] = 120,            // 120 x 92 mm oval
        ["doomsday-ark"] = 120,         // 120 x 92 mm oval
        ["doom-scythe"] = 120,          // 120 x 92 mm oval
        ["night-scythe"] = 120,
        ["convergence-of-dominion"] = 80,

        // ---- Titanic ----
        ["monolith"] = 180,             // squared hull, approximated by its footprint
        ["obelisk"] = 180,
        ["tesseract-vault"] = 180,
        ["seraptek-heavy-construct"] = 170,
    };

    /// <summary>The base diameter (mm) for a datasheet: the exact value when known, else a keyword guess.</summary>
    public static int ForDatasheet(Datasheet datasheet)
    {
        if (datasheet is null)
            return Default;
        if (datasheet.Id is { Length: > 0 } id && ByDatasheetId.TryGetValue(id, out var known))
            return known;
        return ForKeywords(datasheet.Keywords);
    }

    /// <summary>True when an exact base size is on file for this datasheet id (rather than a guess).</summary>
    public static bool IsKnown(string? datasheetId) =>
        datasheetId is { Length: > 0 } && ByDatasheetId.ContainsKey(datasheetId);

    /// <summary>A keyword-based estimate, used for datasheets with no entry in the table.</summary>
    public static int ForKeywords(IEnumerable<string> keywords)
    {
        var set = new HashSet<string>(keywords ?? [], StringComparer.OrdinalIgnoreCase);

        if (set.Contains("Titanic") || set.Contains("Towering"))
            return 160;
        if (set.Contains("Vehicle") || set.Contains("Monster"))
            return 90;
        if (set.Contains("Mounted") || set.Contains("Beast"))
            return 60;
        if (set.Contains("Swarm"))
            return 40;
        if (set.Contains("Terminator") || set.Contains("Gravis"))
            return 40;
        if (set.Contains("Epic Hero") || set.Contains("Character"))
            return 40;
        // Standard infantry / everything else.
        return Default;
    }
}
