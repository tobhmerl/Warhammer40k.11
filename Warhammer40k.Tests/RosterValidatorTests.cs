using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Rosters;
using Warhammer40k.Core.Rosters.Validation;
using Warhammer40k.Core.Rosters.Validation.Rules;

namespace Warhammer40k.Tests;

/// <summary>
/// Per-rule unit tests for the pure validation engine (§4 R1–R11) using small synthetic catalogues so each
/// derived flag is controlled exactly. The §6 worked example against the real seed lives in
/// <see cref="RosterWorkedExampleTests"/>.
/// </summary>
public class RosterValidatorTests
{
    private const string ValidDetachment = "hand-of-the-dynasty";

    // ---------- R1: points limit ----------

    [Fact]
    public void R1_flags_when_total_exceeds_limit()
    {
        var cat = Cat(Sheet("warriors", "Necron Warriors", models: 10, points: 90));
        var roster = Roster(ValidDetachment, limit: 100, Unit("warriors", 10), Unit("warriors", 10)); // 180 > 100

        var messages = Run(new PointsLimitRule(), roster, cat);

        var error = Assert.Single(messages);
        Assert.Equal("R1", error.RuleId);
        Assert.Equal(ValidationSeverity.Error, error.Severity);
    }

    [Fact]
    public void R1_passes_when_total_within_limit()
    {
        var cat = Cat(Sheet("warriors", "Necron Warriors", models: 10, points: 90));
        var roster = Roster(ValidDetachment, limit: 100, Unit("warriors", 10));

        Assert.Empty(Run(new PointsLimitRule(), roster, cat));
    }

    // ---------- R2: one detachment ----------

    [Fact]
    public void R2_flags_missing_detachment()
    {
        var roster = Roster(detachmentId: "", limit: 2000);
        Assert.Single(Run(new DetachmentSelectionRule(), roster, Cat()));
    }

    [Fact]
    public void R2_flags_unknown_detachment()
    {
        var roster = Roster(detachmentId: "not-a-detachment", limit: 2000);
        Assert.Single(Run(new DetachmentSelectionRule(), roster, Cat()));
    }

    [Fact]
    public void R2_passes_for_known_detachment()
    {
        var roster = Roster(ValidDetachment, limit: 2000);
        Assert.Empty(Run(new DetachmentSelectionRule(), roster, Cat()));
    }

    [Fact]
    public void R2_flags_detachments_over_the_DP_budget()
    {
        // Two 2-DP detachments = 4 DP, but only 3 are available at 2000 pts.
        var roster = Roster("", limit: 2000);
        roster.DetachmentIds = ["cryptek-conclave", "cryptek-conclave"];
        var error = Assert.Single(Run(new DetachmentSelectionRule(), roster, Cat()));
        Assert.Contains("DP", error.Text);
    }

    // ---------- R3: copy limits ----------

    [Fact]
    public void R3_flags_over_cap_and_passes_at_cap()
    {
        var cat = Cat(Sheet("immortals", "Immortals", configure: d => d.MaxCopies = 6));

        var atCap = Roster(ValidDetachment, 2000, Enumerable.Range(0, 6).Select(_ => Unit("immortals")).ToArray());
        Assert.Empty(Run(new CopyLimitRule(), atCap, cat));

        var overCap = Roster(ValidDetachment, 2000, Enumerable.Range(0, 7).Select(_ => Unit("immortals")).ToArray());
        var error = Assert.Single(Run(new CopyLimitRule(), overCap, cat));
        Assert.Equal("R3", error.RuleId);
    }

    [Fact]
    public void R3_ignores_epic_heroes_those_belong_to_R4()
    {
        var cat = Cat(Sheet("silent-king", "The Silent King", configure: d => { d.IsEpicHero = true; d.MaxCopies = 1; }));
        var roster = Roster(ValidDetachment, 2000, Unit("silent-king"), Unit("silent-king"));

        Assert.Empty(Run(new CopyLimitRule(), roster, cat));
    }

