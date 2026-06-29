using Warhammer40k._11.Features.CombatSimulator.Domain;

namespace Warhammer40k._11.Features.CombatSimulator.Engine;

/// <summary>
/// Resolves <b>one</b> combat exchange iteration through the 11th-edition sequence (§8): gather attacks →
/// hit → wound → save → damage/FNP, with the two independent hit-modifier buckets (§8.0), the S/T table,
/// Lethal Hits / Sustained Hits / Devastating Wounds / Anti-X, best-of armour-vs-invuln saves, damage
/// reduction, and sequential allocation so excess damage is lost per model. Pure and allocation-light: the
/// per-iteration model wound-pool is the only buffer, reused across iterations via <see cref="Reset"/>.
/// Part of the removable Combat Simulator feature — see <c>Features/CombatSimulator/DELETE.md</c>.
/// </summary>
public sealed class AttackResolver
{
    private readonly SimulationConfig _config;
    private readonly DiceRoller _rng;

    // The defender's model wound-pool, expanded to one entry per model (wounds remaining). Sequential
    // allocation against this list is what makes "excess damage lost per model" correct. Rebuilt each iteration.
    private readonly List<int> _woundPool = [];
    private readonly int _modelWoundsTemplateCount;
    private readonly int[] _modelWoundsTemplate;
    private readonly int _toughness;
    private readonly int _totalWounds;
    private readonly int _totalModels;

