using Warhammer40k._11.Features.CombatSimulator.Domain;

namespace Warhammer40k._11.Features.CombatSimulator.Engine;

/// <summary>
/// A deterministic closed-form expected-value cross-check (§9): probabilities at each step × dice means. It is
/// intentionally a <b>simplified</b> model of the headline path (hit → wound → save → damage), used as a sanity
/// rail next to the Monte-Carlo mean — a large divergence signals a bug. It does not attempt to model every
/// interaction (e.g. Devastating one-model caps or sequential allocation), so small differences are expected
/// when those are in play. Part of the removable Combat Simulator feature.
/// </summary>
public static class ExpectedValueCalculator
{
    /// <summary>P(d6 natural roll meets a target, with a clamped ±1 modifier and a crit auto-success threshold).</summary>
    private static double SuccessProbability(int target, int modifier, int critThreshold)
    {
        // natural 1 always fails; natural >= crit always succeeds; else modified die >= target.
        var success = 0;
        for (var natural = 1; natural <= 6; natural++)
        {
            if (natural == 1)
                continue;
            if (natural >= critThreshold || natural + modifier >= target)
                success++;
        }
        return success / 6.0;
    }

    private static double CritProbability(int critThreshold)
    {
        var faces = 0;
        for (var natural = 1; natural <= 6; natural++)
            if (natural != 1 && natural >= critThreshold)
                faces++;
        return faces / 6.0;
    }

    /// <summary>Computes the closed-form expectations for the whole configured exchange (summed over weapons).</summary>
    public static ExpectedValueSummary Compute(SimulationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var atk = config.Attacker;
        var def = config.Defender;

        var profile = config.Target.ModelGroups.FirstOrDefault()?.Profile ?? new CombatModelProfile();
        var toughness = def.ToughnessOverride ?? config.Target.BodyguardToughness ?? profile.Toughness;
        var models = def.ModelCountOverride ?? config.Target.TotalModels;

        double totalAttacks = 0, totalHits = 0, totalWounds = 0, totalUnsaved = 0, totalDamage = 0;

        foreach (var weapon in config.Weapons)
        {
            var firing = Math.Max(0, weapon.CarriedByModels);
            var perModelAttacks = weapon.Attacks.ExpectedValue() + atk.AttacksBonus;
            if (atk.TargetWithinHalfRange && weapon.Get<RapidFire>() is { } rf)
                perModelAttacks += rf.X;
            if (weapon.Has<Blast>())
                perModelAttacks += models / 5;
            var attacks = Math.Max(0, perModelAttacks) * firing;
            totalAttacks += attacks;

            // ---- Hit ----
            double hits;
            double critHits;
            if (weapon.Has<Torrent>())
            {
                hits = attacks;
                critHits = 0;
            }
            else
            {
                var bsBucket = atk.BsWsModifier;
                if (def.Cover && !(atk.IgnoresCover || weapon.Has<IgnoresCover>()))
                    bsBucket -= 1;
                var hitRoll = Math.Clamp(atk.HitRollModifier - (def.MinusOneToBeHit ? 1 : 0), -1, 1);
                var target = weapon.Skill - bsBucket;
                if (weapon.Has<Psychic>())
                {
                    target = weapon.Skill;
                    hitRoll = 0;
                }
                var pHit = SuccessProbability(target, hitRoll, atk.CritHitThreshold);
                hits = attacks * pHit;
                critHits = attacks * CritProbability(atk.CritHitThreshold);

                // Sustained Hits add extra hits per crit.
                var sustained = atk.SustainedHits + (weapon.Get<SustainedHits>()?.X ?? 0);
                hits += critHits * sustained;
            }
            totalHits += hits;

            // ---- Wound ----
            var strength = (int)Math.Round(weapon.Strength.ExpectedValue()) + atk.StrengthBonus;
            var required = AttackResolver.WoundTarget(Math.Max(1, strength), toughness);
            var woundMod = Math.Clamp(atk.WoundRollModifier + (atk.OathOfMoment ? 1 : 0) - (def.MinusOneToBeWounded ? 1 : 0), -1, 1);
            var critWoundThreshold = atk.CritWoundThreshold;
            if (atk.AntiOverrideThreshold is { } o)
                critWoundThreshold = Math.Min(critWoundThreshold, o);
            foreach (var anti in weapon.Abilities.OfType<Anti>())
                if (config.TargetKeywords.Any(k => string.Equals(k, anti.Keyword, StringComparison.OrdinalIgnoreCase)))
                    critWoundThreshold = Math.Min(critWoundThreshold, anti.CritThreshold);

            // Lethal hits auto-wound; only the non-lethal hits roll to wound.
            var lethal = atk.LethalHits || weapon.Has<LethalHits>();
            var lethalWounds = lethal ? critHits : 0;
            var rollingHits = hits - lethalWounds;
            var pWound = SuccessProbability(required, woundMod, critWoundThreshold);
            var wounds = rollingHits * pWound + lethalWounds;
            totalWounds += wounds;

            // ---- Save ----
            var baseSave = def.SaveOverride ?? profile.Save;
            var armour = Math.Clamp(baseSave - (weapon.ArmourPenetration + atk.ApModifier) - def.SaveModifier, 2, 7);
            var invuln = def.InvulnSaveOverride ?? profile.InvulnSave;
            var saveTarget = invuln is not null && invuln.Value < armour ? invuln.Value : armour;
            var pSavePass = SaveProbability(saveTarget);
            var unsaved = wounds * (1 - pSavePass);
            totalUnsaved += unsaved;

            // ---- Damage (with reduction + FNP means) ----
            var dmgPer = weapon.Damage.ExpectedValue() + atk.DamageBonus;
            if (atk.TargetWithinHalfRange && weapon.Get<Melta>() is { } melta)
                dmgPer += melta.X;
            dmgPer = ApplyReductionMean(dmgPer, def, profile);
            var fnp = def.FeelNoPainOverride ?? profile.FeelNoPain;
            if (fnp is not null && !((def.FnpMortalOnly || profile.FnpMortalOnly)))
                dmgPer *= FnpThroughFraction(fnp.Value);
            totalDamage += unsaved * dmgPer;
        }

        var perModelWounds = Math.Max(1, def.WoundsPerModelOverride ?? profile.Wounds);
        var modelsSlain = Math.Min(models, totalDamage / perModelWounds);

        return new ExpectedValueSummary
        {
            Attacks = totalAttacks,
            Hits = totalHits,
            Wounds = totalWounds,
            UnsavedWounds = totalUnsaved,
            Damage = totalDamage,
            ModelsSlain = modelsSlain,
        };
    }

    private static double SaveProbability(int target)
    {
        if (target >= 7)
            return 0; // unsaveable
        var pass = 0;
        for (var roll = 1; roll <= 6; roll++)
            if (roll != 1 && roll >= target)
                pass++;
        return pass / 6.0;
    }

    private static double ApplyReductionMean(double damage, DefenderModifiers def, CombatModelProfile profile)
    {
        var flat = def.DamageReductionFlat + profile.DamageReductionFlat;
        var d = damage - flat;
        if (def.DamageHalved || profile.DamageHalved)
            d = Math.Ceiling(d / 2.0);
        return Math.Max(1, d);
    }

    // Mean fraction of damage points that get through an FNP X+ (each point independently survives with (X-1)/6).
    private static double FnpThroughFraction(int fnp) => Math.Clamp((fnp - 1) / 6.0, 0, 1);
}