    // ---------- R4: epic heroes 0-1 ----------

    [Fact]
    public void R4_flags_duplicate_epic_hero()
    {
        var cat = Cat(Sheet("silent-king", "The Silent King", configure: d => { d.IsEpicHero = true; d.MaxCopies = 1; }));
        var roster = Roster(ValidDetachment, 2000, Unit("silent-king"), Unit("silent-king"));

        var error = Assert.Single(Run(new EpicHeroRule(), roster, cat));
        Assert.Equal("R4", error.RuleId);
        Assert.Contains("Silent King", error.Text);
    }

    [Fact]
    public void R4_passes_for_single_epic_hero()
    {
        var cat = Cat(Sheet("silent-king", "The Silent King", configure: d => { d.IsEpicHero = true; d.MaxCopies = 1; }));
        var roster = Roster(ValidDetachment, 2000, Unit("silent-king"));

        Assert.Empty(Run(new EpicHeroRule(), roster, cat));
    }

    // ---------- R5: warlord ----------

    [Fact]
    public void R5_flags_no_warlord_when_roster_has_units()
    {
        var cat = Cat(Sheet("overlord", "Overlord", configure: Character));
        var roster = Roster(ValidDetachment, 2000, Unit("overlord"));

        Assert.Single(Run(new WarlordRule(), roster, cat));
    }

    [Fact]
    public void R5_flags_multiple_warlords()
    {
        var cat = Cat(Sheet("overlord", "Overlord", configure: Character));
        var roster = Roster(ValidDetachment, 2000,
            Unit("overlord", configure: u => u.IsWarlord = true),
            Unit("overlord", configure: u => u.IsWarlord = true));

        Assert.Contains(Run(new WarlordRule(), roster, cat), m => m.Text.Contains("Only one Warlord"));
    }

    [Fact]
    public void R5_blocks_ineligible_warlord_like_ctan()
    {
        var cat = Cat(Sheet("ctan", "C'tan Shard", configure: d => { d.IsCharacter = true; d.WarlordEligible = false; }));
        var roster = Roster(ValidDetachment, 2000, Unit("ctan", configure: u => u.IsWarlord = true));

        var error = Assert.Single(Run(new WarlordRule(), roster, cat));
        Assert.Contains("not eligible", error.Text);
    }

    [Fact]
    public void R5_passes_for_single_eligible_warlord()
    {
        var cat = Cat(Sheet("overlord", "Overlord", configure: Character));
        var roster = Roster(ValidDetachment, 2000, Unit("overlord", configure: u => u.IsWarlord = true));

        Assert.Empty(Run(new WarlordRule(), roster, cat));
    }

    // ---------- R6: enhancements ----------

    [Fact]
    public void R6_flags_more_than_three_enhancements()
    {
        var cat = Cat(Sheet("overlord", "Overlord", configure: Character));
        var roster = Roster(ValidDetachment, 2000,
            Unit("overlord", configure: u => u.AssignedEnhancementId = "e1"),
            Unit("overlord", configure: u => u.AssignedEnhancementId = "e2"),
            Unit("overlord", configure: u => u.AssignedEnhancementId = "e3"),
            Unit("overlord", configure: u => u.AssignedEnhancementId = "e4"));

        Assert.Contains(Run(new EnhancementRule(), roster, cat), m => m.Text.Contains("at most 3"));
    }

    [Fact]
    public void R6_flags_duplicate_enhancement()
    {
        var cat = Cat(Sheet("overlord", "Overlord", configure: Character));
        var roster = Roster(ValidDetachment, 2000,
            Unit("overlord", configure: u => u.AssignedEnhancementId = "same"),
            Unit("overlord", configure: u => u.AssignedEnhancementId = "same"));

        Assert.Contains(Run(new EnhancementRule(), roster, cat), m => m.Text.Contains("each may be taken once"));
    }

