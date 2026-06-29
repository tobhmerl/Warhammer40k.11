namespace Warhammer40k._11.Features.CombatSimulator.Domain;

/// <summary>
/// The funnel counters recorded for a single Monte-Carlo iteration (§8.6). Plain mutable struct-like class so
/// the hot loop reuses one instance per iteration with no per-roll allocations. Part of the removable feature.
/// </summary>
public sealed class IterationTally
{
    public int Attacks;
    public int Hits;
    public int CriticalHits;
    public int SustainedExtraHits;
    public int LethalAutoWounds;
    public int Wounds;
    public int CriticalWounds;
    public int DevastatingMortalWounds;
    public int FailedArmourSaves;
    public int FailedInvulnSaves;
    public int WoundsSaved;
    public int FnpIgnored;

    /// <summary>Total damage that landed after FNP (uncapped).</summary>
    public int DamageDealt;

    /// <summary>Damage capped at the unit's remaining total wounds.</summary>
    public int EffectiveDamage;

    public int ModelsSlain;

    public void Reset()
    {
        Attacks = Hits = CriticalHits = SustainedExtraHits = LethalAutoWounds = 0;
        Wounds = CriticalWounds = DevastatingMortalWounds = 0;
        FailedArmourSaves = FailedInvulnSaves = WoundsSaved = FnpIgnored = 0;
        DamageDealt = EffectiveDamage = ModelsSlain = 0;
    }
}

/// <summary>Mean/spread statistics over a numeric series, plus a histogram.</summary>
public sealed record Distribution
{
    public double Mean { get; init; }
    public double Median { get; init; }
    public double StdDev { get; init; }
    public int Min { get; init; }
    public int Max { get; init; }
    public int P10 { get; init; }
    public int P25 { get; init; }
    public int P75 { get; init; }
    public int P90 { get; init; }

    /// <summary>Histogram: value -> count of iterations producing it.</summary>
    public IReadOnlyDictionary<int, int> Histogram { get; init; } = new Dictionary<int, int>();
}

/// <summary>The averaged funnel across all iterations (§8.6).</summary>
public sealed record FunnelAverages
{
    public double Attacks { get; init; }
    public double Hits { get; init; }
    public double CriticalHits { get; init; }
    public double SustainedExtraHits { get; init; }
    public double LethalAutoWounds { get; init; }
    public double Wounds { get; init; }
    public double CriticalWounds { get; init; }
    public double DevastatingMortalWounds { get; init; }
    public double FailedArmourSaves { get; init; }
    public double FailedInvulnSaves { get; init; }
    public double WoundsSaved { get; init; }
    public double FnpIgnored { get; init; }
    public double DamageDealt { get; init; }
    public double EffectiveDamage { get; init; }
    public double ModelsSlain { get; init; }
}

/// <summary>The deterministic closed-form cross-check (§9).</summary>
public sealed record ExpectedValueSummary
{
    public double Attacks { get; init; }
    public double Hits { get; init; }
    public double Wounds { get; init; }
    public double UnsavedWounds { get; init; }
    public double Damage { get; init; }
    public double ModelsSlain { get; init; }
}

/// <summary>
/// The full result of a Monte-Carlo run: distributions for damage and models slain, the averaged funnel,
/// <c>P(unit wiped)</c>, the per-models-slain probability table, and the deterministic cross-check.
/// </summary>
public sealed record SimulationResult
{
    public int Iterations { get; init; }
    public Distribution Damage { get; init; } = new();
    public Distribution EffectiveDamage { get; init; } = new();
    public Distribution ModelsSlain { get; init; } = new();
    public FunnelAverages Funnel { get; init; } = new();

    /// <summary>P(models slain == k) for k = 0..unit size.</summary>
    public IReadOnlyList<double> ModelsSlainProbabilities { get; init; } = [];

    /// <summary>P(every model in the target unit slain).</summary>
    public double ProbabilityWiped { get; init; }

    public ExpectedValueSummary ExpectedValue { get; init; } = new();

    /// <summary>Optional Reanimation Protocols preview (out-of-exchange), or null when not applicable.</summary>
    public string? ReanimationPreview { get; init; }
}
