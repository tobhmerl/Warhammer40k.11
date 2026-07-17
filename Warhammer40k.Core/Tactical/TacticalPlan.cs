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
/// Sensible round-base diameters (mm) inferred from a datasheet's keywords, used to seed a token's base
/// size when the player hasn't set one. Option B: the value is editable and stored on the plan, so these
/// are only the starting point. Ovals/vehicles are approximated by their longer dimension for spacing.
/// </summary>
public static class BaseSizeDefaults
{
    /// <summary>Fallback when no keyword matches (standard infantry).</summary>
    public const int Default = 32;

    /// <summary>The default base diameter (mm) for a datasheet, from its keywords.</summary>
    public static int ForDatasheet(Datasheet datasheet)
    {
        if (datasheet is null)
            return Default;
        return ForKeywords(datasheet.Keywords);
    }

    /// <summary>The default base diameter (mm) for a set of unit keywords.</summary>
    public static int ForKeywords(IEnumerable<string> keywords)
    {
        var set = new HashSet<string>(keywords ?? [], StringComparer.OrdinalIgnoreCase);

        if (set.Contains("Titanic") || set.Contains("Monster") || set.Contains("Vehicle"))
            return 90;
        if (set.Contains("Mounted") || set.Contains("Beast"))
            return 60;
        if (set.Contains("Epic Hero") || set.Contains("Character"))
            return 40;
        if (set.Contains("Swarm"))
            return 40;
        // Standard infantry / everything else.
        return Default;
    }
}