    [Fact]
    public void R6_blocks_enhancement_on_non_eligible_unit()
    {
        var cat = Cat(Sheet("ctan", "C'tan Shard", configure: d => { d.IsCharacter = true; d.CanTakeEnhancements = false; }));
        var roster = Roster(ValidDetachment, 2000, Unit("ctan", configure: u => u.AssignedEnhancementId = "e1"));

        var error = Assert.Single(Run(new EnhancementRule(), roster, cat));
        Assert.Contains("cannot be given an Enhancement", error.Text);
    }

    [Fact]
    public void R6_enforces_detachment_membership_when_enhancements_are_authored()
    {
        var cat = Cat(Sheet("overlord", "Overlord", configure: Character));
        var detachments = new List<Detachment>
        {
            new()
            {
                Id = "cryptek-conclave", Name = "Cryptek Conclave",
                Enhancements = [new Enhancement { Id = "quantum-abacus", Name = "Quantum Abacus", Points = 15 }],
            },
        };
        var roster = Roster("cryptek-conclave", 2000, Unit("overlord", configure: u => u.AssignedEnhancementId = "not-in-detachment"));

        var error = Assert.Single(Run(new EnhancementRule(), roster, cat, detachments));
        Assert.Contains("not part of your selected detachment", error.Text);
    }

    [Fact]
    public void R6_stays_permissive_when_detachment_has_no_authored_enhancements()
    {
        // Hand of the Dynasty has no 10th-MFM points yet, so its Enhancements list is empty (permissive).
        var cat = Cat(Sheet("overlord", "Overlord", configure: Character));
        var roster = Roster(ValidDetachment, 2000, Unit("overlord", configure: u => u.AssignedEnhancementId = "placeholder"));

        Assert.Empty(Run(new EnhancementRule(), roster, cat));
    }

    [Fact]
    public void R6_blocks_enhancement_when_required_keyword_is_missing()
    {
        var cat = Cat(Sheet("overlord", "Overlord", configure: Character)); // no "Cryptek" keyword
        var roster = Roster("cryptek-conclave", 2000, Unit("overlord", configure: u => u.AssignedEnhancementId = "abacus"));

        var error = Assert.Single(Run(new EnhancementRule(), roster, cat, CryptekOnly()));
        Assert.Contains("not eligible", error.Text);
    }

    [Fact]
    public void R6_allows_enhancement_when_required_keyword_is_present()
    {
        var cat = Cat(Sheet("chronomancer", "Chronomancer", configure: d => { Character(d); d.Keywords.Add("Cryptek"); }));
        var roster = Roster("cryptek-conclave", 2000, Unit("chronomancer", configure: u => u.AssignedEnhancementId = "abacus"));

        Assert.Empty(Run(new EnhancementRule(), roster, cat, CryptekOnly()));
    }

    [Fact]
    public void R6_blocks_enhancement_excluded_by_keyword()
    {
        var cat = Cat(Sheet("chronomancer", "Chronomancer", configure: d => { Character(d); d.Keywords.Add("Cryptek"); }));
        var detachments = new List<Detachment>
        {
            new()
            {
                Id = "cursed-legion", Name = "Cursed Legion",
                Enhancements =
                [
                    new Enhancement
                    {
                        Id = "circlet", Name = "Cursed Circlet", Points = 25,
                        Eligibility = new EnhancementEligibility { ExcludedKeywords = ["Cryptek"] },
                    },
                ],
            },
        };
        var roster = Roster("cursed-legion", 2000, Unit("chronomancer", configure: u => u.AssignedEnhancementId = "circlet"));

        Assert.Contains(Run(new EnhancementRule(), roster, cat, detachments), m => m.Text.Contains("not eligible"));
    }

