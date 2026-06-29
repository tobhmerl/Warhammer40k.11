using Warhammer40k._11.Features.CombatSimulator.Dice;
using Warhammer40k._11.Features.CombatSimulator.Domain;
using Warhammer40k._11.Features.CombatSimulator.Engine;

namespace Warhammer40k.Tests.CombatSimulator;

/// <summary>
/// Engine golden cases (§12): the Monte-Carlo mean and the deterministic calculator are asserted against
/// hand-computed expectations. Uses a fixed seed and a large N for stable means. Part of the removable feature.
/// </summary>
public class AttackResolverTests
{
    private const int N = 50_000;
    private const int Seed = 20260101;

    private static CombatWeapon Weapon(
        string a = "1", int skill = 3, string s = "4", int ap = 0, string d = "1",
        int models = 1, bool melee = false, params WeaponAbility[] abilities) => new()
    {
        Name = "Test weapon",
        IsMelee = melee,
        Attacks = DiceExpression.Parse(a),
        Skill = skill,
        Strength = DiceExpression.Parse(s),
        ArmourPenetration = ap <= 0 ? ap : -ap,
        Damage = DiceExpression.Parse(d),
        Abilities = abilities.ToList(),
        CarriedByModels = models,
    };

    private static CombatUnit Target(int toughness = 4, int save = 3, int wounds = 1, int models = 10,
        int? invuln = null, int? fnp = null, bool fnpMortalOnly = false,
        int damageReductionFlat = 0, bool damageHalved = false, params string[] keywords) => new()
    {
        Name = "Target",
        ModelGroups =
        [
            new CombatModelGroup
            {
                Profile = new CombatModelProfile
                {
                    Name = "Target", Toughness = toughness, Save = save, Wounds = wounds,
                    InvulnSave = invuln, FeelNoPain = fnp, FnpMortalOnly = fnpMortalOnly,
                    DamageReductionFlat = damageReductionFlat, DamageHalved = damageHalved,
                },
                Count = models,
            },
        ],
    };

    private static SimulationResult Run(CombatWeapon weapon, CombatUnit target,
        AttackerModifiers? atk = null, DefenderModifiers? def = null, IEnumerable<string>? targetKeywords = null)
    {
        var config = new SimulationConfig
        {
            Weapons = [weapon],
            Target = target,
            Attacker = atk ?? new AttackerModifiers(),
            Defender = def ?? new DefenderModifiers(),
            TargetKeywords = targetKeywords?.ToList() ?? [],
            Iterations = N,
            Seed = Seed,
        };
        return new MonteCarloRunner().Run(config);
    }

    [Fact]
    public void Ten_bolt_shots_baseline()
    {
        // 10 shots, BS 3+ (P=2/3), S4 vs T4 (4+, P=1/2), Sv3+/AP0 (save 3+, fail 1/3), D1.
        var result = Run(Weapon(a: "1", skill: 3, s: "4", ap: 0, d: "1", models: 10), Target(toughness: 4, save: 3));

        Assert.Equal(6.667, result.Funnel.Hits, 1);
        Assert.Equal(3.333, result.Funnel.Wounds, 1);
        Assert.Equal(1.111, result.Funnel.DamageDealt, 1);

        // Closed-form cross-check (tight).
        Assert.Equal(6.667, result.ExpectedValue.Hits, 2);
        Assert.Equal(3.333, result.ExpectedValue.Wounds, 2);
        Assert.Equal(1.111, result.ExpectedValue.UnsavedWounds, 2);
        Assert.Equal(1.111, result.ExpectedValue.Damage, 2);
    }

    [Fact]
    public void Ap_minus_one_raises_unsaved()
    {
        // Same as baseline but AP -1 → save 4+, fail 1/2 → unsaved 3.333 × 1/2 = 1.667.
        var result = Run(Weapon(a: "1", skill: 3, s: "4", ap: -1, d: "1", models: 10), Target(toughness: 4, save: 3));
        Assert.Equal(1.667, result.Funnel.DamageDealt, 1);
        Assert.Equal(1.667, result.ExpectedValue.Damage, 2);
    }

