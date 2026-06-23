using Warhammer40k.Api;
using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins <see cref="BattleRoster.Build"/>: resolving units against the catalogue, merging attached Leaders
/// into their bodyguard (and leaving dangling attachments standalone), wound-pool maths, and phase content.
/// </summary>
public class BattleRosterTests
{
    private static Datasheet Sheet(string id, string name, string wounds = "1",
        IEnumerable<WeaponProfile>? weapons = null, IEnumerable<Ability>? abilities = null) => new()
    {
        Id = id,
        Name = name,
        StatProfiles = [new StatProfile { Name = name, Wounds = wounds }],
        Weapons = weapons?.ToList() ?? [],
        Abilities = abilities?.ToList() ?? [],
    };

    private static CatalogueData Catalogue(params Datasheet[] sheets) => new() { Datasheets = sheets.ToList() };

    private static RosterUnit Unit(string id, string datasheetId, int models = 1,
        string? attachedTo = null, bool warlord = false) => new()
    {
        Id = id,
        DatasheetId = datasheetId,
        ModelCount = models,
        AttachedToRosterUnitId = attachedTo,
        IsWarlord = warlord,
    };

    [Fact]
    public void Merges_attached_leader_into_bodyguard_as_one_group()
    {
        var catalogue = Catalogue(
            Sheet("overlord", "Overlord"),
            Sheet("necron-warriors", "Necron Warriors"));
        var roster = new Roster
        {
            Units =
            [
                Unit("u1", "overlord", attachedTo: "u2", warlord: true),
                Unit("u2", "necron-warriors", models: 10),
            ],
        };

        var battle = BattleRoster.Build(roster, catalogue);

        var group = Assert.Single(battle.Units);
        Assert.Equal("u2", group.Id); // primary is the bodyguard
        Assert.Equal(2, group.Parts.Count);
        Assert.True(group.Primary.Datasheet.Id == "necron-warriors");
        Assert.Contains("Overlord", group.Name);
        Assert.True(group.IsWarlord);
        Assert.Equal(11, group.ModelCount);
        Assert.True(group.Parts[1].IsLeader);
    }

    [Fact]
    public void Dangling_attachment_leaves_leader_standalone()
    {
        var catalogue = Catalogue(Sheet("overlord", "Overlord"));
        var roster = new Roster { Units = [Unit("u1", "overlord", attachedTo: "missing")] };

        var battle = BattleRoster.Build(roster, catalogue);

        var group = Assert.Single(battle.Units);
        Assert.Single(group.Parts);
        Assert.False(group.Primary.IsLeader);
    }

    [Fact]
    public void Skips_units_whose_datasheet_is_missing()
    {
        var catalogue = Catalogue(Sheet("overlord", "Overlord"));
        var roster = new Roster { Units = [Unit("u1", "overlord"), Unit("u2", "ghost-of-a-unit")] };

        var battle = BattleRoster.Build(roster, catalogue);

        Assert.Single(battle.Units);
    }

    [Fact]
    public void Wound_pool_sums_models_and_attached_leader()
    {
        var catalogue = Catalogue(
            Sheet("overlord", "Overlord", wounds: "4"),
            Sheet("necron-warriors", "Necron Warriors", wounds: "1"));
        var roster = new Roster
        {
            Units =
            [
                Unit("u1", "overlord", attachedTo: "u2"),
                Unit("u2", "necron-warriors", models: 10),
            ],
        };

        var group = Assert.Single(BattleRoster.Build(roster, catalogue).Units);
        Assert.Equal(14, group.MaxWounds); // 10×1 + 1×4
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("4", 4)]
    [InlineData("12", 12)]
    public void Parses_fixed_wounds(string wounds, int expected) =>
        Assert.Equal(expected, BattleRoster.ParseWounds(wounds));

    [Theory]
    [InlineData("D6")]
    [InlineData("D3+1")]
    [InlineData("")]
    [InlineData(null)]
    public void Variable_or_empty_wounds_are_not_trackable(string? wounds) =>
        Assert.Null(BattleRoster.ParseWounds(wounds));