    [Fact]
    public void Enhancement_IsAvailableTo_respects_required_and_excluded_keywords()
    {
        var cryptek = Sheet("c", "Cryptek Lord", configure: d => d.Keywords = ["Faction: Necrons", "Cryptek"]);
        var noble = Sheet("n", "Noble Lord", configure: d => d.Keywords = ["Faction: Necrons", "Noble"]);

        var requiresCryptek = new Enhancement { Eligibility = new EnhancementEligibility { RequiredKeywords = ["Cryptek"] } };
        var forbidsCryptek = new Enhancement { Eligibility = new EnhancementEligibility { ExcludedKeywords = ["Cryptek"] } };
        var unconstrained = new Enhancement();

        Assert.True(requiresCryptek.IsAvailableTo(cryptek));
        Assert.False(requiresCryptek.IsAvailableTo(noble));
        Assert.False(forbidsCryptek.IsAvailableTo(cryptek));
        Assert.True(forbidsCryptek.IsAvailableTo(noble));
        Assert.True(unconstrained.IsAvailableTo(noble));
        Assert.True(unconstrained.Eligibility.IsUnconstrained);
    }

    private static List<Detachment> CryptekOnly() =>
    [
        new()
        {
            Id = "cryptek-conclave", Name = "Cryptek Conclave",
            Enhancements =
            [
                new Enhancement
                {
                    Id = "abacus", Name = "Quantum Abacus", Points = 15,
                    Eligibility = new EnhancementEligibility { RequiredKeywords = ["Cryptek"] },
                },
            ],
        },
    ];

    // ---------- R7: leader attach ----------

    [Fact]
    public void R7_flags_attach_to_disallowed_target()
    {
        var cat = Cat(
            Sheet("overlord", "Overlord", configure: d => { Character(d); d.HasLeaderAbility = true; d.LeaderTargetIds = ["necron-warriors"]; }),
            Sheet("immortals", "Immortals"));
        var bodyguard = Unit("immortals");
        var leader = Unit("overlord", configure: u => u.AttachedToRosterUnitId = bodyguard.Id);
        var roster = Roster(ValidDetachment, 2000, bodyguard, leader);

        Assert.Contains(Run(new LeaderAttachRule(), roster, cat), m => m.Text.Contains("cannot be attached"));
    }

    [Fact]
    public void R7_passes_for_allowed_target()
    {
        var cat = Cat(
            Sheet("overlord", "Overlord", configure: d => { Character(d); d.HasLeaderAbility = true; d.LeaderTargetIds = ["immortals"]; }),
            Sheet("immortals", "Immortals"));
        var bodyguard = Unit("immortals");
        var leader = Unit("overlord", configure: u => u.AttachedToRosterUnitId = bodyguard.Id);
        var roster = Roster(ValidDetachment, 2000, bodyguard, leader);

        Assert.Empty(Run(new LeaderAttachRule(), roster, cat));
    }

    [Fact]
    public void R7_blocks_two_leaders_on_one_bodyguard_without_co_leader()
    {
        var cat = Cat(
            Sheet("overlord", "Overlord", configure: d => { Character(d); d.HasLeaderAbility = true; d.LeaderTargetIds = ["immortals"]; }),
            Sheet("immortals", "Immortals"));
        var bodyguard = Unit("immortals");
        var roster = Roster(ValidDetachment, 2000, bodyguard,
            Unit("overlord", configure: u => u.AttachedToRosterUnitId = bodyguard.Id),
            Unit("overlord", configure: u => u.AttachedToRosterUnitId = bodyguard.Id));

        Assert.Contains(Run(new LeaderAttachRule(), roster, cat), m => m.Text.Contains("more than one Leader"));
    }

    [Fact]
    public void R7_allows_co_leader()
    {
        var cat = Cat(
            Sheet("overlord", "Overlord", configure: d => { Character(d); d.HasLeaderAbility = true; d.LeaderTargetIds = ["immortals"]; }),
            Sheet("chronomancer", "Chronomancer", configure: d => { Character(d); d.HasLeaderAbility = true; d.AllowsCoLeader = true; d.LeaderTargetIds = ["immortals"]; }),
            Sheet("immortals", "Immortals"));
        var bodyguard = Unit("immortals");
        var roster = Roster(ValidDetachment, 2000, bodyguard,
            Unit("overlord", configure: u => u.AttachedToRosterUnitId = bodyguard.Id),
            Unit("chronomancer", configure: u => u.AttachedToRosterUnitId = bodyguard.Id));

        Assert.Empty(Run(new LeaderAttachRule(), roster, cat));
    }