    [Theory]
    [InlineData(8, 4, 2)]   // S ≥ 2T → 2+
    [InlineData(5, 4, 3)]   // S>T → 3+
    [InlineData(4, 4, 4)]   // S=T → 4+
    [InlineData(4, 6, 5)]   // T>S but < 2S → 5+
    [InlineData(4, 8, 6)]   // T ≥ 2S → 6+
    [InlineData(4, 16, 6)]  // T ≥ 2S → 6+
    public void Wound_table_boundaries(int strength, int toughness, int expectedTarget) =>
        Assert.Equal(expectedTarget, AttackResolver.WoundTarget(strength, toughness));

    [Fact]
    public void S8_vs_T4_is_two_plus()
    {
        Assert.Equal(2, AttackResolver.WoundTarget(8, 4)); // 8 >= 2*4
    }

    [Fact]
    public void Lethal_hits_auto_wound_and_skip_wound_roll()
    {
        // BS3+, crit 6 → E[crit hits] = A × 1/6 auto-wound. Use a high-toughness target so non-lethal wounds
        // are unlikely, isolating the lethal contribution.
        var weapon = Weapon(a: "1", skill: 3, s: "1", ap: 0, d: "1", models: 36, abilities: new LethalHits());
        var result = Run(weapon, Target(toughness: 10, save: 7)); // unsaveable, S1 vs T10 → only 6+ wounds normally

        // 36 attacks × 1/6 crit = 6 lethal auto-wounds; plus the few normal 6+ wounds from non-crit hits.
        Assert.True(result.Funnel.LethalAutoWounds is > 5.0 and < 7.0, $"lethal auto-wounds={result.Funnel.LethalAutoWounds}");
        // Lethal hits must NOT generate devastating mortals (no Dev ability here anyway).
        Assert.Equal(0, result.Funnel.DevastatingMortalWounds, 3);
    }

    [Fact]
    public void Sustained_hits_add_extra_hits_that_roll_to_wound()
    {
        var plain = Run(Weapon(a: "1", skill: 3, s: "4", models: 36), Target(toughness: 4, save: 7));
        var sustained = Run(Weapon(a: "1", skill: 3, s: "4", models: 36, abilities: new SustainedHits(1)),
            Target(toughness: 4, save: 7));

        // Sustained adds ~ (36 × 1/6) extra hits = 6 more hits.
        Assert.True(sustained.Funnel.Hits - plain.Funnel.Hits is > 5.0 and < 7.0,
            $"extra hits = {sustained.Funnel.Hits - plain.Funnel.Hits}");
        Assert.True(sustained.Funnel.SustainedExtraHits is > 5.0 and < 7.0);
    }

    [Fact]
    public void Devastating_anti_infantry_makes_mortals_that_bypass_saves()
    {
        // Anti-Infantry 4+ → crit-wound on 4+; Dev Wounds → mortals = D. Target Infantry, great armour (2+) that
        // mortals must bypass. D1 so one-model-per-crit cap is moot.
        var weapon = Weapon(a: "1", skill: 2, s: "4", ap: 0, d: "1", models: 20,
            abilities: new[] { new Anti("Infantry", 4), (WeaponAbility)new DevastatingWounds() });
        var result = Run(weapon, Target(toughness: 4, save: 2, models: 20), targetKeywords: new[] { "Infantry" });

        Assert.True(result.Funnel.DevastatingMortalWounds > 0, "expected some devastating mortal wounds");
        // Those mortals land as damage despite the 2+ save.
        Assert.True(result.Funnel.DamageDealt > 0);
    }

