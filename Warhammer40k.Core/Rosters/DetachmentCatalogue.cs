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
        Make("Skyshroud Spearhead", 1,
            ("Deepening Madness", 20),
            ("Recursive Reanimation", 5)),
        Make("The Phaeron's Armoury", 1,
            ("Mortality Shroud", 10),
            ("Prelocational Optimiser", 25)),
        StarshatterArsenal(),
        Cryptek(),
        Make("Cursed Legion", 2,
            ("Cursed Circlet", 25),
            ("Destroyer Ankh", 20),
            ("Mark of the Nekrosor", 20),
            ("Murdermind", 15)),
        MakePantheon("Pantheon of Woe"),
        Make("Annihilation Legion", 2,
            ("Eldritch Nightmare", 10),
            ("Eternal Madness", 20),
            ("Ingrained Superiority", 5),
            ("Soulless Reaper", 15)),
        AwakenedDynasty(),
        Make("Canoptek Court", 3,
            ("Autodivinator", 15),
            ("Dimensional Sanctum", 20),
            ("Hyperphasic Fulcrum", 15),
            ("Metalodermal Tesla Weave", 10)),
        Make("Hypercrypt Legion", 2,
            ("Arisen Tyrant", 25),
            ("Dimensional Overseer", 25),
            ("Hyperspatial Transfer Node", 15),
            ("Osteoclave Fulcrum", 20)),
        Make("Obeisance Phalanx", 2,
            ("Eternal Conqueror", 25),
            ("Honourable Combatant", 10),
            ("Unflinching Will", 20),
            ("Warrior Noble", 15)),
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