    [Fact]
    public void R7_reports_unattached_leader_as_info()
    {
        var cat = Cat(Sheet("overlord", "Overlord", configure: d => { Character(d); d.HasLeaderAbility = true; }));
        var roster = Roster(ValidDetachment, 2000, Unit("overlord"));

        var info = Assert.Single(Run(new LeaderAttachRule(), roster, cat));
        Assert.Equal(ValidationSeverity.Info, info.Severity);
    }

    // ---------- R8: unit size ----------

    [Fact]
    public void R8_flags_invalid_size()
    {
        var cat = Cat(Sheet("immortals", "Immortals", models: 5, points: 70));
        var roster = Roster(ValidDetachment, 2000, Unit("immortals", models: 7)); // 7 not a legal size

        var error = Assert.Single(Run(new UnitSizeRule(), roster, cat));
        Assert.Equal("R8", error.RuleId);
    }

    [Fact]
    public void R8_passes_for_legal_size()
    {
        var cat = Cat(Sheet("immortals", "Immortals", models: 5, points: 70));
        var roster = Roster(ValidDetachment, 2000, Unit("immortals", models: 5));

        Assert.Empty(Run(new UnitSizeRule(), roster, cat));
    }

    [Fact]
    public void R8_requires_minimum_wargear_selection()
    {
        var cat = Cat(Sheet("destroyers", "Destroyers", models: 3, points: 100,
            configure: d => d.WargearGroups = [Group("g", min: 1, max: 1, "o1", "o2")]));
        var roster = Roster(ValidDetachment, 2000, Unit("destroyers", models: 3)); // no selection

        Assert.Contains(Run(new UnitSizeRule(), roster, cat), m => m.Text.Contains("at least 1"));
    }

    [Fact]
    public void R8_passes_with_valid_wargear_selection()
    {
        var cat = Cat(Sheet("destroyers", "Destroyers", models: 3, points: 100,
            configure: d => d.WargearGroups = [Group("g", min: 1, max: 1, "o1", "o2")]));
        var roster = Roster(ValidDetachment, 2000, Unit("destroyers", models: 3,
            configure: u => u.Wargear = [new WargearSelection { GroupId = "g", OptionIds = ["o1"] }]));

        Assert.Empty(Run(new UnitSizeRule(), roster, cat));
    }

    [Fact]
    public void R8_flags_too_many_wargear_options()
    {
        var cat = Cat(Sheet("destroyers", "Destroyers", models: 3, points: 100,
            configure: d => d.WargearGroups = [Group("g", min: 0, max: 1, "o1", "o2")]));
        var roster = Roster(ValidDetachment, 2000, Unit("destroyers", models: 3,
            configure: u => u.Wargear = [new WargearSelection { GroupId = "g", OptionIds = ["o1", "o2"] }]));

        Assert.Contains(Run(new UnitSizeRule(), roster, cat), m => m.Text.Contains("at most 1"));
    }

    [Fact]
    public void R8_flags_unknown_wargear_option()
    {
        var cat = Cat(Sheet("destroyers", "Destroyers", models: 3, points: 100,
            configure: d => d.WargearGroups = [Group("g", min: 0, max: 2, "o1")]));
        var roster = Roster(ValidDetachment, 2000, Unit("destroyers", models: 3,
            configure: u => u.Wargear = [new WargearSelection { GroupId = "g", OptionIds = ["nope"] }]));

        Assert.Contains(Run(new UnitSizeRule(), roster, cat), m => m.Text.Contains("unknown wargear"));
    }

    // ---------- R9: faction coherence ----------

