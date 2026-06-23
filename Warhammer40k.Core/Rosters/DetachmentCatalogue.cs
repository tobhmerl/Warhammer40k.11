using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Text;

namespace Warhammer40k.Core.Rosters;

/// <summary>
/// The built-in set of the seven Necron detachments (§2) with their known 10th-MFM enhancement points (§8).
/// </summary>
/// <remarks>
/// The validation machinery (R6 enhancement eligibility, stratagem reference) is finalized; what's still
/// missing is <i>content</i> — the 11th-edition enhancement points/eligibility and stratagems. The three
/// detachments without published points (Hand of the Dynasty, Skyshroud Spearhead, The Phaeron's Armoury)
/// keep empty enhancement lists, so R6 stays permissive for them until those entries are filled in here.
/// Per-enhancement <c>Eligibility</c> and per-detachment <c>Stratagems</c> are empty pending §10/§11 — add
/// keyword constraints / stratagem entries below to activate them (no engine change required).
/// </remarks>
public static class DetachmentCatalogue
{
    /// <summary>The seven detachments offered by the New-Roster wizard (§2), in line-up order.</summary>
    public static IReadOnlyList<Detachment> BuiltIn { get; } =
    [
        HandOfTheDynasty(),
        Make("Skyshroud Spearhead"),
        Make("The Phaeron's Armoury"),
        Make("Starshatter Arsenal",
            ("Chrono-impedance Fields", 25),
            ("Demanding Leader", 10),
            ("Dread Majesty", 30),
            ("Miniaturised Nebuloscope", 15)),
        Cryptek(),
        Make("Cursed Legion",
            ("Cursed Circlet", 25),
            ("Destroyer Ankh", 20),
            ("Mark of the Nekrosor", 20),
            ("Murdermind", 15)),
        MakePantheon("Pantheon of Woe"),
    ];

    /// <summary>Finds a built-in detachment by its derived id, or <c>null</c>.</summary>
    public static Detachment? FindById(string id) =>
        BuiltIn.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The detachments currently offered in setup — only those with authored 11th-edition rules.</summary>
    public static IReadOnlyList<Detachment> Selectable { get; } =
        BuiltIn.Where(d => d.Enabled).ToList();

    /// <summary>Detachment Points available at a points level (11th edition): 1000 or under = 2, otherwise 3.</summary>
    public static int Budget(int pointsLimit) => pointsLimit <= 1000 ? 2 : 3;

