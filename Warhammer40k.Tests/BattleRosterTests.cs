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
}