    [Fact]
    public void R9_flags_non_necron_keyword()
    {
        var cat = Cat(Sheet("intruder", "Ork Boy", configure: d => d.Keywords = ["Faction: Orks"]));
        var roster = Roster(ValidDetachment, 2000, Unit("intruder"));

        Assert.Contains(Run(new FactionCoherenceRule(), roster, cat), m => m.Text.Contains("not a Necrons unit"));
    }

    [Fact]
    public void R9_flags_dangling_datasheet_reference()
    {
        var roster = Roster(ValidDetachment, 2000, Unit("ghost"));
        Assert.Contains(Run(new FactionCoherenceRule(), roster, Cat()), m => m.Text.Contains("unknown datasheet"));
    }

    [Fact]
    public void R9_passes_for_necron_unit()
    {
        var cat = Cat(Sheet("warriors", "Necron Warriors"));
        Assert.Empty(Run(new FactionCoherenceRule(), Roster(ValidDetachment, 2000, Unit("warriors")), cat));
    }

    // ---------- R10: Pantheon of Woe ----------

    [Fact]
    public void R10_inactive_for_non_pantheon_detachment()
    {
        var cat = MonsterCatalogue();
        var roster = Roster(ValidDetachment, 2000, Unit("ctan"));

        Assert.Empty(Run(new PantheonRule(), roster, cat));
    }

    [Fact]
    public void R10_flags_monster_without_applied_binding_under_pantheon()
    {
        var cat = MonsterCatalogue();
        var roster = Roster("pantheon-of-woe", 2000, Unit("ctan"));

        var error = Assert.Single(Run(new PantheonRule(), roster, cat));
        Assert.Equal("R10", error.RuleId);
        Assert.Contains("Necrodermal Binding", error.Text);
    }

    [Fact]
    public void R10_passes_after_applier_sets_binding()
    {
        var cat = MonsterCatalogue();
        var roster = Roster("pantheon-of-woe", 2000, Unit("ctan"));

        PantheonBindingApplier.Apply(roster, cat, DetachmentCatalogue.FindById("pantheon-of-woe"));

        Assert.Empty(Run(new PantheonRule(), roster, cat));
        Assert.Equal("Test Matrix", roster.Units[0].AppliedBindingId);
        Assert.Equal(50, roster.Units[0].BindingSurcharge);
    }

    // ---------- R11: battle size (none currently) ----------

    [Fact]
    public void R11_never_produces_messages()
    {
        var cat = Cat(Sheet("warriors", "Necron Warriors"));
        Assert.Empty(Run(new BattleSizeRule(), Roster(ValidDetachment, 2000, Unit("warriors")), cat));
    }

    // ---------- RosterCalculator ----------

    [Fact]
    public void Calculator_sums_units_enhancements_and_surcharges()
    {
        var cat = Cat(Sheet("overlord", "Overlord", models: 1, points: 85, configure: Character));
        var detachment = new Detachment
        {
            Id = "cryptek-conclave", Name = "Cryptek Conclave",
            Enhancements = [new Enhancement { Id = "quantum-abacus", Name = "Quantum Abacus", Points = 15 }],
        };
        var roster = Roster("cryptek-conclave", 2000,
            Unit("overlord", configure: u =>
            {
                u.AssignedEnhancementId = "quantum-abacus";
                u.BindingSurcharge = 10;
            }));

        Assert.Equal(110, RosterCalculator.TotalPoints(roster, cat, detachment)); // 85 + 15 + 10
    }

    // ---------- PantheonBindingApplier ----------

    [Fact]
    public void Applier_clears_bindings_when_detachment_is_not_pantheon()
    {
        var cat = MonsterCatalogue();
        var unit = Unit("ctan", configure: u => { u.AppliedBindingId = "Test Matrix"; u.BindingSurcharge = 50; });
        var roster = Roster(ValidDetachment, 2000, unit);

        PantheonBindingApplier.Apply(roster, cat, DetachmentCatalogue.FindById(ValidDetachment));

        Assert.Null(unit.AppliedBindingId);
        Assert.Equal(0, unit.BindingSurcharge);
    }

