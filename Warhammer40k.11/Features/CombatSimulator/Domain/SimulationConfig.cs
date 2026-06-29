namespace Warhammer40k._11.Features.CombatSimulator.Domain;

/// <summary>
/// One configured combat exchange: the chosen attacker weapons (with firing modes resolved), the target unit,
/// both sides' modifiers, and run settings. Part of the removable Combat Simulator feature.
/// </summary>
public sealed record SimulationConfig
{
    /// <summary>The attacking weapons actually selected to fire (already resolved to one mode each).</summary>
    public List<CombatWeapon> Weapons { get; init; } = [];

    /// <summary>The defending unit.</summary>
    public CombatUnit Target { get; init; } = new();

    public AttackerModifiers Attacker { get; init; } = new();
    public DefenderModifiers Defender { get; init; } = new();

    /// <summary>The target's keywords (used by Anti-X matching), upper-cased by the caller.</summary>
    public List<string> TargetKeywords { get; init; } = [];

    /// <summary>Monte-Carlo iteration count (default 10,000; capped by the runner).</summary>
    public int Iterations { get; init; } = 10_000;

    /// <summary>Optional RNG seed for reproducible runs; null = random.</summary>
    public int? Seed { get; init; }
}