    // Hand of the Dynasty (1 DP, DYNASTY) — "Hypermotility Protocols".
    private static Detachment HandOfTheDynasty()
    {
        var d = Make("Hand of the Dynasty");
        d.Enabled = true;
        d.DetachmentPoints = 1;
        d.Tags = ["Dynasty"];

        // 11th-edition Upgrades: assigned to a whole non-Character unit (EnhancementScope.Unit) rather than a
        // Character. R6 + the configurator gate them by keyword eligibility; their text shows in Play Mode.
        d.Enhancements =
        [
            new Enhancement
            {
                Id = "enlivened-sentinels", Name = "Enlivened Sentinels", Points = 20,
                Scope = EnhancementScope.Unit,
                Eligibility = new EnhancementEligibility { RequiredKeywords = ["Necron Warriors"] },
                Text = "NECRON WARRIORS unit only. This unit has Scouts 5\".",
            },
            new Enhancement
            {
                Id = "tools-of-dominion", Name = "Tools of Dominion", Points = 15,
                Scope = EnhancementScope.Unit,
                Eligibility = new EnhancementEligibility { RequiredKeywords = ["Immortals"] },
                Text = "IMMORTALS unit only. This unit's ranged attacks have [RAPID FIRE 1].",
            },
        ];
        d.Rules =
        [
            new DetachmentRule
            {
                Name = "Hypermotility Protocols",
                Text =
                    "Friendly IMMORTALS/NECRON WARRIORS units' ranged attacks have the [ASSAULT] ability.\n" +
                    "When a friendly IMMORTALS/NECRON WARRIORS unit is selected to make an Advance move, that " +
                    "move does not prevent the unit from being eligible to start an action.\n" +
                    "This detachment has the DYNASTY tag and cannot be taken with another DYNASTY detachment.",
            },
        ];
        d.WeaponGrants =
        [
            new WeaponAbilityGrant
            {
                Keywords = ["Immortals", "Necron Warriors"],
                Scope = GrantScope.Unit,
                WeaponClass = DetachmentWeaponClass.Ranged,
                Abilities = ["Assault"],
            },
        ];
        d.Stratagems =
        [
            new Stratagem
            {
                Id = "dominance-protocols", Name = "Dominance Protocols", CpCost = 1,
                Turn = StratagemTurn.Either, Phases = [BattlePhase.Command],
                RequiredUnitKeywords = ["Immortals"],
                When = "Command phase.",
                Target = "One friendly IMMORTALS unit.",
                Effect = "Your unit has +1 OC until the end of the turn.",
            },
            new Stratagem
            {
                Id = "will-of-the-conqueror", Name = "Will of the Conqueror", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Movement],
                RequiredUnitKeywords = ["Immortals", "Necron Warriors"],
                When = "End of your Movement phase.",
                Target = "One friendly IMMORTALS/NECRON WARRIORS unit.",
                Effect = "Select one objective your unit is controlling. That objective is secured.",
            },
            new Stratagem
            {
                Id = "nanosaturation", Name = "Nanosaturation", CpCost = 1,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Shooting],
                RequiredUnitKeywords = ["Immortals", "Necron Warriors"],
                When = "Your opponent's Shooting phase, when an enemy unit that targeted a friendly IMMORTALS/NECRON WARRIORS unit has shot.",
                Target = "That IMMORTALS/NECRON WARRIORS unit.",
                Effect = "Your unit shoots using snap shooting, but while doing so your unit can only target that enemy unit.",
            },
        ];
        return d;
    }

    // Cryptek Conclave (2 DP) — "Technosorcerous Augmentations" expressed as data-driven smart effects.
    private static Detachment Cryptek()
    {
        var d = Make("Cryptek Conclave",
            ("Atomic Disintegrators", 10),
            ("Gauntlet of Compression", 20),
            ("Gravitic Bolas", 15),
            ("Quantum Abacus", 15));
        d.Enabled = true;
        d.DetachmentPoints = 2;

        // Enhancement display text (shown on the bearer's card in Play Mode) + per-enhancement eligibility (R6).
        // "CRYPTEK model only" requires the Cryptek keyword; "NECRONS model only" is unconstrained (R6 still
        // requires the bearer to be a Character that can take Enhancements).
        Author(d, "atomic-disintegrators",
            "CRYPTEK model only. In your Shooting phase, each time the bearer's unit is selected to shoot, when " +
            "selecting an ability for the Technosorcerous Augmentations Detachment rule, you can also select from " +
            "the following abilities: [ANTI-MONSTER 5+], [ANTI-VEHICLE 5+].",
            "Cryptek");
        Author(d, "gauntlet-of-compression",
            "NECRONS model only. Add 6\" to the Range characteristic of ranged weapons equipped by models in the " +
            "bearer's unit.");
        Author(d, "gravitic-bolas",
            "CRYPTEK model only. In your Shooting phase, after the bearer has shot, select one enemy unit hit by " +
            "one or more of those attacks (excluding TITANIC units); until the start of your next turn, that enemy " +
            "unit is pinned. While a unit is pinned, subtract 2 from that unit's Move characteristic and subtract 2 " +
            "from Charge rolls made for that unit.",
            "Cryptek");
        Author(d, "quantum-abacus",
            "NECRONS model only. Each time you select the bearer's unit as the target of a Stratagem, roll one D6, " +
            "adding 1 if it is within range of one or more objectives: on a 4+, you gain 1CP.");

        // Gauntlet of Compression is a live, unit-wide buff: +6" Range on every ranged weapon in the bearer's
        // unit. Play Mode applies it to the whole combat group and shows the increased Rng on each weapon.
        var gauntlet = d.FindEnhancement("gauntlet-of-compression")!;
        gauntlet.AffectsWholeUnit = true;
        gauntlet.StatModifiers =
        [
            new StatModifier { Target = StatTarget.Range, Delta = 6, WeaponClass = WeaponClass.Ranged, Label = "+6\" Range" },
        ];

        // Atomic Disintegrators adds two more selectable shooting abilities (Technosorcerous Augmentations) to
        // the bearer's CRYPTEK unit, on top of the detachment's standard five.
        d.FindEnhancement("atomic-disintegrators")!.ShootingAbilityOptions = ["Anti-MONSTER 5+", "Anti-VEHICLE 5+"];

        d.Rules =
        [
            new DetachmentRule
            {
                Name = "Technosorcerous Augmentations",
                Text =
                    "Ranged weapons equipped by CRYPTEK models from your army have the [ASSAULT] ability.\n" +
                    "In your Shooting phase, each time a CRYPTEK unit from your army is selected to shoot, select " +
                    "one of the following abilities: [ANTI-INFANTRY 3+], [ANTI-MOUNTED 4+], [ASSAULT], [HEAVY], " +
                    "[IGNORES COVER]. Until the end of the phase, ranged weapons equipped by models in that unit " +
                    "have that ability.",
            },
        ];
        d.WeaponGrants =
        [
            new WeaponAbilityGrant
            {
                Keywords = ["Cryptek"],
                Scope = GrantScope.Model,
                WeaponClass = DetachmentWeaponClass.Ranged,
                Abilities = ["Assault"],
            },
        ];
        d.WeaponChoices =
        [
            new WeaponAbilityChoice
            {
                Name = "Technosorcerous Augmentations",
                RequiresModelKeyword = "Cryptek",
                WeaponClass = DetachmentWeaponClass.Ranged,
                Options = ["Anti-INFANTRY 3+", "Anti-MOUNTED 4+", "Assault", "Heavy", "Ignores Cover"],
            },
        ];
        d.Stratagems =
        [
            new Stratagem
            {
                Id = "molecular-targeting", Name = "Molecular Targeting", Type = "Battle Tactic", CpCost = 1,
                Turn = StratagemTurn.Either, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                When = "Your Shooting phase or the Fight phase.",
                Target = "One NECRONS unit from your army that has not been selected to shoot or fight this phase.",
                Effect = "Until the end of the phase, each time a model in your unit makes an attack, you can ignore any or all modifiers to the following: that attack's Ballistic Skill or Weapon Skill characteristic; the Hit roll. If your unit has the CRYPTEK keyword, you can also ignore any or all modifiers to the Wound roll.",
            },
            new Stratagem
            {
                Id = "microscarab-swarm", Name = "Microscarab Swarm", Type = "Wargear", CpCost = 1,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                RequiredUnitKeywords = ["Cryptek"],
                When = "Your opponent's Shooting phase or the Fight phase, just after an enemy unit has selected its targets.",
                Target = "One CRYPTEK INFANTRY unit from your army that was selected as the target of one or more of the attacking unit's attacks.",
                Effect = "If your unit has the NECRON WARRIORS keyword, until the end of the phase, models in your unit have a 5+ invulnerable save. If your unit has the IMMORTALS keyword, until the end of the phase, models in your unit have a 4+ invulnerable save.",
            },
            new Stratagem
            {
                Id = "animus-curse", Name = "Animus Curse", Type = "Wargear", CpCost = 1,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                RequiredUnitKeywords = ["Cryptek"],
                When = "Your opponent's Shooting phase or the Fight phase, just after an enemy unit has shot or fought.",
                Target = "One CRYPTEK model from your army that was destroyed by one of the attacking unit's attacks. You can use this Stratagem on that model even though it was just destroyed.",
                Effect = "Until the end of the battle, each time a friendly NECRONS model makes an attack that targets the attacking unit, you can re-roll the Hit roll.",
            },
            new Stratagem
            {
                Id = "synergistic-empowerment", Name = "Synergistic Empowerment", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Shooting],
                RequiredUnitKeywords = ["Cryptek"],
                When = "Start of your Shooting phase.",
                Target = "One CRYPTEK unit from your army.",
                Effect = "Select one friendly NECRONS model (excluding MONSTERS and VEHICLES) within 12\" of a CRYPTEK model in your unit. Until the end of the phase, that friendly NECRONS model has the CRYPTEK keyword.",
            },
            new Stratagem
            {
                Id = "untapped-power", Name = "Untapped Power", Type = "Battle Tactic", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Shooting],
                RequiredUnitKeywords = ["Cryptek"],
                When = "Your Shooting phase.",
                Target = "One CRYPTEK unit from your army that has not been selected to shoot this phase.",
                Effect = "Until the end of the phase, each time your unit is selected to shoot, when selecting an ability for the Technosorcerous Augmentations Detachment Rule, you can select one additional ability from those available.",
            },
            new Stratagem
            {
                Id = "potentiality-syphon", Name = "Potentiality Syphon", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Command],
                When = "Your opponent's Command phase.",
                Target = "One NECRONS unit from your army within range of one or more objective markers.",
                Effect = "Your unit's Reanimation Protocols activate. If it is a CRYPTEK unit, it reanimates an additional 1 wound.",
            },
        ];
        return d;
    }

    private static Detachment Make(string name, params (string Name, int Points)[] enhancements) => new()
    {
        Id = Slugger.Slug(name),
        Name = name,
        Enhancements = enhancements
            .Select(e => new Enhancement { Id = Slugger.Slug(e.Name), Name = e.Name, Points = e.Points })
            .ToList(),
    };

    /// <summary>
    /// Sets a built-in enhancement's Play-Mode display <see cref="Enhancement.Text"/> and, when keywords are
    /// supplied, its R6 eligibility (<see cref="EnhancementEligibility.RequiredKeywords"/>). No-op when the id
    /// is not found.
    /// </summary>
    private static void Author(Detachment d, string enhancementId, string text, params string[] requiredKeywords)
    {
        var enhancement = d.FindEnhancement(enhancementId);
        if (enhancement is null)
            return;
        enhancement.Text = text;
        if (requiredKeywords.Length > 0)
            enhancement.Eligibility.RequiredKeywords = [.. requiredKeywords];
    }

    private static Detachment MakePantheon(string name)
    {
        var d = Make(name);
        d.AppliesPantheonBindings = true;
        return d;
    }
}