    [Fact]
    public void Splits_weapons_and_phase_content()
    {
        var sheet = Sheet("immortals", "Immortals", wounds: "1",
            weapons:
            [
                new WeaponProfile { Name = "Gauss blaster", Type = "Ranged" },
                new WeaponProfile { Name = "Close combat weapon", Type = "Melee" },
            ],
            abilities:
            [
                new Ability { Name = "Protocols", Text = "At the start of your Command phase, choose a protocol." },
                new Ability { Name = "Invulnerable Save", Text = "This model has a 4+ invulnerable save." },
            ]);
        var roster = new Roster { Units = [Unit("u1", "immortals", models: 5)] };

        var group = Assert.Single(BattleRoster.Build(roster, Catalogue(sheet)).Units);
        var part = group.Primary;

        Assert.Single(part.RangedWeapons);
        Assert.Single(part.MeleeWeapons);
        Assert.True(group.HasContentIn(BattlePhase.Shooting));
        Assert.True(group.HasContentIn(BattlePhase.Fight));
        // Command shows the command-phase ability AND the passive invuln ability.
        Assert.Equal(2, part.AbilitiesIn(BattlePhase.Command).Count);
        Assert.Equal("4+", group.InvulnerableSave);
    }

    [Fact]
    public void Part_weapons_honour_setup_selection()
    {
        var sheet = Sheet("necron-warriors", "Necron Warriors", wounds: "1",
            weapons:
            [
                new WeaponProfile { Name = "Close combat weapon", Type = "Melee" },
                new WeaponProfile { Name = "Gauss flayer", Type = "Ranged" },
                new WeaponProfile { Name = "Gauss reaper", Type = "Ranged" },
            ]);
        sheet.WargearGroups =
        [
            new WargearGroup
            {
                Id = "g", Name = "Weapon", Min = 0, Max = 1,
                Options = [new() { Id = "gauss-flayer", Name = "Gauss flayer" }, new() { Id = "gauss-reaper", Name = "Gauss reaper" }],
            },
        ];
        var unit = Unit("u1", "necron-warriors", models: 10);
        unit.Wargear = [new WargearSelection { GroupId = "g", OptionIds = ["gauss-flayer"] }];
        var roster = new Roster { Units = [unit] };

        var part = Assert.Single(BattleRoster.Build(roster, Catalogue(sheet)).Units).Primary;
        var names = part.Weapons.Select(w => w.Name).ToList();

        Assert.Contains("Gauss flayer", names);
        Assert.Contains("Close combat weapon", names);
        Assert.DoesNotContain("Gauss reaper", names);
    }

    [Fact]
    public void Combined_abilities_merge_across_parts_and_dedupe()
    {
        var overlord = Sheet("overlord", "Overlord", abilities:
        [
            new Ability { Name = "Leader", Text = "Can be attached." },
            new Ability { Name = "My Will Be Done", Text = "Buff." },
        ]);
        var warriors = Sheet("necron-warriors", "Necron Warriors", abilities:
        [
            new Ability { Name = "Reanimation Protocols", Text = "Heal." },
            new Ability { Name = "Leader", Text = "Duplicate name should be deduped." },
        ]);
        var roster = new Roster
        {
            Units =
            [
                Unit("u1", "overlord", attachedTo: "u2"),
                Unit("u2", "necron-warriors", models: 10),
            ],
        };

        var group = Assert.Single(BattleRoster.Build(roster, Catalogue(overlord, warriors)).Units);
        var abilities = group.CombinedAbilities;
        var names = abilities.Select(a => a.Ability.Name).ToList();

        // Primary (warriors) first, then leader (overlord); "Leader" appears once (deduped).
        Assert.Equal(new[] { "Reanimation Protocols", "Leader", "My Will Be Done" }, names);
        Assert.Equal("Necron Warriors", abilities[0].Source);
        Assert.Equal("Overlord", abilities.Single(a => a.Ability.Name == "My Will Be Done").Source);
    }

