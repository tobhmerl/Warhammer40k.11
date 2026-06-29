using Warhammer40k._11.Features.CombatSimulator.Domain;

namespace Warhammer40k._11.Features.CombatSimulator.Engine;

/// <summary>
/// Runs the <see cref="AttackResolver"/> N times and aggregates the per-iteration tallies into a
/// <see cref="SimulationResult"/> (distributions, averaged funnel, models-slain table, P(wiped)). One reusable
/// <see cref="IterationTally"/> and pre-sized arrays keep the hot loop allocation-free. Part of the removable
/// Combat Simulator feature — see <c>Features/CombatSimulator/DELETE.md</c>.
/// </summary>
public sealed class MonteCarloRunner
{
    /// <summary>Hard cap on iterations to keep WASM responsive.</summary>
    public const int MaxIterations = 100_000;

    /// <summary>
    /// Runs the simulation. <paramref name="progress"/> (optional) is reported roughly every 1% for long runs.
    /// </summary>
    public SimulationResult Run(SimulationConfig config, IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        var iterations = Math.Clamp(config.Iterations, 1, MaxIterations);

        var rng = config.Seed is { } seed ? new DiceRoller(seed) : new DiceRoller();
        var resolver = new AttackResolver(config, rng);
        var modelCount = resolver.TargetModelCount;

        var damage = new int[iterations];
        var effective = new int[iterations];
        var slain = new int[iterations];

        // Funnel sums (doubles to avoid overflow on big runs).
        double sAttacks = 0, sHits = 0, sCritHits = 0, sSustained = 0, sLethal = 0, sWounds = 0, sCritWounds = 0,
            sDevMortals = 0, sFailArmour = 0, sFailInvuln = 0, sWoundsSaved = 0, sFnp = 0, sDamage = 0, sEff = 0, sSlain = 0;

        var slainCounts = new int[modelCount + 1];
        var tally = new IterationTally();
        var reportEvery = Math.Max(1, iterations / 100);

        for (var i = 0; i < iterations; i++)
        {
            resolver.ResolveInto(tally);

            damage[i] = tally.DamageDealt;
            effective[i] = tally.EffectiveDamage;
            var s = Math.Clamp(tally.ModelsSlain, 0, modelCount);
            slain[i] = s;
            slainCounts[s]++;

            sAttacks += tally.Attacks; sHits += tally.Hits; sCritHits += tally.CriticalHits;
            sSustained += tally.SustainedExtraHits; sLethal += tally.LethalAutoWounds; sWounds += tally.Wounds;
            sCritWounds += tally.CriticalWounds; sDevMortals += tally.DevastatingMortalWounds;
            sFailArmour += tally.FailedArmourSaves; sFailInvuln += tally.FailedInvulnSaves;
            sWoundsSaved += tally.WoundsSaved; sFnp += tally.FnpIgnored;
            sDamage += tally.DamageDealt; sEff += tally.EffectiveDamage; sSlain += tally.ModelsSlain;

            if (progress is not null && (i + 1) % reportEvery == 0)
                progress.Report((i + 1) / (double)iterations);
        }

        var n = iterations;
        var funnel = new FunnelAverages
        {
            Attacks = sAttacks / n,
            Hits = sHits / n,
            CriticalHits = sCritHits / n,
            SustainedExtraHits = sSustained / n,
            LethalAutoWounds = sLethal / n,
            Wounds = sWounds / n,
            CriticalWounds = sCritWounds / n,
            DevastatingMortalWounds = sDevMortals / n,
            FailedArmourSaves = sFailArmour / n,
            FailedInvulnSaves = sFailInvuln / n,
            WoundsSaved = sWoundsSaved / n,
            FnpIgnored = sFnp / n,
            DamageDealt = sDamage / n,
            EffectiveDamage = sEff / n,
            ModelsSlain = sSlain / n,
        };

        var slainProbs = new double[modelCount + 1];
        for (var k = 0; k <= modelCount; k++)
            slainProbs[k] = slainCounts[k] / (double)n;

        return new SimulationResult
        {
            Iterations = n,
            Damage = Summarize(damage),
            EffectiveDamage = Summarize(effective),
            ModelsSlain = Summarize(slain),
            Funnel = funnel,
            ModelsSlainProbabilities = slainProbs,
            ProbabilityWiped = modelCount > 0 ? slainCounts[modelCount] / (double)n : 0,
            ExpectedValue = ExpectedValueCalculator.Compute(config),
            ReanimationPreview = ReanimationPreview(config),
        };
    }

    private static Distribution Summarize(int[] values)
    {
        var n = values.Length;
        if (n == 0)
            return new Distribution();

        var sorted = (int[])values.Clone();
        Array.Sort(sorted);

        double mean = 0;
        for (var i = 0; i < n; i++)
            mean += values[i];
        mean /= n;

        double variance = 0;
        for (var i = 0; i < n; i++)
        {
            var d = values[i] - mean;
            variance += d * d;
        }
        variance /= n;

        var histogram = new Dictionary<int, int>();
        foreach (var v in values)
            histogram[v] = histogram.TryGetValue(v, out var c) ? c + 1 : 1;

        return new Distribution
        {
            Mean = mean,
            Median = Percentile(sorted, 50),
            StdDev = Math.Sqrt(variance),
            Min = sorted[0],
            Max = sorted[^1],
            P10 = Percentile(sorted, 10),
            P25 = Percentile(sorted, 25),
            P75 = Percentile(sorted, 75),
            P90 = Percentile(sorted, 90),
            Histogram = histogram,
        };
    }

    private static int Percentile(int[] sorted, int percentile)
    {
        if (sorted.Length == 0)
            return 0;
        var rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Length) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Length - 1)];
    }

    private static string? ReanimationPreview(SimulationConfig config)
    {
        var ability = config.Target.UnitAbilities
            .FirstOrDefault(a => a.Name.Contains("Reanimation", StringComparison.OrdinalIgnoreCase)
                || a.Description.Contains("Reanimation Protocols", StringComparison.OrdinalIgnoreCase));
        if (ability is null)
            return null;
        // Standard Reanimation Protocols heals D3 (mean 2) wounds in the next Command phase — out of exchange.
        return "Reanimation Protocols (next Command phase, not part of this exchange): ~2 wounds reanimated (D3).";
    }
}