    public AttackResolver(SimulationConfig config, DiceRoller rng)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));

        // Build a per-model wounds template (one int per model) once, so each iteration just copies it.
        var def = _config.Defender;
        var template = new List<int>();
        foreach (var group in _config.Target.ModelGroups)
        {
            var perModel = Math.Max(1, def.WoundsPerModelOverride ?? group.Profile.Wounds);
            var count = def.ModelCountOverride is { } mc && _config.Target.ModelGroups.Count == 1 ? mc : group.Count;
            for (var i = 0; i < count; i++)
                template.Add(perModel);
        }
        if (template.Count == 0)
            template.Add(1);

        _modelWoundsTemplate = [.. template];
        _modelWoundsTemplateCount = _modelWoundsTemplate.Length;
        _totalModels = _modelWoundsTemplateCount;
        _totalWounds = template.Sum();

        // Toughness: explicit override, else attached-unit bodyguard T, else the first group's profile T.
        _toughness = def.ToughnessOverride
            ?? _config.Target.BodyguardToughness
            ?? (_config.Target.ModelGroups.FirstOrDefault()?.Profile.Toughness ?? 4);
    }

    /// <summary>The defender's total models (after any override) — for the models-slain distribution size.</summary>
    public int TargetModelCount => _totalModels;

    /// <summary>Runs one full exchange, writing results into <paramref name="tally"/> (which is reset first).</summary>
    public void ResolveInto(IterationTally tally)
    {
        tally.Reset();

        // Fresh wound pool for this iteration.
        _woundPool.Clear();
        for (var i = 0; i < _modelWoundsTemplateCount; i++)
            _woundPool.Add(_modelWoundsTemplate[i]);

        foreach (var weapon in _config.Weapons)
            ResolveWeapon(weapon, tally);

        // Effective damage is capped at the unit's starting total wounds.
        tally.EffectiveDamage = Math.Min(tally.DamageDealt, _totalWounds);
        tally.ModelsSlain = CountSlain();
    }

    private void ResolveWeapon(CombatWeapon weapon, IterationTally tally)
    {
        var atk = _config.Attacker;
        var firingModels = Math.Max(0, weapon.CarriedByModels);
        var torrent = weapon.Has<Torrent>();

        for (var model = 0; model < firingModels; model++)
        {
            // ---- 8.1 Gather attacks (per firing model) ----
            var attacks = weapon.Attacks.Roll(_rng) + atk.AttacksBonus;

            if (atk.TargetWithinHalfRange && weapon.Get<RapidFire>() is { } rf)
                attacks += rf.X;
            if (weapon.Has<Blast>())
                attacks += BlastBonus();

            attacks = Math.Max(0, attacks);
            tally.Attacks += attacks;

            // Resolve each attack die.
            for (var a = 0; a < attacks; a++)
            {
                if (torrent)
                {
                    tally.Hits++;
                    DoWoundStep(weapon, tally, isLethalAutoWound: false);
                    continue;
                }

                ResolveHit(weapon, tally);
            }
        }
    }

    // ---- 8.2 Hit roll ----
    private void ResolveHit(CombatWeapon weapon, IterationTally tally)
    {
        var atk = _config.Attacker;
        var def = _config.Defender;
        var psychic = weapon.Has<Psychic>();

        // BS/WS-characteristic bucket (uncapped). Cover worsens BS by 1 (11th). Stealth handled in hit-roll bucket.
        var bsBucket = atk.BsWsModifier;
        if (def.Cover && !(atk.IgnoresCover || weapon.Has<IgnoresCover>()))
            bsBucket -= 1; // worsens the attacker → applied as +1 to the target number below

        // Hit-roll bucket (±1 die), clamped net to [-1, +1].
        var hitRollMods = atk.HitRollModifier;
        if (def.MinusOneToBeHit)
            hitRollMods -= 1;

        if (psychic)
        {
            bsBucket = 0;
            hitRollMods = 0;
        }

        var effectiveTarget = weapon.Skill - bsBucket; // bsBucket negative (cover) raises the target
        var clampedHitMod = Math.Clamp(hitRollMods, -1, 1);
        var rerollHits = atk.RerollHits;
        if (atk.OathOfMoment && rerollHits == RerollPolicy.None)
            rerollHits = RerollPolicy.Failures;

        var natural = RollWithReroll(rerollHits, n => IsHit(n, clampedHitMod, effectiveTarget, atk.CritHitThreshold));

        var crit = natural >= atk.CritHitThreshold;
        var hit = natural != 1 && (crit || natural + clampedHitMod >= effectiveTarget);
        if (!hit)
            return;

        tally.Hits++;
        if (crit)
        {
            tally.CriticalHits++;

            // Sustained Hits X → X extra normal hits (go to wound step; not crits themselves).
            var sustained = atk.SustainedHits + (weapon.Get<SustainedHits>()?.X ?? 0);
            for (var s = 0; s < sustained; s++)
            {
                tally.Hits++;
                tally.SustainedExtraHits++;
                DoWoundStep(weapon, tally, isLethalAutoWound: false);
            }

            // Lethal Hits → auto-wound (skip wound roll; a normal wound, NOT a crit → no Dev Wounds).
            if (atk.LethalHits || weapon.Has<LethalHits>())
            {
                tally.LethalAutoWounds++;
                DoSaveStep(weapon, tally, fromInvulnAllowed: true, isMortal: false);
                return;
            }
        }

        DoWoundStep(weapon, tally, isLethalAutoWound: false);
    }

    private static bool IsHit(int natural, int hitMod, int target, int critThreshold) =>
        natural != 1 && (natural >= critThreshold || natural + hitMod >= target);

    // ---- 8.3 Wound roll ----
    private void DoWoundStep(CombatWeapon weapon, IterationTally tally, bool isLethalAutoWound)
    {
        var atk = _config.Attacker;
        var def = _config.Defender;

        var strength = Math.Max(1, weapon.Strength.Roll(_rng) + atk.StrengthBonus);
        var required = WoundTarget(strength, _toughness);

        var woundMods = atk.WoundRollModifier;
        if (atk.OathOfMoment)
            woundMods += 1; // simplified: Oath grants +1 to wound when active
        if (def.MinusOneToBeWounded)
            woundMods -= 1;
        var clampedWoundMod = Math.Clamp(woundMods, -1, 1);

        var critThreshold = CriticalWoundThreshold(weapon);

        var rerollWounds = atk.RerollWounds;
        if (weapon.Has<TwinLinked>() && rerollWounds == RerollPolicy.None)
            rerollWounds = RerollPolicy.Failures;

        var natural = RollWithReroll(rerollWounds, n => IsWound(n, clampedWoundMod, required, critThreshold));

        var crit = natural >= critThreshold;
        var wound = natural != 1 && (crit || natural + clampedWoundMod >= required);
        if (!wound)
            return;

        tally.Wounds++;
        if (crit)
        {
            tally.CriticalWounds++;
            if (atk.DevastatingWounds || weapon.Has<DevastatingWounds>())
            {
                // Devastating Wounds: generate mortal wounds = Damage, bypassing saves (one model max per crit).
                var mortal = Math.Max(1, weapon.Damage.Roll(_rng) + atk.DamageBonus);
                tally.DevastatingMortalWounds += mortal;
                AllocateMortal(weapon, tally, mortal);
                return;
            }
        }

        DoSaveStep(weapon, tally, fromInvulnAllowed: true, isMortal: false);
    }

    private static bool IsWound(int natural, int woundMod, int required, int critThreshold) =>
        natural != 1 && (natural >= critThreshold || natural + woundMod >= required);

    /// <summary>The 11th-edition S-vs-T wound table.</summary>
    public static int WoundTarget(int strength, int toughness)
    {
        if (strength >= 2 * toughness) return 2;
        if (strength > toughness) return 3;
        if (strength == toughness) return 4;
        if (toughness >= 2 * strength) return 6;
        return 5; // T > S but < 2S
    }

    private int CriticalWoundThreshold(CombatWeapon weapon)
    {
        var atk = _config.Attacker;
        var threshold = atk.CritWoundThreshold;
        if (atk.AntiOverrideThreshold is { } o)
            threshold = Math.Min(threshold, o);
        foreach (var anti in weapon.Abilities.OfType<Anti>())
            if (TargetHasKeyword(anti.Keyword))
                threshold = Math.Min(threshold, anti.CritThreshold);
        return threshold;
    }

    private bool TargetHasKeyword(string keyword) =>
        _config.TargetKeywords.Any(k => string.Equals(k, keyword, StringComparison.OrdinalIgnoreCase));

    // ---- 8.4 Saving throw ----
    private void DoSaveStep(CombatWeapon weapon, IterationTally tally, bool fromInvulnAllowed, bool isMortal)
    {
        var def = _config.Defender;
        var profile = _config.Target.ModelGroups.FirstOrDefault()?.Profile ?? new CombatModelProfile();

        var baseSave = def.SaveOverride ?? profile.Save;
        // AP is non-positive → subtracting raises the number. SaveModifier: +1 improves (lowers).
        var armourRequired = baseSave - (weapon.ArmourPenetration + _config.Attacker.ApModifier) - def.SaveModifier;
        armourRequired = Math.Clamp(armourRequired, 2, 7);

        var invuln = def.InvulnSaveOverride ?? profile.InvulnSave;
        var usingInvuln = fromInvulnAllowed && invuln is not null && invuln.Value < armourRequired;
        var required = usingInvuln ? invuln!.Value : armourRequired;

        var roll = _rng.D6();
        var saved = roll != 1 && roll >= required;
        if (saved)
        {
            tally.WoundsSaved++;
            return;
        }

        if (usingInvuln) tally.FailedInvulnSaves++;
        else tally.FailedArmourSaves++;

        // Failed save → damage goes through.
        var damage = Math.Max(1, weapon.Damage.Roll(_rng) + _config.Attacker.DamageBonus);
        if (_config.Attacker.TargetWithinHalfRange && weapon.Get<Melta>() is { } melta)
            damage += melta.X;

        ApplyDamageToNextModel(weapon, tally, damage, isMortal: false);
    }

    // ---- 8.5 Damage, reduction, FNP, allocation ----
    private void ApplyDamageToNextModel(CombatWeapon weapon, IterationTally tally, int rawDamage, bool isMortal)
    {
        var def = _config.Defender;
        var profile = _config.Target.ModelGroups.FirstOrDefault()?.Profile ?? new CombatModelProfile();

        // Damage reduction: flat first, then halve (round up), then floor at 1.
        var reduceFlat = def.DamageReductionFlat + profile.DamageReductionFlat;
        var halved = def.DamageHalved || profile.DamageHalved;

        var dmg = rawDamage - reduceFlat;
        if (halved)
            dmg = (int)Math.Ceiling(dmg / 2.0);
        dmg = Math.Max(1, dmg);

        // FNP point-by-point.
        dmg = ApplyFnp(tally, dmg, isMortal);
        if (dmg <= 0)
            return;

        tally.DamageDealt += dmg;
        AllocateToModel(dmg);
    }

    private void AllocateMortal(CombatWeapon weapon, IterationTally tally, int mortal)
    {
        // Mortal wounds bypass saves and damage reduction; FNP still applies. One model max per crit: any
        // leftover beyond the allocated model's wounds is lost because AllocateToModel never carries over.
        var surviving = ApplyFnp(tally, mortal, isMortal: true);
        if (surviving <= 0)
            return;
        tally.DamageDealt += surviving;
        AllocateToModel(surviving);
    }

    private int ApplyFnp(IterationTally tally, int damage, bool isMortal)
    {
        var def = _config.Defender;
        var profile = _config.Target.ModelGroups.FirstOrDefault()?.Profile ?? new CombatModelProfile();
        var fnp = def.FeelNoPainOverride ?? profile.FeelNoPain;
        if (fnp is null)
            return damage;

        var mortalOnly = def.FnpMortalOnly || profile.FnpMortalOnly;
        if (mortalOnly && !isMortal)
            return damage;

        var remaining = 0;
        for (var i = 0; i < damage; i++)
        {
            if (_rng.D6() >= fnp.Value)
                tally.FnpIgnored++;
            else
                remaining++;
        }
        return remaining;
    }

    // Allocate to the current wounded-or-next model; excess beyond the model's wounds is lost.
    private void AllocateToModel(int damage)
    {
        var idx = CurrentModelIndex();
        if (idx < 0)
            return;
        _woundPool[idx] = Math.Max(0, _woundPool[idx] - damage); // excess lost (no carry-over)
    }

    private int CurrentModelIndex()
    {
        // First partially-damaged model, else first alive model (wound allocation stays on a model until it dies).
        var firstAlive = -1;
        for (var i = 0; i < _woundPool.Count; i++)
        {
            if (_woundPool[i] <= 0)
                continue;
            if (firstAlive < 0)
                firstAlive = i;
            if (_woundPool[i] < _modelWoundsTemplate[i])
                return i; // already wounded → keep allocating here
        }
        return firstAlive;
    }

    private int CountSlain()
    {
        var slain = 0;
        for (var i = 0; i < _woundPool.Count; i++)
            if (_woundPool[i] <= 0)
                slain++;
        return slain;
    }

    private int BlastBonus()
    {
        var models = _config.Defender.ModelCountOverride ?? _totalModels;
        return models / 5;
    }

    // Roll a d6, applying the re-roll policy once when the first die "fails" the supplied success predicate.
    private int RollWithReroll(RerollPolicy policy, Func<int, bool> isSuccess)
    {
        var first = _rng.D6();
        var reroll = policy switch
        {
            RerollPolicy.Ones => first == 1,
            RerollPolicy.Failures => !isSuccess(first),
            _ => false,
        };
        return reroll ? _rng.D6() : first;
    }
}