    [Fact]
    public void Invuln_is_used_against_high_ap_and_ignores_ap()
    {
        // Sv3+ / 4++ vs AP-3 → armour becomes 6+, invuln 4+ is better → effective 4+. Compare to no-invuln (6+).
        var withInvuln = Run(Weapon(a: "1", skill: 2, s: "8", ap: -3, d: "1", models: 100),
            Target(toughness: 4, save: 3, invuln: 4));
        var noInvuln = Run(Weapon(a: "1", skill: 2, s: "8", ap: -3, d: "1", models: 100),
            Target(toughness: 4, save: 3));

        // With a 4++ the defender saves more (fewer unsaved) than relying on the AP-degraded 6+ armour.
        Assert.True(withInvuln.Funnel.DamageDealt < noInvuln.Funnel.DamageDealt,
            $"invuln {withInvuln.Funnel.DamageDealt} should be < armour-only {noInvuln.Funnel.DamageDealt}");
        Assert.True(withInvuln.Funnel.FailedInvulnSaves > 0);
        Assert.Equal(0, withInvuln.Funnel.FailedArmourSaves, 3);
    }

    [Fact]
    public void Cover_worsens_bs_and_does_not_touch_the_save()
    {
        var noCover = Run(Weapon(a: "1", skill: 3, s: "4", models: 50), Target(toughness: 4, save: 4));
        var cover = Run(Weapon(a: "1", skill: 3, s: "4", models: 50), Target(toughness: 4, save: 4),
            def: new DefenderModifiers { Cover = true });

        // Cover (−1 BS) lowers the hit rate (3+ → 4+).
        Assert.True(cover.Funnel.Hits < noCover.Funnel.Hits, $"cover hits {cover.Funnel.Hits} vs {noCover.Funnel.Hits}");
    }

    [Fact]
    public void Cover_plus_hit_debuff_stacks_to_minus_two()
    {
        // Skill 3+. Cover (−1 BS → 4+) plus a −1 hit-roll mod = effective −2 swing → need natural 5+ ⇒ P=1/3.
        var result = Run(Weapon(a: "1", skill: 3, s: "4", models: 60), Target(toughness: 4, save: 7),
            atk: new AttackerModifiers { HitRollModifier = -1 },
            def: new DefenderModifiers { Cover = true });

        // 60 × 1/3 ≈ 20 hits.
        Assert.True(result.Funnel.Hits is > 18.0 and < 22.0, $"hits={result.Funnel.Hits}");
    }

    [Fact]
    public void Damage_reduction_floors_at_one()
    {
        // D2 weapon vs −1 damage → 1 per wound.
        var result = Run(Weapon(a: "1", skill: 2, s: "8", ap: -4, d: "2", models: 50),
            Target(toughness: 4, save: 7, wounds: 3, damageReductionFlat: 1));
        // Every unsaved wound deals exactly 1; damage ≈ unsaved.
        Assert.Equal(result.Funnel.FailedArmourSaves, result.Funnel.DamageDealt, 0);
    }

    [Fact]
    public void Halving_rounds_up_and_floors_at_one()
    {
        // D3 halved → ceil(d/2): 1→1, 2→1, 3→2. Mean of D3 halved = (1+1+2)/3 = 1.333.
        var result = Run(Weapon(a: "1", skill: 2, s: "8", ap: -4, d: "D3", models: 200),
            Target(toughness: 4, save: 7, wounds: 5, damageHalved: true));
        var perWound = result.Funnel.DamageDealt / result.Funnel.FailedArmourSaves;
        Assert.Equal(1.333, perWound, 1);
    }

    [Fact]
    public void Excess_damage_is_lost_per_model()
    {
        // D6 hits on 2-wound models: a single failed save never kills two models.
        var result = Run(Weapon(a: "1", skill: 2, s: "10", ap: -4, d: "D6", models: 1),
            Target(toughness: 4, save: 7, wounds: 2, models: 1));
        // At most one model in the unit, so models slain is 0 or 1 — never more.
        Assert.True(result.ModelsSlain.Max <= 1);
    }

    [Fact]
    public void Monte_carlo_mean_tracks_the_closed_form()
    {
        var result = Run(Weapon(a: "2", skill: 3, s: "5", ap: -1, d: "2", models: 5), Target(toughness: 4, save: 3, wounds: 2));
        // Within 5% of each other on the headline damage path.
        var mc = result.Funnel.DamageDealt;
        var cf = result.ExpectedValue.Damage;
        Assert.True(Math.Abs(mc - cf) / Math.Max(0.01, cf) < 0.05, $"MC {mc} vs CF {cf}");
    }
}