    [Fact]
    public void Applier_preserves_an_edited_surcharge_on_reapply()
    {
        var cat = MonsterCatalogue();
        var roster = Roster("pantheon-of-woe", 2000, Unit("ctan"));
        var pantheon = DetachmentCatalogue.FindById("pantheon-of-woe");

        PantheonBindingApplier.Apply(roster, cat, pantheon);
        roster.Units[0].BindingSurcharge = 99; // user edits the surcharge
        PantheonBindingApplier.Apply(roster, cat, pantheon);

        Assert.Equal(99, roster.Units[0].BindingSurcharge);
    }

    // ---------- RosterValidator composition ----------

    [Fact]
    public void DefaultRules_contains_R1_through_R11_in_order()
    {
        var ids = RosterValidator.DefaultRules().Select(r => r.Id).ToArray();
        Assert.Equal(new[] { "R1", "R2", "R3", "R4", "R5", "R6", "R7", "R8", "R9", "R10", "R11" }, ids);
    }

    [Fact]
    public void Validator_reports_total_points_and_aggregates_rule_messages()
    {
        var cat = Cat(Sheet("warriors", "Necron Warriors", models: 10, points: 90));
        var roster = Roster(detachmentId: "", limit: 50, Unit("warriors", 10)); // R1 over limit + R2 missing + R5 no warlord

        var result = new RosterValidator().Validate(roster, cat);

        Assert.Equal(90, result.TotalPoints);
        Assert.False(result.IsReady);
        Assert.True(result.HasMessageFrom("R1"));
        Assert.True(result.HasMessageFrom("R2"));
        Assert.True(result.HasMessageFrom("R5"));
    }

    // ---------- Helpers ----------

    private static void Character(Datasheet d)
    {
        d.IsCharacter = true;
        d.WarlordEligible = true;
        d.CanTakeEnhancements = true;
    }

    private static Datasheet Sheet(string id, string name, int models = 1, int points = 100, Action<Datasheet>? configure = null)
    {
        var d = new Datasheet
        {
            Id = id,
            Name = name,
            Keywords = ["Faction: Necrons"],
            PointsOptions = [new PointsOption { Models = models, Points = points }],
        };
        configure?.Invoke(d);
        return d;
    }

    private static CatalogueData Cat(params Datasheet[] sheets) => new()
    {
        Faction = "Necrons",
        Datasheets = sheets.ToList(),
    };

    private static WargearGroup Group(string id, int min, int max, params string[] optionIds) => new()
    {
        Id = id,
        Name = id,
        Min = min,
        Max = max,
        Options = optionIds.Select(o => new WargearOption { Id = o, Name = o }).ToList(),
    };

    private static CatalogueData MonsterCatalogue()
    {
        var cat = Cat(Sheet("ctan", "C'tan Test", configure: d => d.IsMonster = true));
        cat.PantheonBindings = [new PantheonBinding { Name = "Test Matrix", Unit = "C'tan Test", Points = 50 }];
        return cat;
    }

    private static RosterUnit Unit(string datasheetId, int models = 1, Action<RosterUnit>? configure = null)
    {
        var u = new RosterUnit
        {
            Id = Guid.NewGuid().ToString("n"),
            DatasheetId = datasheetId,
            ModelCount = models,
        };
        configure?.Invoke(u);
        return u;
    }

    private static Roster Roster(string detachmentId, int limit, params RosterUnit[] units) => new()
    {
        Name = "Test Roster",
        Faction = "Necrons",
        PointsLimit = limit,
        DetachmentId = detachmentId,
        Units = units.ToList(),
    };

    private static List<ValidationMessage> Run(IRosterRule rule, Roster roster, CatalogueData catalogue, IReadOnlyList<Detachment>? detachments = null) =>
        rule.Evaluate(new RosterValidationContext(roster, catalogue, detachments ?? DetachmentCatalogue.BuiltIn)).ToList();
}