    [Fact]
    public void Single_multi_wound_model_tracks_wounds_even_when_attached()
    {
        var catalogue = Catalogue(
            Sheet("imotekh", "Imotekh", wounds: "6"),
            Sheet("necron-warriors", "Necron Warriors", wounds: "1"));
        var roster = new Roster
        {
            Units =
            [
                Unit("u1", "imotekh", attachedTo: "u2"),
                Unit("u2", "necron-warriors", models: 10),
            ],
        };

        var group = Assert.Single(BattleRoster.Build(roster, catalogue).Units);
        var warriors = group.Primary;
        var imotekh = group.Parts.Single(p => p.Datasheet.Id == "imotekh");

        // The lone Character tracks wounds (6); the multi-model bodyguard tracks models (10).
        Assert.True(imotekh.TracksWounds);
        Assert.Equal(6, imotekh.TrackMax);
        Assert.False(warriors.TracksWounds);
        Assert.Equal(10, warriors.TrackMax);
    }

    [Theory]
    [InlineData(1, "1", false, 1)]   // lone single-wound model → still a model count
    [InlineData(1, "6", true, 6)]    // lone multi-wound model → tracks 6 wounds
    [InlineData(5, "2", false, 5)]   // multi-model unit → tracks 5 models
    [InlineData(1, "D6", false, 1)]  // variable wounds can't be tracked numerically
    public void TracksWounds_and_TrackMax_follow_models_and_wounds(int models, string wounds, bool tracksWounds, int trackMax)
    {
        var catalogue = Catalogue(Sheet("u", "Unit", wounds: wounds));
        var roster = new Roster { Units = [Unit("r", "u", models: models)] };

        var part = Assert.Single(BattleRoster.Build(roster, catalogue).Units).Primary;

        Assert.Equal(tracksWounds, part.TracksWounds);
        Assert.Equal(trackMax, part.TrackMax);
    }

    // ---------- Cryptek Conclave detachment: smart weapon effects ----------

    [Fact]
    public void Cryptek_Conclave_grants_Assault_to_Cryptek_models_ranged_weapons_only()
    {
        var cryptek = Sheet("plasmancer", "Plasmancer", wounds: "4",
            weapons:
            [
                new WeaponProfile { Name = "Gauntlet", Type = "Ranged" },
                new WeaponProfile { Name = "Stave", Type = "Melee" },
            ]);
        cryptek.Keywords = ["Cryptek"];
        var warriors = Sheet("necron-warriors", "Necron Warriors", wounds: "1",
            weapons: [new WeaponProfile { Name = "Gauss flayer", Type = "Ranged" }]);

        var roster = new Roster
        {
            DetachmentId = "cryptek-conclave",
            Units = [Unit("u1", "plasmancer"), Unit("u2", "necron-warriors", models: 10)],
        };

        var battle = BattleRoster.Build(roster, Catalogue(cryptek, warriors));
        var crypUnit = battle.Units.Single(u => u.Primary.Datasheet.Id == "plasmancer");
        var warUnit = battle.Units.Single(u => u.Primary.Datasheet.Id == "necron-warriors");

        // Cryptek's ranged weapons gain [ASSAULT]; its melee weapons do not.
        Assert.Contains("Assault", battle.GrantedWeaponAbilities(crypUnit, crypUnit.Primary, ranged: true), StringComparer.OrdinalIgnoreCase);
        Assert.Empty(battle.GrantedWeaponAbilities(crypUnit, crypUnit.Primary, ranged: false));
        // A non-Cryptek model gets nothing (model-targeted, not unit-targeted).
        Assert.Empty(battle.GrantedWeaponAbilities(warUnit, warUnit.Primary, ranged: true));
    }

    [Fact]
    public void Cryptek_Conclave_offers_shooting_choices_only_to_units_with_a_Cryptek()
    {
        var cryptek = Sheet("plasmancer", "Plasmancer", wounds: "4");
        cryptek.Keywords = ["Cryptek"];
        var warriors = Sheet("necron-warriors", "Necron Warriors", wounds: "1");

        var roster = new Roster
        {
            DetachmentId = "cryptek-conclave",
            Units =
            [
                Unit("led", "necron-warriors", models: 10),
                Unit("plas", "plasmancer", attachedTo: "led"), // attached Cryptek → the unit qualifies
                Unit("lone", "necron-warriors", models: 10),   // no Cryptek
            ],
        };

        var battle = BattleRoster.Build(roster, Catalogue(cryptek, warriors));
        var ledUnit = battle.Units.Single(u => u.Parts.Count == 2);
        var loneUnit = battle.Units.Single(u => u.Parts.Count == 1);

        Assert.NotEmpty(battle.WeaponChoicesFor(ledUnit));
        Assert.Empty(battle.WeaponChoicesFor(loneUnit));
        Assert.Contains("Anti-INFANTRY 3+", battle.WeaponChoicesFor(ledUnit)[0].Options);
    }

