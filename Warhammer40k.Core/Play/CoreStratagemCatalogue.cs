namespace Warhammer40k.Core.Play;

/// <summary>
/// The universal Core Stratagems (§15), authored verbatim from the 11th-edition rulebook and shown
/// contextually in Play Mode. Pure reference data ordered by rulebook id. <see cref="Usable"/> applies the
/// "need to know now" filter — only the stratagems whose phase(s) and "Used in" turn marker match the moment.
/// </summary>
/// <remarks>
/// Delivered in sets as the rulebook is transcribed; append new entries to <see cref="All"/> in id order.
/// The coloured "Used in" markers map to <see cref="StratagemTurn"/>: green = Either, blue = Your, red = Opponent.
/// </remarks>
public static class CoreStratagemCatalogue
{
    /// <summary>Every Core Stratagem, in rulebook order.</summary>
    public static readonly IReadOnlyList<CoreStratagem> All =
    [
        new CoreStratagem
        {
            Id = "15.02",
            Name = "Command Re-roll",
            Cost = 1,
            Turn = StratagemTurn.Either,
            Phases = [], // Any phase.
            Flavour = "A great commander can bend even the vagaries of fate and fortune to their will, the better to ensure victory.",
            When = "Any phase, just after you make one of the following rolls for a friendly unit or model:\n" +
                   "• Advance roll\n" +
                   "• Charge roll\n" +
                   "• Damage roll\n" +
                   "• Hazard roll\n" +
                   "• Hit roll\n" +
                   "• Save roll\n" +
                   "• Wound roll\n" +
                   "• A roll to determine the number of attacks generated with a weapon.",
            Target = "That unit or model.",
            Effect = "You re-roll that roll. If you are rolling more than one dice together, select one of those dice to re-roll (excluding charge rolls, which you must re-roll in full).",
        },
        new CoreStratagem
        {
            Id = "15.03",
            Name = "Epic Challenge",
            Cost = 1,
            Turn = StratagemTurn.Either,
            Phases = [BattlePhase.Fight],
            RequiredUnitKeywords = ["Character"],
            Flavour = "The legends of the 41st millennium are replete with deadly duels between mighty champions.",
            When = "Fight phase, just after a friendly CHARACTER unit is selected to fight.",
            Target = "That CHARACTER unit.",
            Effect = "Select one CHARACTER model in your unit. Until the end of the phase, that model's melee weapons have the [PRECISION] ability.",
        },
        new CoreStratagem
        {
            Id = "15.04",
            Name = "Insane Bravery",
            Cost = 1,
            Turn = StratagemTurn.Your,
            Phases = [BattlePhase.Command],
            Flavour = "Indifferent to their own survival, these warriors hold their ground against seemingly impossible odds.",
            When = "Battle-shock step of your Command phase, just before you make a battle-shock roll for a friendly unit.",
            Target = "That unit.",
            Effect = "That battle-shock roll is automatically successful.",
            Restrictions = "You cannot use this stratagem more than once per battle.",
        },
        new CoreStratagem
        {
            Id = "15.05",
            Name = "Explosives",
            Cost = 1,
            Turn = StratagemTurn.Your,
            Phases = [BattlePhase.Shooting],
            RequiredUnitKeywords = ["Explosives", "Grenades"],
            Flavour = "Priming grenades or other explosives, these warriors draw back and hurl death into the enemy's midst.",
            When = "Your Shooting phase.",
            Target = "One friendly unengaged EXPLOSIVES/GRENADES unit that is eligible to shoot and did not make an advance move this turn.",
            Effect = "Resolve the following sequence:\n" +
                     "1. Select one EXPLOSIVES/GRENADES model in your unit.\n" +
                     "2. Select one unengaged enemy unit within 8\" of and visible to that model.\n" +
                     "3. Roll six D6: for each 4+, that enemy unit suffers 1 mortal wound (06.02).",
        },
        new CoreStratagem
        {
            Id = "15.06",
            Name = "Crushing Impact",
            Cost = 1,
            Turn = StratagemTurn.Your,
            Phases = [BattlePhase.Charge],
            RequiredUnitKeywords = ["Monster", "Vehicle"],
            Flavour = "In extremis, armoured vehicles and rampaging monsters can use their sheer size as a weapon, ramming and crushing enemies beneath their colossal bulk, though doing so risks sustaining damage in return.",
            When = "Your Charge phase, just after a friendly MONSTER/VEHICLE unit ends a charge move.",
            Target = "That MONSTER/VEHICLE unit.",
            Effect = "Resolve the following sequence:\n" +
                     "1. Select one enemy unit engaged with your unit.\n" +
                     "2. Select one model in your unit engaged with that enemy unit.\n" +
                     "3. Roll a number of D6 equal to the T characteristic of that model: for each 1, your unit suffers 1 mortal wound; for each 5+, that enemy unit suffers 1 mortal wound (to a maximum of 6 mortal wounds per unit).",
        },
        new CoreStratagem
        {
            Id = "15.07",
            Name = "Rapid Ingress",
            Cost = 1,
            Turn = StratagemTurn.Opponent,
            Phases = [BattlePhase.Movement],
            Flavour = "Be it cunning strategy, potent technology or supernatural ritual, there are many means by which a commander may hasten their warriors' onset.",
            When = "End of your opponent's Movement phase.",
            Target = "One friendly unit that is in strategic reserves (excluding AIRCRAFT).",
            Effect = "Your unit makes an ingress move (20.04).",
            Restrictions = "You cannot use this stratagem during the first battle round.",
        },
        new CoreStratagem
        {
            Id = "15.08",
            Name = "Fire Overwatch",
            Cost = 1,
            Turn = StratagemTurn.Opponent,
            Phases = [BattlePhase.Movement],
            Flavour = "A hail of fire can drive back advancing foes.",
            When = "End of your opponent's Movement phase.",
            Target = "One friendly unengaged unit (excluding TITANIC units).",
            Effect = "Your unit shoots using snap shooting (15.09). Snap shooting — your unit shoots as described in Making Attacks (04), except:\n" +
                     "• You can only target one visible enemy unit within 24\" of your unit (and only if it is an eligible target).\n" +
                     "• Each attack only hits on an unmodified hit roll of 6 (irrespective of the attacking weapon's BS characteristic or any modifiers).\n" +
                     "• You cannot re-roll hit rolls.\n" +
                     "After shooting, until the end of the phase your unit is not eligible to start an action.",
        },
        new CoreStratagem
        {
            Id = "15.10",
            Name = "Smokescreen",
            Cost = 1,
            Turn = StratagemTurn.Opponent,
            Phases = [BattlePhase.Shooting],
            RequiredUnitKeywords = ["Smoke"],
            Flavour = "Even the most skilled marksmen struggle to hit targets veiled by billowing screens of smoke.",
            When = "Start of your opponent's Shooting phase.",
            Target = "One friendly SMOKE unit.",
            Effect = "Until the end of the phase, each time an attack targets either your SMOKE unit, or a unit that is not fully visible to the attacking model because of one or more models in your SMOKE unit, the target has the benefit of cover against that attack (13.08).",
        },
        new CoreStratagem
        {
            Id = "15.11",
            Name = "Heroic Intervention",
            Cost = 1,
            Turn = StratagemTurn.Opponent,
            Phases = [BattlePhase.Charge],
            Flavour = "Voices raised in furious war cries, your warriors surge forth to meet the enemy's onslaught head-on.",
            When = "End of your opponent's Charge phase.",
            Target = "One friendly unengaged unit within 12\" of one or more enemy units. You can only select a VEHICLE unit if it is a CHARACTER/WALKER unit.",
            Effect = "Resolve a charge with your unit (11.02). While doing so, before making the charge roll, you must select one of the following modes:\n" +
                     "• Leap to Defend: When selecting charge targets, you can only select enemy units that made a charge move this phase and are within the maximum distance.\n" +
                     "• Into the Fray (+1CP): When making the charge roll, if the result is greater than 6 (after modifiers), change it to 6. When selecting charge targets, you can select any enemy units that are within 6\" of your unit and within the maximum distance.",
        },
        new CoreStratagem
        {
            Id = "15.12",
            Name = "Counteroffensive",
            Cost = 2,
            Turn = StratagemTurn.Opponent,
            Phases = [BattlePhase.Fight],
            Flavour = "In close-quarters combat, the slightest hesitation can leave an opening for a swift foe to exploit.",
            When = "Fight step of your opponent's Fight phase, just after an enemy unit has resolved its attacks.",
            Target = "One friendly unit that is eligible to fight.",
            Effect = "Until the end of the phase, your unit has the Fights First ability and it must be the next unit you select to fight (12.04).",
        },
    ];
}
