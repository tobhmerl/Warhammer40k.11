using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Text;

namespace Warhammer40k.Core.Rosters;

/// <summary>
/// The built-in set of Necron detachments (§2) with their known 11th-edition Detachment-Points cost and
/// enhancement points (§8).
/// </summary>
/// <remarks>
/// The validation machinery (R6 enhancement eligibility, stratagem reference) is finalized; what's still
/// missing for most detachments is <i>content</i> — the 11th-edition enhancement eligibility and stratagems.
/// Detachments authored only with points (Skyshroud Spearhead, The Phaeron's Armoury, Starshatter Arsenal,
/// Cursed Legion, Annihilation Legion, Awakened Dynasty, Canoptek Court, Hypercrypt Legion, Obeisance Phalanx)
/// stay <see cref="Detachment.Enabled"/> = false until their rules are filled in here; R6 stays permissive for
/// them meanwhile. Per-enhancement <c>Eligibility</c> and per-detachment <c>Stratagems</c> are empty pending
/// §10/§11 — add keyword constraints / stratagem entries below to activate them (no engine change required).
/// </remarks>
public static class DetachmentCatalogue
{
    /// <summary>The twelve detachments offered by the New-Roster wizard (§2), in line-up order.</summary>
    public static IReadOnlyList<Detachment> BuiltIn { get; } =
    [
        HandOfTheDynasty(),
        SkyshroudSpearhead(),
        PhaeronsArmoury(),
        StarshatterArsenal(),
        Cryptek(),
        CursedLegion(),
        PantheonOfWoe(),
        AnnihilationLegion(),
        AwakenedDynasty(),
        CanoptekCourt(),
        HypercryptLegion(),
        ObeisancePhalanx(),
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

    // Awakened Dynasty (3 DP, Take and Hold) — "Command Protocols".
    private static Detachment AwakenedDynasty()
    {
        // Enhancement points are authored; their text/eligibility and the stratagems follow (§10/§11).
        var d = Make("Awakened Dynasty", 3,
            ("Enaegic Dermal Bond", 30),
            ("Nether-realm Casket", 20),
            ("Phasal Subjugator", 35),
            ("Veil of Darkness", 20));
        d.Enabled = true;
        d.Tags = ["Dynasty"];

        d.Rules =
        [
            new DetachmentRule
            {
                Name = "Command Protocols",
                Text =
                    "While a NECRONS CHARACTER model is leading this unit, each time a model in this unit makes " +
                    "an attack, add 1 to the Hit roll.\n" +
                    "This detachment has the DYNASTY tag and cannot be taken with another DYNASTY detachment.",
            },
        ];

        // The +1 to Hit is data-driven: every friendly unit that currently has a Leader attached gets +1 to its
        // Hit roll (BS and WS), shown as a "(+1)" badge on the weapon's BS/WS cell in Play Mode. It is not baked
        // into the BS/WS characteristic (which the Cover rule can lower independently).
        d.StatBuffs =
        [
            new DetachmentStatBuff
            {
                Scope = GrantScope.Unit,
                RequiresAttachedLeader = true,
                Keywords = [],
                Modifier = new StatModifier
                {
                    Target = StatTarget.Skill,
                    Delta = 1,
                    WeaponClass = WeaponClass.Any,
                    Label = "+1 Hit",
                },
            },
        ];

        // Enhancement display text (bearer's Play card). All four are "NECRONS model only" — left R6-unconstrained
        // (every model that can bear an enhancement is already NECRONS). Phasal Subjugator is a proximity Aura, so
        // (like Starshatter's Dread Majesty) it stays reference text rather than a data-driven buff.
        Author(d, "enaegic-dermal-bond",
            "NECRONS model only. The bearer has the Feel No Pain 4+ ability.");
        Author(d, "nether-realm-casket",
            "NECRONS model only. While the bearer is leading a unit, models in that unit have the Stealth ability.");
        Author(d, "phasal-subjugator",
            "NECRONS model only. Aura. While a friendly NECRONS unit (excluding CHARACTER units) is within 6\" of " +
            "the bearer, each time a model in that unit makes an attack, add 1 to the Hit roll.");
        Author(d, "veil-of-darkness",
            "NECRONS model only. Once per battle, at the end of your opponent's turn, if the bearer's unit is not " +
            "within Engagement Range of one or more enemy units, the bearer can use this Enhancement. If it does, " +
            "remove that unit from the battlefield. Then, in the Reinforcements step of your next Movement phase, " +
            "set up that unit anywhere on the battlefield that is more than 9\" horizontally away from all enemy " +
            "models.");

        d.Stratagems =
        [
            new Stratagem
            {
                Id = "protocol-of-the-sudden-storm", Name = "Protocol of the Sudden Storm", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Movement],
                When = "Your Movement phase.",
                Target = "One NECRONS unit from your army.",
                Effect = "Until the end of the turn, ranged weapons equipped by models in your unit have the [ASSAULT] ability. In addition, if a NECRONS CHARACTER is leading your unit, until the end of the phase, you can re-roll Advance rolls made for your unit.",
            },
            new Stratagem
            {
                Id = "protocol-of-the-conquering-tyrant", Name = "Protocol of the Conquering Tyrant", Type = "Battle Tactic", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Shooting],
                When = "Your Shooting phase.",
                Target = "One NECRONS unit from your army that has not been selected to shoot this phase.",
                Effect = "Until the end of the phase, each time a model in your unit makes an attack that targets a unit within half range, re-roll a Hit roll of 1. If a NECRONS CHARACTER is leading your unit, until the end of the phase, you can re-roll the Hit roll for that attack instead.",
            },
            new Stratagem
            {
                Id = "protocol-of-the-vengeful-stars", Name = "Protocol of the Vengeful Stars", Type = "Strategic Ploy", CpCost = 2,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Shooting],
                RequiredUnitKeywords = ["Character"],
                When = "Your opponent's Shooting phase, just after an enemy unit destroys a NECRONS unit from your army.",
                Target = "One NECRONS CHARACTER unit from your army that was within 6\" of that NECRONS unit when it was destroyed.",
                Effect = "After the attacking unit has resolved its attacks, your unit can shoot as if it were your Shooting phase, but it must target only that enemy unit when doing so, and can only do so if that enemy unit is an eligible target.",
            },
            new Stratagem
            {
                Id = "protocol-of-the-eternal-revenant", Name = "Protocol of the Eternal Revenant", Type = "Epic Deed", CpCost = 1,
                Turn = StratagemTurn.Either, Phases = [],
                RequiredUnitKeywords = ["Character"],
                When = "Any phase.",
                Target = "One NECRONS INFANTRY CHARACTER model from your army that was just destroyed. You can use this Stratagem on that model even though it was just destroyed.",
                Effect = "At the end of the phase, set your model back up on the battlefield as close as possible to where it was destroyed and not within Engagement Range of any enemy units, with half of its starting number of wounds remaining. Each model can only be targeted with this Stratagem once per battle.",
            },
            new Stratagem
            {
                Id = "protocol-of-the-hungry-void", Name = "Protocol of the Hungry Void", Type = "Battle Tactic", CpCost = 1,
                Turn = StratagemTurn.Either, Phases = [BattlePhase.Fight],
                When = "Fight phase.",
                Target = "One NECRONS unit from your army that has not been selected to fight this phase.",
                Effect = "Until the end of the phase, add 1 to the Strength characteristic of melee weapons equipped by models in your unit. In addition, if a NECRONS CHARACTER is leading your unit, until the end of the phase, improve the Armour Penetration characteristic of melee weapons equipped by models in your unit by 1 (this is not cumulative with any other modifiers that improve Armour Penetration).",
            },
            new Stratagem
            {
                Id = "protocol-of-the-undying-legions", Name = "Protocol of the Undying Legions", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                When = "Your opponent's Shooting phase or the Fight phase, just after an enemy unit has resolved its attacks.",
                Target = "One NECRONS unit from your army that had one or more of its models destroyed as a result of the attacking unit's attacks.",
                Effect = "Your unit activates its Reanimation Protocols and reanimates D3 wounds (or D3+1 wounds if a NECRONS CHARACTER is leading your unit).",
            },
        ];

        return d;
    }

    // Annihilation Legion (2 DP) — "Annihilation Protocol". Charge re-rolls and the closest-target AP bonus are
    // board-state dependent, so the rule stays reference prose (no data-driven buff).
    private static Detachment AnnihilationLegion()
    {
        var d = Make("Annihilation Legion", 2,
            ("Eldritch Nightmare", 10),
            ("Eternal Madness", 20),
            ("Ingrained Superiority", 5),
            ("Soulless Reaper", 15));
        d.Enabled = true;
        d.Rules =
        [
            new DetachmentRule
            {
                Name = "Annihilation Protocol",
                Text =
                    "Each time a DESTROYER CULT or FLAYED ONES unit from your army declares a charge, you can " +
                    "re-roll the Charge roll. If one or more targets of that charge are Below Half-strength, add 1 " +
                    "to the Charge roll as well.\n" +
                    "Each time a DESTROYER CULT unit from your army makes a ranged attack that targets the closest " +
                    "eligible target, add 1 to the Armour Penetration characteristic of that attack.",
            },
        ];
        return d;
    }

    // Canoptek Court (3 DP) — "Power Matrix". Entirely board-state (objective control / zones), so prose only.
    private static Detachment CanoptekCourt()
    {
        var d = Make("Canoptek Court", 3,
            ("Autodivinator", 15),
            ("Dimensional Sanctum", 20),
            ("Hyperphasic Fulcrum", 15),
            ("Metalodermal Tesla Weave", 10));
        d.Enabled = true;
        d.Rules =
        [
            new DetachmentRule
            {
                Name = "Power Matrix",
                Text =
                    "Certain areas of the battlefield are within your army's Power Matrix:\n" +
                    "• Your Deployment zone is always within your Power Matrix.\n" +
                    "• At the start of any phase, if you control at least half of the objective markers within No " +
                    "Man's Land, until the end of that phase No Man's Land is within your Power Matrix.\n" +
                    "• At the start of any phase, if you control at least half of the objective markers within your " +
                    "opponent's Deployment zone, until the end of that phase that zone is within your Power Matrix.\n" +
                    "Each time a model in a CRYPTEK or CANOPTEK unit from your army makes an attack, re-roll a Hit " +
                    "roll of 1. If such a unit is wholly within your Power Matrix, you can re-roll the Hit roll instead.",
            },
        ];
        return d;
    }

    // Cursed Legion (2 DP) — "Cold Fervour". The always-on +2 Strength for DESTROYER CULT weapons is wired as a
    // data-driven buff; the "first kill each turn" army-wide boost is board-state, so it stays prose.
    private static Detachment CursedLegion()
    {
        var d = Make("Cursed Legion", 2,
            ("Cursed Circlet", 25),
            ("Destroyer Ankh", 20),
            ("Mark of the Nekrosor", 20),
            ("Murdermind", 15));
        d.Enabled = true;
        d.Rules =
        [
            new DetachmentRule
            {
                Name = "Cold Fervour",
                Text =
                    "Add 2 to the Strength characteristic of weapons equipped by DESTROYER CULT models from your army.\n" +
                    "The first time each turn that a DESTROYER CULT unit from your army makes attacks that destroy a " +
                    "unit or cause it to become Below Half-strength, after that unit has finished resolving its " +
                    "attacks, until the end of the turn add 2 to the Strength characteristic of weapons equipped by " +
                    "friendly NECRONS models (excluding DESTROYER CULT, MONSTER and TITANIC models).",
            },
        ];
        // Always-on half: DESTROYER CULT models' weapons get +2 Strength (both ranged and melee).
        d.StatBuffs =
        [
            new DetachmentStatBuff
            {
                Scope = GrantScope.Model,
                Keywords = ["Destroyer Cult"],
                Modifier = new StatModifier
                {
                    Target = StatTarget.Strength,
                    Delta = 2,
                    WeaponClass = WeaponClass.Any,
                    Label = "+2 Str",
                },
            },
        ];

        // Enhancement display text (bearer's Play card). R6-unconstrained — several confer the DESTROYER CULT
        // keyword or reposition the bearer, so they are not tied to a fixed model keyword.
        Author(d, "cursed-circlet",
            "Each time an enemy unit is selected to shoot, after that unit has shot, if any models from the " +
            "bearer's unit were destroyed as a result of those attacks, the bearer's unit can make a Surge move. " +
            "Roll one D6: the unit can be moved up to that many inches but must finish as close as possible to the " +
            "closest enemy unit (excluding AIRCRAFT), and can move within Engagement Range of it. A unit cannot " +
            "make a Surge move while it is Battle-shocked.");
        Author(d, "mark-of-the-nekrosor",
            "Each time a model in the bearer's unit makes an attack, add 1 to the Hit roll.");
        Author(d, "destroyer-ankh",
            "The bearer has the DESTROYER CULT keyword. Add 2\" to the Move characteristic of models in the " +
            "bearer's unit and add 2 to the Attacks characteristic of melee weapons equipped by the bearer.");
        Author(d, "murdermind",
            "The bearer has the DESTROYER CULT keyword and, during the Declare Battle Formations step, can be " +
            "attached to a DESTROYER CULT unit (excluding CHARACTER units). If you do, the bearer's unit cannot " +
            "contain any models without the DESTROYER CULT keyword. Add 3\" to the Move characteristic of the bearer.");

        d.Stratagems =
        [
            new Stratagem
            {
                Id = "spreading-madness", Name = "Spreading Madness", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Charge],
                When = "Your Charge phase.",
                Target = "One NECRONS unit (excluding MONSTER and VEHICLE units) from your army that has not declared a charge this phase.",
                Effect = "Until the end of the phase, each time your unit declares a charge, if one or more targets of that charge are within Engagement Range of one or more friendly units, add 2 to the Charge roll.",
            },
            new Stratagem
            {
                Id = "driven-to-butchery", Name = "Driven to Butchery", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Shooting, BattlePhase.Charge],
                RequiredUnitKeywords = ["Destroyer Cult"],
                When = "Your Shooting phase or your Charge phase.",
                Target = "One DESTROYER CULT unit from your army.",
                Effect = "Until the end of the turn, your unit is eligible to shoot and declare a charge in a turn in which it Advanced. You can only use this Stratagem once per turn.",
            },
            new Stratagem
            {
                Id = "methodical-murder", Name = "Methodical Murder", Type = "Battle Tactic", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                When = "Your Shooting phase or the Fight phase.",
                Target = "One NECRONS unit (excluding MONSTER and VEHICLE units) from your army that has not been selected to shoot or fight this phase.",
                Effect = "Until the end of the phase, weapons equipped by models in your unit have the [SUSTAINED HITS 1] ability.",
            },
            new Stratagem
            {
                Id = "mortis-protocols", Name = "Mortis Protocols", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                RequiredUnitKeywords = ["Destroyer Cult"],
                When = "Your Shooting phase or the Fight phase, just after the first time a DESTROYER CULT unit from your army destroys an enemy unit this turn.",
                Target = "One friendly NECRONS unit (excluding MONSTER and VEHICLE units) within 9\" of that DESTROYER CULT unit.",
                Effect = "The friendly unit's Reanimation Protocols activate.",
            },
            new Stratagem
            {
                Id = "unnatural-aggression", Name = "Unnatural Aggression", Type = "Strategic Ploy", CpCost = 2,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Charge],
                When = "End of your opponent's Charge phase.",
                Target = "One NECRONS unit (excluding MONSTER and VEHICLE units) from your army that is within 6\" of one or more enemy units and would be eligible to declare a charge against one or more of those enemy units if it were your Charge phase.",
                Effect = "Your unit now declares a charge that only targets one or more of those enemy units, and you resolve that charge. Even if this charge is successful, your unit does not receive any Charge bonus this turn.",
            },
            new Stratagem
            {
                Id = "image-of-death", Name = "Image of Death", Type = "Battle Tactic", CpCost = 1,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                RequiredUnitKeywords = ["Destroyer Cult"],
                When = "Your opponent's Shooting phase or the Fight phase, just after an enemy unit has selected its targets.",
                Target = "One DESTROYER CULT unit from your army that was selected as the target of one or more of the attacking unit's attacks.",
                Effect = "Until the end of the phase, each time an attack targets your unit, subtract 1 from the Hit roll.",
            },
        ];

        return d;
    }

    // Hypercrypt Legion (2 DP, HYPERCRYPT) — "Hyperphasing". Redeploy into Strategic Reserves is a manual action,
    // so prose only. Tagged HYPERCRYPT: mutually exclusive with The Phaeron's Armoury.
    private static Detachment HypercryptLegion()
    {
        var d = Make("Hypercrypt Legion", 2,
            ("Arisen Tyrant", 25),
            ("Dimensional Overseer", 25),
            ("Hyperspatial Transfer Node", 15),
            ("Osteoclave Fulcrum", 20));
        d.Enabled = true;
        d.Tags = ["Hypercrypt"];
        d.Rules =
        [
            new DetachmentRule
            {
                Name = "Hyperphasing",
                Text =
                    "At the end of your opponent's turn, you can select a number of NECRONS units from your army " +
                    "(excluding units within Engagement Range of one or more enemy units). The maximum depends on " +
                    "battle size: Incursion — up to 1 unit; Strike Force — up to 2 units; Onslaught — up to 3 units. " +
                    "Remove those units from the battlefield and place them into Strategic Reserves.\n" +
                    "This detachment has the HYPERCRYPT tag and cannot be taken with another HYPERCRYPT detachment.",
            },
        ];

        // Enhancement display text (bearer's Play card). All four are "NECRONS model only" — R6-unconstrained.
        Author(d, "arisen-tyrant",
            "NECRONS model only. Each time a model in the bearer's unit makes an attack, re-roll a Hit roll of 1. " +
            "If the bearer's unit was set up on the battlefield this turn, you can re-roll the Hit roll instead.");
        Author(d, "dimensional-overseer",
            "NECRONS model only. While the bearer is on the battlefield or in Strategic Reserves, add 1 to the " +
            "number of units from your army that you can select for the Hyperphasing rule.");
        Author(d, "hyperspatial-transfer-node",
            "NECRONS model only. Each time the bearer's unit Advances, do not make an Advance roll for it. Instead, " +
            "until the end of the phase, add 6\" to the Move characteristic of models in the bearer's unit.");
        Author(d, "osteoclave-fulcrum",
            "NECRONS model only. Models in the bearer's unit have the Deep Strike ability.");

        d.Stratagems =
        [
            new Stratagem
            {
                Id = "dimensional-corridor", Name = "Dimensional Corridor", Type = "Strategic Ploy", CpCost = 2,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Charge],
                When = "Your Charge phase.",
                Target = "One NECRONS unit from your army that was set up on the battlefield this turn using the Eternity Gate ability of a MONOLITH model that started the turn on the battlefield.",
                Effect = "Your unit is eligible to charge this phase.",
            },
            new Stratagem
            {
                Id = "reanimation-crypts", Name = "Reanimation Crypts", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Command],
                When = "Your Command phase.",
                Target = "Your NECRONS WARLORD.",
                Effect = "For each of your NECRONS units in Reserves, that Reserves unit's Reanimation Protocols activate.",
            },
            new Stratagem
            {
                Id = "cosmic-precision", Name = "Cosmic Precision", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Movement],
                When = "Your Movement phase.",
                Target = "One NECRONS unit from your army (excluding MONSTER units) that is arriving using the Deep Strike or Hyperphasing abilities this phase.",
                Effect = "Your unit can be set up anywhere on the battlefield that is more than 6\" horizontally away from all enemy models. A unit targeted with this Stratagem is not eligible to declare a charge in the same turn.",
            },
            new Stratagem
            {
                Id = "entropic-damping", Name = "Entropic Damping", Type = "Wargear", CpCost = 1,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Shooting],
                RequiredUnitKeywords = ["Titanic"],
                When = "Your opponent's Shooting phase, just after an enemy unit has selected its targets.",
                Target = "One TITANIC model from your army that was selected as the target of one or more of the attacking unit's attacks and is within 18\" of the attacking unit.",
                Effect = "Until the end of the phase, weapons equipped by models in the attacking unit have the [HAZARDOUS] ability.",
            },
            new Stratagem
            {
                Id = "hyperphasic-recall", Name = "Hyperphasic Recall", Type = "Strategic Ploy", CpCost = 2,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                RequiredUnitKeywords = ["Infantry"],
                When = "Your opponent's Shooting phase or the Fight phase, just after an enemy unit has shot or fought.",
                Target = "One NECRONS INFANTRY unit from your army that had one or more of its models destroyed as a result of the attacking unit's attacks, and one friendly MONOLITH model.",
                Effect = "Remove your INFANTRY unit from the battlefield and then set it back up anywhere on the battlefield that is wholly within 6\" of your MONOLITH model and not within Engagement Range of one or more enemy units.",
            },
            new Stratagem
            {
                Id = "quantum-deflection", Name = "Quantum Deflection", Type = "Wargear", CpCost = 1,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                RequiredUnitKeywords = ["Vehicle"],
                When = "Your opponent's Shooting phase or the Fight phase, just after an enemy unit has selected its targets.",
                Target = "One NECRONS VEHICLE unit from your army that was selected as the target of one or more of the attacking unit's attacks.",
                Effect = "Until the end of the phase, models in your unit have a 4+ invulnerable save.",
            },
        ];

        return d;
    }

    // Obeisance Phalanx (2 DP) — "Worthy Foes". Command-phase target selection is manual, so prose only.
    private static Detachment ObeisancePhalanx()
    {
        var d = Make("Obeisance Phalanx", 2,
            ("Eternal Conqueror", 25),
            ("Honourable Combatant", 10),
            ("Unflinching Will", 20),
            ("Warrior Noble", 15));
        d.Enabled = true;
        d.Rules =
        [
            new DetachmentRule
            {
                Name = "Worthy Foes",
                Text =
                    "In your Command phase, select one enemy unit. Until the start of your next Command phase, each " +
                    "time a NOBLE, LYCHGUARD or TRIARCH unit from your army makes an attack against that unit, add 1 " +
                    "to the Wound roll.",
            },
        ];

        // Enhancement display text (bearer's Play card) + R6 eligibility — all four are OVERLORD model only.
        Author(d, "eternal-conqueror",
            "OVERLORD model only. Each time a model in the bearer's unit makes an attack that targets an enemy unit " +
            "within range of an objective marker, you can re-roll the Hit roll.",
            "Overlord");
        Author(d, "honourable-combatant",
            "OVERLORD model only. Each time the bearer's unit destroys an enemy CHARACTER unit, your opponent loses " +
            "1CP if they have any.",
            "Overlord");
        Author(d, "unflinching-will",
            "OVERLORD model only. The bearer's melee weapons have the [PRECISION] and [ANTI-INFANTRY 5+] abilities.",
            "Overlord");
        Author(d, "warrior-noble",
            "OVERLORD model only. Each time a melee attack targets the bearer's unit, subtract 1 from the Hit roll.",
            "Overlord");

        d.Stratagems =
        [
            new Stratagem
            {
                Id = "territorial-obsession", Name = "Territorial Obsession", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Command],
                RequiredUnitKeywords = ["Lychguard", "Triarch"],
                When = "Your Command phase.",
                Target = "One LYCHGUARD or TRIARCH unit from your army.",
                Effect = "Until the start of your next Command phase, add 1 to the Objective Control characteristic of models in your unit. If your unit has the VEHICLE keyword, add 3 to the Objective Control characteristic instead.",
            },
            new Stratagem
            {
                Id = "your-time-is-nigh", Name = "Your Time Is Nigh", Type = "Epic Deed", CpCost = 1,
                Turn = StratagemTurn.Either, Phases = [],
                When = "Any phase, just after your opponent's WARLORD is destroyed.",
                Target = "Your NECRONS WARLORD.",
                Effect = "Until the end of the battle, each time an enemy unit takes a Battle-shock or Leadership test, subtract 1 from the result.",
            },
            new Stratagem
            {
                Id = "suffer-no-rival", Name = "Suffer No Rival", Type = "Battle Tactic", CpCost = 1,
                Turn = StratagemTurn.Either, Phases = [BattlePhase.Fight],
                RequiredUnitKeywords = ["Lychguard", "Triarch"],
                When = "Fight phase.",
                Target = "One LYCHGUARD or TRIARCH unit from your army that has not been selected to fight this phase.",
                Effect = "Until the end of the phase, melee weapons equipped by models in your unit have the [PRECISION] ability.",
            },
            new Stratagem
            {
                Id = "sentinels-of-eternity", Name = "Sentinels of Eternity", Type = "Epic Deed", CpCost = 1,
                Turn = StratagemTurn.Either, Phases = [BattlePhase.Fight],
                RequiredUnitKeywords = ["Lychguard", "Triarch Praetorians"],
                When = "Fight phase, just after an enemy unit has selected its targets.",
                Target = "One LYCHGUARD or TRIARCH PRAETORIANS unit from your army that was selected as the target of one or more of the attacking unit's attacks.",
                Effect = "Until the end of the phase, each time a model in your unit is destroyed, if that model has not fought this phase, roll one D6: on a 4+, do not remove it from play. The destroyed model can fight after the attacking model's unit has finished making attacks, and is then removed from play.",
            },
            new Stratagem
            {
                Id = "nanoassembly-protocols", Name = "Nanoassembly Protocols", Type = "Battle Tactic", CpCost = 1,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                RequiredUnitKeywords = ["Vehicle"],
                When = "Your opponent's Shooting phase or the Fight phase, just after an enemy unit has selected its targets.",
                Target = "One NECRONS VEHICLE unit from your army that was selected as the target of one or more of the attacking unit's attacks.",
                Effect = "Until the end of the phase, each time an attack is allocated to a model in your unit, subtract 1 from the Damage characteristic of that attack.",
            },
            new Stratagem
            {
                Id = "enslaved-artifice", Name = "Enslaved Artifice", Type = "Battle Tactic", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                When = "Your Shooting phase or the Fight phase.",
                Target = "One NECRONS unit from your army (excluding TITANIC units) that has not been selected to shoot or fight this phase.",
                Effect = "Until the end of the phase, each time a model in your unit makes an attack, an unmodified Hit roll of 5+ scores a Critical Hit.",
            },
        ];

        return d;
    }

    // Pantheon of Woe (2 DP) — "Cosmic Distortion". Keeps its auto-applied Necrodermal Bindings (rule R10); the
    // Distortion Fields aura is proximity/board-state, so prose only.
    private static Detachment PantheonOfWoe()
    {
        var d = Make("Pantheon of Woe");
        d.DetachmentPoints = 2;
        d.Enabled = true;
        d.AppliesPantheonBindings = true;
        d.Rules =
        [
            new DetachmentRule
            {
                Name = "Cosmic Distortion",
                Text =
                    "NECRONS MONSTER units from your army have the Distortion Fields ability:\n" +
                    "Distortion Fields (Aura): While an enemy unit is within 6\" of this unit, it is unravelling. " +
                    "While an enemy unit is unravelling, each time an attack targets that unit, improve the Armour " +
                    "Penetration characteristic of that attack by 1.\n" +
                    "At the start of each phase, for each NECRONS MONSTER unit from your army, that unit can suffer " +
                    "3 mortal wounds. If it does, until the end of the phase the range of that unit's Distortion " +
                    "Fields aura is increased to 9\".\n" +
                    "When mustering, each NECRONS MONSTER unit takes its relevant Necrodermal Binding and pays the " +
                    "Munitorum Field Manual surcharge.",
            },
        ];

        d.Stratagems =
        [
            new Stratagem
            {
                Id = "phase-melding", Name = "Phase Melding", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Movement],
                When = "Your opponent's Movement phase, when an unravelling enemy unit is selected to Fall Back.",
                Target = "One NECRONS unit from your army that is within Engagement Range of that enemy unit.",
                Effect = "When that enemy unit Falls Back, all models in that enemy unit must take a Desperate Escape test. When doing so, if that enemy unit is Battle-shocked, subtract 1 from each of those tests.",
            },
            new Stratagem
            {
                Id = "disharmonisation-cascade", Name = "Disharmonisation Cascade", Type = "Epic Deed", CpCost = 1,
                Turn = StratagemTurn.Either, Phases = [],
                RequiredUnitKeywords = ["Monster"],
                When = "Any phase, just after a NECRONS MONSTER model from your army is destroyed, before making its Deadly Demise roll.",
                Target = "That NECRONS MONSTER model. You can use this Stratagem on that model even though it was just destroyed.",
                Effect = "Until the end of the phase, your model's Deadly Demise ability inflicts mortal wounds on a D6 roll of 3+ instead of on a 6.",
            },
            new Stratagem
            {
                Id = "molecular-erosion", Name = "Molecular Erosion", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Either, Phases = [BattlePhase.Command],
                RequiredUnitKeywords = ["Monster"],
                When = "Command phase.",
                Target = "One NECRONS MONSTER unit from your army.",
                Effect = "Select one unravelling enemy unit visible to your unit. That enemy unit must take a Battle-shock test, subtracting 1 from the result. If that test is failed, that enemy unit suffers D3+1 mortal wounds. You can only use this Stratagem once per battle round.",
            },
            new Stratagem
            {
                Id = "chronodistortion", Name = "Chronodistortion", Type = "Battle Tactic", CpCost = 1,
                Turn = StratagemTurn.Either, Phases = [BattlePhase.Fight],
                When = "Fight phase, just after an enemy unit has selected its targets.",
                Target = "One NECRONS unit from your army that was selected as the target of one or more of the attacking unit's attacks.",
                Effect = "Until the end of the phase, each time a model in your unit is destroyed, if that model has not fought this phase, roll one D6, adding 1 if the attacking unit is unravelling: on a 4+, do not remove the destroyed model from play; it can fight after the attacking unit has finished making its attacks, and is then removed from play.",
            },
            new Stratagem
            {
                Id = "entrophasic-aura-targeting", Name = "Entrophasic Aura Targeting", Type = "Battle Tactic", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                When = "Your Shooting phase or the Fight phase.",
                Target = "One NECRONS unit (excluding MONSTER units) from your army that has not been selected to shoot or fight this phase.",
                Effect = "Until the end of the phase, each time a model in your unit makes an attack that targets an enemy unit, re-roll a Hit roll of 1. If the target of that attack is unravelling, re-roll a Wound roll of 1 as well.",
            },
            new Stratagem
            {
                Id = "mass-transmogrification", Name = "Mass Transmogrification", Type = "Epic Deed", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                When = "Your Shooting phase or the Fight phase, just after a NECRONS MONSTER unit from your army destroys an enemy unit.",
                Target = "One friendly NECRONS unit (excluding MONSTER units) within 6\" of that MONSTER unit.",
                Effect = "If that enemy unit was unravelling at the start of the phase, your friendly unit's Reanimation Protocols activate. You can only use this Stratagem once per turn.",
            },
        ];

        return d;
    }

    // Skyshroud Spearhead (1 DP) — "Transdimensional Deployment". Deep Strike and the ingress +1 Hit are both
    // conditional on a manual move, so the rule stays prose.
    private static Detachment SkyshroudSpearhead()
    {
        var d = Make("Skyshroud Spearhead", 1,
            ("Deepening Madness", 20),
            ("Recursive Reanimation", 5));
        d.Enabled = true;
        d.Rules =
        [
            new DetachmentRule
            {
                Name = "Transdimensional Deployment",
                Text =
                    "Friendly TOMB BLADES units have Deep Strike.\n" +
                    "When a friendly TOMB BLADES unit is selected to shoot, if that unit made an ingress move this " +
                    "turn, that unit's ranged attacks have +1 to Hit rolls.",
            },
        ];
        return d;
    }

    // The Phaeron's Armoury (1 DP, HYPERCRYPT) — "Empowered Engines". The +6" Move for NECRONS TITANIC is wired
    // as a data-driven buff. Tagged HYPERCRYPT: mutually exclusive with Hypercrypt Legion (per rules text).
    private static Detachment PhaeronsArmoury()
    {
        var d = Make("The Phaeron's Armoury", 1,
            ("Mortality Shroud", 10),
            ("Prelocational Optimiser", 25));
        d.Enabled = true;
        d.Tags = ["Hypercrypt"];
        d.Rules =
        [
            new DetachmentRule
            {
                Name = "Empowered Engines",
                Text =
                    "Friendly NECRONS TITANIC FLY units have +6\" Move.\n" +
                    "This detachment has the HYPERCRYPT tag and cannot be taken with another HYPERCRYPT detachment.",
            },
        ];
        // Always-on: NECRONS TITANIC models gain +6" Move. Keyword match is any-of, so "Titanic" is used (Necrons
        // Titanic units are also FLY); the AND with FLY isn't expressible here but no non-FLY Titanic exists.
        d.StatBuffs =
        [
            new DetachmentStatBuff
            {
                Scope = GrantScope.Unit,
                Keywords = ["Titanic"],
                Modifier = new StatModifier
                {
                    Target = StatTarget.Move,
                    Delta = 6,
                    Label = "+6\" M",
                },
            },
        ];
        return d;
    }

    // Starshatter Arsenal (3 DP, Priority Assets) — "Relentless Onslaught".
    private static Detachment StarshatterArsenal()
    {
        // Enhancement points are authored; their text/eligibility and the stratagems follow (§10/§11).
        var d = Make("Starshatter Arsenal", 3,
            ("Chrono-impedance Fields", 25),
            ("Demanding Leader", 10),
            ("Dread Majesty", 30),
            ("Miniaturised Nebuloscope", 15));
        d.Enabled = true;

        d.Rules =
        [
            new DetachmentRule
            {
                Name = "Relentless Onslaught",
                Text =
                    "Each time a NECRONS model (excluding MONSTER models) from your army makes an attack that " +
                    "targets a unit within range of one or more objective markers, add 1 to the Hit roll.\n" +
                    "In addition, ranged weapons equipped by NECRONS VEHICLE and NECRONS MOUNTED models " +
                    "(excluding TITANIC models) from your army have the [ASSAULT] ability.",
                ConditionalBuffs =
                [
                    new ConditionalUnitBuff
                    {
                        Label = "Relentless Onslaught",
                        Effect = "+1 to Hit while targeting a unit on an objective",
                        RequiredKeywords = ["Necrons"],
                        ExcludedKeywords = ["Monster"],
                        Modifiers =
                        [
                            new StatModifier
                            {
                                Target = StatTarget.Skill,
                                Delta = 1,
                                WeaponClass = WeaponClass.Any,
                                Label = "+1 Hit",
                            },
                        ],
                    },
                ],
            },
        ];

        // The always-on half is data-driven: VEHICLE/MOUNTED (not TITANIC) models' ranged weapons gain [ASSAULT].
        // The objective-conditional +1 Hit depends on live battlefield state, so it stays as reference text above.
        d.WeaponGrants =
        [
            new WeaponAbilityGrant
            {
                Keywords = ["Vehicle", "Mounted"],
                ExcludedKeywords = ["Titanic"],
                Scope = GrantScope.Model,
                WeaponClass = DetachmentWeaponClass.Ranged,
                Abilities = ["Assault"],
            },
        ];

        // Enhancement display text (bearer's Play card) + R6 eligibility. Three are conditional (aura / command-
        // phase selection) so they stay as schedulable reference text; Dread Majesty is OVERLORD-or-CCB only.
        Author(d, "miniaturised-nebuloscope",
            "NECRONS model only. Ranged weapons equipped by models in the bearer's unit have the [IGNORES COVER] " +
            "ability.");
        Author(d, "demanding-leader",
            "NECRONS model only. In your Command phase, select one friendly NECRONS VEHICLE or NECRONS MOUNTED " +
            "unit (excluding TITANIC units) within 6\" of the bearer. Until the start of your next Command phase, " +
            "that unit is eligible to shoot in a turn in which it Fell Back.");
        Author(d, "chrono-impedance-fields",
            "NECRONS model only. In your Command phase, select one friendly NECRONS VEHICLE or NECRONS MOUNTED " +
            "unit (excluding TITANIC units) within 6\" of the bearer. Until the start of your next Command phase, " +
            "each time an attack is allocated to a model in that unit, subtract 1 from the Damage characteristic " +
            "of that attack.");
        Author(d, "dread-majesty",
            "OVERLORD or CATACOMB COMMAND BARGE model only. Aura. While a friendly NECRONS unit (excluding " +
            "MONSTER and TITANIC units) is within 6\" of the bearer, each time a model in that unit makes an " +
            "attack, re-roll a Hit roll of 1 and re-roll a Wound roll of 1.");
        d.FindEnhancement("dread-majesty")!.Eligibility.AnyOfKeywords = ["Overlord", "Catacomb Command Barge"];

        d.Stratagems =
        [
            new Stratagem
            {
                Id = "merciless-reclamation", Name = "Merciless Reclamation", Type = "Battle Tactic", CpCost = 2,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                When = "Your Shooting phase or the Fight phase.",
                Target = "One NECRONS unit (excluding MONSTER and TITANIC units) from your army that has not been selected to shoot or fight this phase.",
                Effect = "Until the end of the phase, each time a model in your unit makes an attack, if the target of that attack is within range of one or more objective markers, add 1 to the Wound roll.",
            },
            new Stratagem
            {
                Id = "unyielding-forms", Name = "Unyielding Forms", Type = "Battle Tactic", CpCost = 2,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Shooting, BattlePhase.Fight],
                RequiredUnitKeywords = ["Vehicle", "Mounted"],
                When = "Your opponent's Shooting phase or the Fight phase, just after an enemy unit has selected its targets.",
                Target = "One NECRONS VEHICLE or NECRONS MOUNTED unit (excluding TITANIC units) from your army that was selected as the target of one or more of the attacking unit's attacks.",
                Effect = "Until the end of the phase, each time an attack targets a model in your unit, if the Strength characteristic of that attack is greater than the Toughness characteristic of that unit, subtract 1 from the Wound roll.",
            },
            new Stratagem
            {
                Id = "chronoshift", Name = "Chronoshift", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Movement],
                RequiredUnitKeywords = ["Vehicle", "Mounted"],
                When = "Your Movement phase.",
                Target = "One NECRONS VEHICLE or NECRONS MOUNTED unit (excluding TITANIC units) from your army that has not been selected to move this phase.",
                Effect = "Until the end of the phase, if your unit Advances, do not make an Advance roll for it. Instead, until the end of the phase, add 6\" to the Move characteristic of models in your unit.",
            },
            new Stratagem
            {
                Id = "dimensional-tunnel", Name = "Dimensional Tunnel", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Movement],
                RequiredUnitKeywords = ["Vehicle", "Mounted"],
                When = "Your Movement phase.",
                Target = "One NECRONS VEHICLE or NECRONS MOUNTED unit (excluding TITANIC units) from your army.",
                Effect = "Until the end of the phase, models in your unit can move horizontally through models and terrain features.",
            },
            new Stratagem
            {
                Id = "endless-servitude", Name = "Endless Servitude", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Your, Phases = [BattlePhase.Fight],
                When = "End of your Fight phase.",
                Target = "One NECRONS unit (excluding MONSTER and TITANIC units) from your army that is within range of one or more objective markers you control.",
                Effect = "Your unit's Reanimation Protocols activate.",
            },
            new Stratagem
            {
                Id = "reactive-reposition", Name = "Reactive Reposition", Type = "Strategic Ploy", CpCost = 1,
                Turn = StratagemTurn.Opponent, Phases = [BattlePhase.Shooting],
                When = "Your opponent's Shooting phase, just after an enemy unit has shot.",
                Target = "One NECRONS unit from your army (excluding MONSTER and TITANIC units) that was the target of one or more of the attacking unit's attacks.",
                Effect = "Your unit can make a Normal move of up to D6\".",
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
    /// Builds a detachment with its Detachment-Points cost and enhancement list. These entries carry points
    /// only (DP + enhancement costs) and stay <see cref="Detachment.Enabled"/> = false until their 11th-edition
    /// rules/stratagems/eligibility (§10/§11) are authored.
    /// </summary>
    private static Detachment Make(string name, int detachmentPoints, params (string Name, int Points)[] enhancements)
    {
        var d = Make(name, enhancements);
        d.DetachmentPoints = detachmentPoints;
        return d;
    }

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