    [Fact]
    public void No_detachment_grants_nothing()
    {
        var cryptek = Sheet("plasmancer", "Plasmancer", wounds: "4",
            weapons: [new WeaponProfile { Name = "Gauntlet", Type = "Ranged" }]);
        cryptek.Keywords = ["Cryptek"];
        var roster = new Roster { Units = [Unit("u1", "plasmancer")] };

        var battle = BattleRoster.Build(roster, Catalogue(cryptek));
        var unit = Assert.Single(battle.Units);

        Assert.Empty(battle.GrantedWeaponAbilities(unit, unit.Primary, ranged: true));
        Assert.Empty(battle.WeaponChoicesFor(unit));
    }

    [Fact]
    public void Hand_of_the_Dynasty_grants_Assault_to_every_model_in_Warriors_or_Immortals_units()
    {
        var warriors = Sheet("necron-warriors", "Necron Warriors", wounds: "1",
            weapons: [new WeaponProfile { Name = "Gauss flayer", Type = "Ranged" }]);
        warriors.Keywords = ["Infantry", "Battleline", "Necron Warriors"];
        var cryptek = Sheet("plasmancer", "Plasmancer", wounds: "4",
            weapons: [new WeaponProfile { Name = "Gauntlet", Type = "Ranged" }]);
        cryptek.Keywords = ["Cryptek"];
        var lokhust = Sheet("lokhust", "Lokhust Destroyers", wounds: "3",
            weapons: [new WeaponProfile { Name = "Gauss cannon", Type = "Ranged" }]);
        lokhust.Keywords = ["Infantry", "Lokhust Destroyers"];

        var roster = new Roster
        {
            DetachmentId = "hand-of-the-dynasty",
            Units =
            [
                Unit("w", "necron-warriors", models: 10),
                Unit("c", "plasmancer", attachedTo: "w"), // Cryptek attached to a Warriors unit
                Unit("l", "lokhust", models: 3),          // not Warriors/Immortals
            ],
        };

        var battle = BattleRoster.Build(roster, Catalogue(warriors, cryptek, lokhust));
        var warUnit = battle.Units.Single(u => u.Parts.Count == 2);
        var loneLokhust = battle.Units.Single(u => u.Primary.Datasheet.Id == "lokhust");
        var warriorsPart = warUnit.Parts.Single(p => p.Datasheet.Id == "necron-warriors");
        var crypPart = warUnit.Parts.Single(p => p.Datasheet.Id == "plasmancer");

        // Unit-scoped: every model in the Warriors unit benefits — including the attached Cryptek.
        Assert.Contains("Assault", battle.GrantedWeaponAbilities(warUnit, warriorsPart, ranged: true), StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Assault", battle.GrantedWeaponAbilities(warUnit, crypPart, ranged: true), StringComparer.OrdinalIgnoreCase);
        // A unit that is neither Warriors nor Immortals gets nothing.
        Assert.Empty(battle.GrantedWeaponAbilities(loneLokhust, loneLokhust.Primary, ranged: true));
    }

    // ---------- Leader-conferred effects (weapon abilities / unit abilities / stat buffs) ----------

    private static Datasheet LeaderSheet(string id, string name, string wounds, params ConferredEffect[] conferrals)
    {
        var sheet = Sheet(id, name, wounds);
        sheet.LeaderConferrals = conferrals.ToList();
        return sheet;
    }

    [Fact]
    public void Attached_leader_confers_weapon_ability_to_led_units_matching_weapons()
    {
        var lord = LeaderSheet("skorpekh-lord", "Skorpekh Lord", "7",
            new ConferredEffect
            {
                SourceAbility = "United In Destruction",
                WeaponClass = WeaponClass.Melee,
                WeaponAbilities = ["Lethal Hits"],
            });
        var destroyers = Sheet("skorpekh-destroyers", "Skorpekh Destroyers", wounds: "3",
            weapons:
            [
                new WeaponProfile { Name = "Skorpekh hyperphase weapons", Type = "Melee" },
                new WeaponProfile { Name = "Plasmacyte", Type = "Ranged" },
            ]);

        var roster = new Roster
        {
            Units =
            [
                Unit("u1", "skorpekh-lord", attachedTo: "u2"),
                Unit("u2", "skorpekh-destroyers", models: 3),
            ],
        };

        var battle = BattleRoster.Build(roster, Catalogue(lord, destroyers));
        var group = Assert.Single(battle.Units);
        var destroyersPart = group.Parts.Single(p => p.Datasheet.Id == "skorpekh-destroyers");

        // Melee weapons of the led unit gain [LETHAL HITS]; ranged weapons do not.
        Assert.Contains("Lethal Hits", battle.GrantedWeaponAbilities(group, destroyersPart, ranged: false), StringComparer.OrdinalIgnoreCase);
        Assert.Empty(battle.GrantedWeaponAbilities(group, destroyersPart, ranged: true));
        // The source ability is replaced by a short "Applied: …" note instead of full text.
        Assert.Equal("Lethal Hits on melee weapons", group.AppliedSummaryFor("United In Destruction"));
    }

    [Fact]
    public void Standalone_leader_confers_nothing()
    {
        var lord = LeaderSheet("skorpekh-lord", "Skorpekh Lord", "7",
            new ConferredEffect
            {
                SourceAbility = "United In Destruction",
                WeaponClass = WeaponClass.Melee,
                WeaponAbilities = ["Lethal Hits"],
            });
        lord.Weapons = [new WeaponProfile { Name = "Hyperphase harvester", Type = "Melee" }];
        var roster = new Roster { Units = [Unit("u1", "skorpekh-lord")] }; // not attached to anyone

        var battle = BattleRoster.Build(roster, Catalogue(lord));
        var group = Assert.Single(battle.Units);

        Assert.Empty(battle.GrantedWeaponAbilities(group, group.Primary, ranged: false));
        Assert.Null(group.AppliedSummaryFor("United In Destruction"));
    }

    [Fact]
    public void Attached_leader_confers_unit_ability_with_applied_summary()
    {
        var techno = LeaderSheet("technomancer", "Technomancer", "4",
            new ConferredEffect { SourceAbility = "Rites of Reanimation", UnitAbilities = ["Feel No Pain 5+"] });
        var warriors = Sheet("necron-warriors", "Necron Warriors", wounds: "1");

        var roster = new Roster
        {
            Units = [Unit("u1", "technomancer", attachedTo: "u2"), Unit("u2", "necron-warriors", models: 10)],
        };

        var battle = BattleRoster.Build(roster, Catalogue(techno, warriors));
        var group = Assert.Single(battle.Units);

        Assert.Contains("Feel No Pain 5+", battle.ConferredUnitAbilities(group));
        Assert.Equal("Feel No Pain 5+", group.AppliedSummaryFor("Rites of Reanimation"));
    }

    [Fact]
    public void Attached_leader_can_confer_a_hit_roll_modifier_to_weapons()
    {
        var lord = LeaderSheet("overlord", "Overlord", "5",
            new ConferredEffect
            {
                SourceAbility = "Awakened Command",
                StatModifiers = [new StatModifier { Target = StatTarget.Skill, Delta = 1, WeaponClass = WeaponClass.Any }],
            });
        var warriors = Sheet("necron-warriors", "Necron Warriors", wounds: "1",
            weapons: [new WeaponProfile { Name = "Gauss flayer", Type = "Ranged", Skill = "4+" }]);

        var roster = new Roster
        {
            Units = [Unit("o", "overlord", attachedTo: "w"), Unit("w", "necron-warriors", models: 10)],
        };

        var battle = BattleRoster.Build(roster, Catalogue(lord, warriors));
        var group = Assert.Single(battle.Units);
        var warriorsPart = group.Parts.Single(p => p.Datasheet.Id == "necron-warriors");

        var mods = battle.WeaponStatModifiers(group, warriorsPart, ranged: true);
        Assert.Equal("3+", StatMath.ApplyAll("4+", mods)); // +1 to Hit: 4+ → 3+
        Assert.Equal("+1 to Hit", group.AppliedSummaryFor("Awakened Command"));
    }

    // ---------- Detachment numeric stat buffs (the "Awaken Dynasty +1 to Hit" pattern) ----------

    private static Detachment HitBuffDetachment(bool requiresLeader) => new()
    {
        Id = "awakened-dynasty",
        Name = "Awakened Dynasty",
        StatBuffs =
        [
            new DetachmentStatBuff
            {
                Scope = GrantScope.Unit,
                RequiresAttachedLeader = requiresLeader,
                Modifier = new StatModifier { Target = StatTarget.Skill, Delta = 1, WeaponClass = WeaponClass.Any },
            },
        ],
    };

    [Fact]
    public void Detachment_stat_buff_improves_a_led_units_hit_roll()
    {
        var warriors = Sheet("necron-warriors", "Necron Warriors", wounds: "1",
            weapons: [new WeaponProfile { Name = "Gauss flayer", Type = "Ranged", Skill = "4+" }]);
        var overlord = Sheet("overlord", "Overlord", wounds: "5");

        var roster = new Roster
        {
            Units = [Unit("o", "overlord", attachedTo: "w"), Unit("w", "necron-warriors", models: 10)],
        };

        var battle = BattleRoster.Build(roster, Catalogue(warriors, overlord), [HitBuffDetachment(requiresLeader: true)]);
        var group = Assert.Single(battle.Units);
        var warriorsPart = group.Parts.Single(p => p.Datasheet.Id == "necron-warriors");

        var mods = battle.WeaponStatModifiers(group, warriorsPart, ranged: true);
        var mod = Assert.Single(mods);
        Assert.Equal(StatTarget.Skill, mod.Target);
        Assert.Equal("3+", StatMath.ApplyAll("4+", mods)); // 4+ → 3+ for the whole led unit
    }

    [Fact]
    public void Detachment_stat_buff_requiring_a_leader_skips_an_unled_unit()
    {
        var warriors = Sheet("necron-warriors", "Necron Warriors", wounds: "1",
            weapons: [new WeaponProfile { Name = "Gauss flayer", Type = "Ranged", Skill = "4+" }]);

        var roster = new Roster { Units = [Unit("w", "necron-warriors", models: 10)] }; // no leader attached

        var battle = BattleRoster.Build(roster, Catalogue(warriors), [HitBuffDetachment(requiresLeader: true)]);
        var group = Assert.Single(battle.Units);

        Assert.Empty(battle.WeaponStatModifiers(group, group.Primary, ranged: true));
    }

    // ---------- Real-seed end-to-end: the actual Skorpekh Lord (user's scenario) ----------

    [Fact]
    public void Real_seed_Skorpekh_Lord_confers_Lethal_Hits_when_leading_Destroyers()
    {
        var catalogue = CatalogueProvider.LoadEmbedded();
        var lord = catalogue.Datasheets.Single(d => d.Name == "Skorpekh Lord");
        var destroyers = catalogue.Datasheets.Single(d => d.Name == "Skorpekh Destroyers");

        var roster = new Roster
        {
            Units =
            [
                Unit("u1", lord.Id, attachedTo: "u2"),
                Unit("u2", destroyers.Id, models: 3),
            ],
        };

        var battle = BattleRoster.Build(roster, catalogue);
        var group = Assert.Single(battle.Units);
        var destroyersPart = group.Parts.Single(p => p.Datasheet.Id == destroyers.Id);

        Assert.Contains("Lethal Hits", battle.GrantedWeaponAbilities(group, destroyersPart, ranged: false), StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Lethal Hits on melee weapons", group.AppliedSummaryFor("United In Destruction"));
    }

    // ---------- Setup-assigned Enhancements surface in Play Mode as abilities / stat changes ----------

    private static Detachment EnhancementDetachment(Enhancement enhancement) => new()
    {
        Id = "test-detachment",
        Name = "Test Detachment",
        Enhancements = [enhancement],
    };

    private static RosterUnit Bearer(string id, string datasheetId, string enhancementId, int models = 1, string? attachedTo = null)
    {
        var unit = Unit(id, datasheetId, models: models, attachedTo: attachedTo);
        unit.AssignedEnhancementId = enhancementId;
        return unit;
    }

    [Fact]
    public void Assigned_text_enhancement_surfaces_as_an_ability_on_the_bearer()
    {
        var overlord = Sheet("overlord", "Overlord", wounds: "4");
        var detachment = EnhancementDetachment(new Enhancement
        {
            Id = "dread-majesty", Name = "Dread Majesty", Points = 30,
            Text = "In your Command phase, this model's terrifying presence intensifies.",
        });
        var roster = new Roster
        {
            DetachmentIds = ["test-detachment"],
            Units = [Bearer("u1", "overlord", "dread-majesty")],
        };

        var group = Assert.Single(BattleRoster.Build(roster, Catalogue(overlord), [detachment]).Units);

        Assert.Same(detachment.Enhancements[0], group.Primary.Enhancement);
        var enh = Assert.Single(group.CombinedAbilities, a => a.IsEnhancement);
        Assert.Equal("Dread Majesty", enh.Ability.Name);
        Assert.Equal("Overlord", enh.Source);
        Assert.Null(enh.AppliedSummary); // text enhancement → shown as prose, not an "Applied" stat note
    }

    [Fact]
    public void Stat_enhancement_buffs_the_bearer_statline_and_shows_an_applied_summary()
    {
        var overlord = Sheet("overlord", "Overlord", wounds: "4");
        var detachment = EnhancementDetachment(new Enhancement
        {
            Id = "sempiternal-weave", Name = "Sempiternal Weave", Points = 15,
            StatModifiers = [new StatModifier { Target = StatTarget.Wounds, Delta = 1 }],
        });
        var roster = new Roster
        {
            DetachmentIds = ["test-detachment"],
            Units = [Bearer("u1", "overlord", "sempiternal-weave")],
        };

        var battle = BattleRoster.Build(roster, Catalogue(overlord), [detachment]);
        var group = Assert.Single(battle.Units);

        Assert.Contains(battle.UnitStatModifiers(group, group.Primary), m => m.Target == StatTarget.Wounds && m.Delta == 1);
        var enh = Assert.Single(group.CombinedAbilities, a => a.IsEnhancement);
        Assert.False(string.IsNullOrEmpty(enh.AppliedSummary)); // stat enhancement → "Applied: …"
    }

    [Fact]
    public void Weapon_enhancement_buffs_only_the_bearers_matching_weapons()
    {
        var overlord = Sheet("overlord", "Overlord", wounds: "4",
            weapons: [new WeaponProfile { Name = "Overlord's blade", Type = "Melee", Attacks = "4" }]);
        var warriors = Sheet("necron-warriors", "Necron Warriors", wounds: "1",
            weapons: [new WeaponProfile { Name = "Gauss flayer", Type = "Ranged" }]);
        var detachment = EnhancementDetachment(new Enhancement
        {
            Id = "honed-edge", Name = "Honed Edge", Points = 10,
            StatModifiers = [new StatModifier { Target = StatTarget.Attacks, Delta = 1, WeaponClass = WeaponClass.Melee }],
        });
        var roster = new Roster
        {
            DetachmentIds = ["test-detachment"],
            Units =
            [
                Bearer("ov", "overlord", "honed-edge", attachedTo: "wa"),
                Unit("wa", "necron-warriors", models: 10),
            ],
        };

        var battle = BattleRoster.Build(roster, Catalogue(overlord, warriors), [detachment]);
        var group = Assert.Single(battle.Units);
        var bearer = group.Parts.Single(p => p.Datasheet.Id == "overlord");

        Assert.Contains(battle.WeaponStatModifiers(group, bearer, ranged: false), m => m.Target == StatTarget.Attacks);
        Assert.Empty(battle.WeaponStatModifiers(group, bearer, ranged: true));        // melee-only buff
        Assert.Empty(battle.WeaponStatModifiers(group, group.Primary, ranged: false)); // bodyguard isn't the bearer
    }

    [Fact]
    public void Unit_wide_enhancement_buffs_every_models_ranged_weapons()
    {
        var overlord = Sheet("overlord", "Overlord", wounds: "4",
            weapons: [new WeaponProfile { Name = "Tachyon arrow", Type = "Ranged", Range = "48\"" }]);
        var warriors = Sheet("necron-warriors", "Necron Warriors", wounds: "1",
            weapons:
            [
                new WeaponProfile { Name = "Gauss flayer", Type = "Ranged", Range = "24\"" },
                new WeaponProfile { Name = "Close combat weapon", Type = "Melee", Range = "Melee" },
            ]);
        var detachment = EnhancementDetachment(new Enhancement
        {
            Id = "gauntlet", Name = "Gauntlet of Compression", Points = 20,
            AffectsWholeUnit = true,
            StatModifiers = [new StatModifier { Target = StatTarget.Range, Delta = 6, WeaponClass = WeaponClass.Ranged }],
        });
        var roster = new Roster
        {
            DetachmentIds = ["test-detachment"],
            Units =
            [
                Bearer("ov", "overlord", "gauntlet", attachedTo: "wa"),
                Unit("wa", "necron-warriors", models: 10),
            ],
        };

        var battle = BattleRoster.Build(roster, Catalogue(overlord, warriors), [detachment]);
        var group = Assert.Single(battle.Units);
        var bearer = group.Parts.Single(p => p.Datasheet.Id == "overlord");
        var bodyguard = group.Primary; // Necron Warriors

        // The +6" Range reaches the bearer AND the rest of the unit's ranged weapons (unit-wide).
        Assert.Contains(battle.WeaponStatModifiers(group, bearer, ranged: true), m => m.Target == StatTarget.Range);
        Assert.Contains(battle.WeaponStatModifiers(group, bodyguard, ranged: true), m => m.Target == StatTarget.Range);
        Assert.Equal("30\"", StatMath.ApplyAll("24\"", battle.WeaponStatModifiers(group, bodyguard, ranged: true)));
        // It never touches melee weapons.
        Assert.Empty(battle.WeaponStatModifiers(group, bodyguard, ranged: false));
    }

    [Fact]
    public void Active_ability_count_includes_phase_text_abilities_and_excludes_passives()
    {
        var imotekh = Sheet("imotekh", "Imotekh", wounds: "6", abilities:
        [
            new Ability { Name = "Grand Strategist", Text = "At the start of your Command phase, you gain 1CP." },
            new Ability { Name = "Invulnerable Save", Text = "This model has a 4+ invulnerable save." },
        ]);
        var roster = new Roster { Units = [Unit("u1", "imotekh")] };

        var group = Assert.Single(BattleRoster.Build(roster, Catalogue(imotekh)).Units);
        var grand = group.CombinedAbilities.Single(a => a.Ability.Name == "Grand Strategist");

        Assert.True(BattleUnit.IsAbilityActiveInPhase(grand, BattlePhase.Command));
        Assert.False(BattleUnit.IsAbilityActiveInPhase(grand, BattlePhase.Shooting));
        Assert.Equal(1, group.ActiveAbilityCount(BattlePhase.Command)); // passive invuln save is not counted
        Assert.Equal(0, group.ActiveAbilityCount(BattlePhase.Fight));
    }

    [Fact]
    public void Stat_enhancement_is_never_phase_marked_even_when_its_text_names_a_phase()
    {
        var overlord = Sheet("overlord", "Overlord", wounds: "4");
        var detachment = EnhancementDetachment(new Enhancement
        {
            Id = "phylactery", Name = "Veil Phylactery", Points = 10,
            Text = "In your Command phase, this model regains a lost wound.",
            StatModifiers = [new StatModifier { Target = StatTarget.Wounds, Delta = 1 }],
        });
        var roster = new Roster
        {
            DetachmentIds = ["test-detachment"],
            Units = [Bearer("u1", "overlord", "phylactery")],
        };

        var group = Assert.Single(BattleRoster.Build(roster, Catalogue(overlord), [detachment]).Units);

        // It changes stats (always-on), so it is shown via the stat line, not the per-phase "usable now" markers.
        Assert.Equal(0, group.ActiveAbilityCount(BattlePhase.Command));
    }
}
