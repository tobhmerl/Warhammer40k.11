namespace Warhammer40k._11.Features.CombatSimulator.Engine;

/// <summary>
/// A thin, seedable wrapper over a pseudo-random generator. One instance is shared across a whole
/// Monte-Carlo run so results are reproducible when a seed is supplied. Part of the removable Combat
/// Simulator feature — see <c>Features/CombatSimulator/DELETE.md</c>.
/// </summary>
public sealed class DiceRoller
{
    private readonly Random _random;

    /// <summary>Creates a roller with a random seed (non-reproducible).</summary>
    public DiceRoller() => _random = new Random();

    /// <summary>Creates a roller with an explicit seed for reproducible runs.</summary>
    public DiceRoller(int seed) => _random = new Random(seed);

    /// <summary>Rolls a single d6 (1..6).</summary>
    public int D6() => _random.Next(1, 7);

    /// <summary>Rolls a single dN with <paramref name="sides"/> faces (1..sides). Returns 0 for non-positive sides.</summary>
    public int Die(int sides) => sides <= 0 ? 0 : _random.Next(1, sides + 1);
}
