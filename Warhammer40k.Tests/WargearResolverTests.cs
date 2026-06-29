using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Play;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Tests;

/// <summary>
/// Pins <see cref="WargearResolver.SelectedWeapons"/> against the real loadout shapes in the seed:
/// clean 1:1 options (Necron Warriors), compound option labels that map to several weapons (Overlord,
/// Canoptek Wraiths), always-on weapons that survive any selection, and the never-empty fallback.
/// </summary>
public class WargearResolverTests
{
    private static WeaponProfile W(string name, string type = "Ranged") => new() { Name = name, Type = type };

    private static Datasheet Warriors() => new()
    {
        Id = "necron-warriors",
        Name = "Necron Warriors",
        Weapons =
        [
            W("Close combat weapon", "Melee"),
            W("Gauss flayer"),
            W("Gauss reaper"),
        ],
        WargearGroups =
        [
            new WargearGroup
            {
                Id = "g", Name = "Weapon", Min = 0, Max = 1,
                Options = [new() { Id = "gauss-flayer", Name = "Gauss flayer" }, new() { Id = "gauss-reaper", Name = "Gauss reaper" }],
            },
        ],
    };

    private static RosterUnit Unit(string datasheetId, params (string Group, string[] Options)[] picks) => new()
    {
        Id = "u1",
        DatasheetId = datasheetId,
        ModelCount = 10,
        Wargear = picks.Select(p => new WargearSelection { GroupId = p.Group, OptionIds = p.Options.ToList() }).ToList(),
    };

    [Fact]
    public void Selecting_one_option_drops_the_other_keeps_always_on()
    {
        var sheet = Warriors();
        var unit = Unit("necron-warriors", ("g", ["gauss-flayer"]));

        var names = WargearResolver.SelectedWeapons(sheet, unit).Select(w => w.Name).ToList();

        Assert.Contains("Gauss flayer", names);
        Assert.Contains("Close combat weapon", names); // always-on survives
        Assert.DoesNotContain("Gauss reaper", names);   // unselected optional dropped
    }

    [Fact]
    public void No_selection_returns_full_loadout()
    {
        var sheet = Warriors();
        var unit = Unit("necron-warriors"); // no wargear picks

        var names = WargearResolver.SelectedWeapons(sheet, unit).Select(w => w.Name).ToList();

        Assert.Equal(3, names.Count); // flayer + reaper + ccw all shown (never empty)
    }

    [Fact]
    public void Compound_option_label_maps_to_multiple_weapons()
    {
        // Overlord: "Overlord's blade and tachyon arrow" should bring in BOTH weapons.
        var sheet = new Datasheet
        {
            Id = "overlord",
            Name = "Overlord",
            Weapons =
            [
                W("Staff of light"),
                W("Voidscythe", "Melee"),
                W("Overlord's blade", "Melee"),
                W("Tachyon arrow"),
            ],
            WargearGroups =
            [
                new WargearGroup
                {
                    Id = "g", Name = "Weapons", Min = 0, Max = 1,
                    Options =
                    [
                        new() { Id = "staff-of-light", Name = "Staff of light" },
                        new() { Id = "voidscythe", Name = "Voidscythe" },
                        new() { Id = "overlords-blade-and-tachyon-arrow", Name = "Overlord's blade and tachyon arrow" },
                    ],
                },
            ],
        };
        var unit = Unit("overlord", ("g", ["overlords-blade-and-tachyon-arrow"]));

        var names = WargearResolver.SelectedWeapons(sheet, unit).Select(w => w.Name).ToList();

        Assert.Contains("Overlord's blade", names);
        Assert.Contains("Tachyon arrow", names);
        Assert.DoesNotContain("Staff of light", names);
        Assert.DoesNotContain("Voidscythe", names);
    }

    [Fact]
    public void Token_split_matches_weapon_by_substring_token()
    {
        // Canoptek Wraiths: "Claws and beamer" → "Vicious claws" + "Transdimensional beamer".
        var sheet = new Datasheet
        {
            Id = "canoptek-wraiths",
            Name = "Canoptek Wraiths",
            Weapons =
            [
                W("Vicious claws", "Melee"),
                W("Transdimensional beamer"),
                W("Particle caster"),
                W("Whip coils", "Melee"),
            ],
            WargearGroups =
            [
                new WargearGroup
                {
                    Id = "g", Name = "Weapon", Min = 0, Max = 1,
                    Options =
                    [
                        new() { Id = "vicious-claws", Name = "Vicious claws" },
                        new() { Id = "claws-and-beamer", Name = "Claws and beamer" },
                        new() { Id = "claws-and-particle-caster", Name = "Claws and particle caster" },
                    ],
                },
            ],
        };
        var unit = Unit("canoptek-wraiths", ("g", ["claws-and-beamer"]));

        var names = WargearResolver.SelectedWeapons(sheet, unit).Select(w => w.Name).ToList();

        Assert.Contains("Vicious claws", names);
        Assert.Contains("Transdimensional beamer", names);
        Assert.DoesNotContain("Particle caster", names);
    }

    [Fact]
    public void Datasheet_with_no_weapons_returns_empty()
    {
        var sheet = new Datasheet { Id = "x", Name = "X" };
        Assert.Empty(WargearResolver.SelectedWeapons(sheet, Unit("x")));
    }

    // ---- Per-model loadouts (Lokhust Heavy Destroyers: each model takes Gauss or Enmitic) ----

    private static Datasheet LokhustHeavy() => new()
    {
        Id = "lokhust-heavy-destroyers",
        Name = "Lokhust Heavy Destroyers",
        Weapons =
        [
            W("Close combat weapon", "Melee"),
            W("Enmitic exterminator"),
            W("Gauss destructor"),
        ],
        WargearGroups =
        [
            new WargearGroup
            {
                Id = "weapon", Name = "Weapon", PerModel = true,
                Options = [new() { Id = "gauss-destructor", Name = "Gauss destructor" }, new() { Id = "enmitic-exterminator", Name = "Enmitic exterminator" }],
            },
        ],
    };

    private static RosterUnit PerModelUnit(int models, params (string OptionId, int Count)[] counts) => new()
    {
        Id = "u1",
        DatasheetId = "lokhust-heavy-destroyers",
        ModelCount = models,
        Wargear = counts.Length == 0 ? [] :
        [
            new WargearSelection
            {
                GroupId = "weapon",
                Counts = counts.Select(c => new WargearOptionCount { OptionId = c.OptionId, Models = c.Count }).ToList(),
            },
        ],
    };

    private static Dictionary<string, int> Carry(Datasheet sheet, RosterUnit unit, int? live = null) =>
        WargearResolver.ResolveWeapons(sheet, unit, live).ToDictionary(r => r.Weapon.Name, r => r.Models, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Per_model_default_is_all_of_the_first_option()
    {
        var carry = Carry(LokhustHeavy(), PerModelUnit(3));

        Assert.Equal(3, carry["Gauss destructor"]);              // first option = default, takes every model
        Assert.False(carry.ContainsKey("Enmitic exterminator")); // 0 carriers → omitted
        Assert.Equal(3, carry["Close combat weapon"]);           // always-on, every model
    }

    [Fact]
    public void Per_model_two_gauss_one_enmitic()
    {
        var carry = Carry(LokhustHeavy(), PerModelUnit(3, ("enmitic-exterminator", 1)));

        Assert.Equal(2, carry["Gauss destructor"]);   // default absorbs the remainder
        Assert.Equal(1, carry["Enmitic exterminator"]);
        Assert.Equal(3, carry["Close combat weapon"]);
    }

    [Fact]
    public void Per_model_one_gauss_two_enmitic()
    {
        var carry = Carry(LokhustHeavy(), PerModelUnit(3, ("enmitic-exterminator", 2)));

        Assert.Equal(1, carry["Gauss destructor"]);
        Assert.Equal(2, carry["Enmitic exterminator"]);
    }

    [Fact]
    public void Per_model_all_enmitic_drops_the_default()
    {
        var carry = Carry(LokhustHeavy(), PerModelUnit(3, ("enmitic-exterminator", 3)));

        Assert.False(carry.ContainsKey("Gauss destructor")); // 0 carriers → omitted
        Assert.Equal(3, carry["Enmitic exterminator"]);
    }

    [Fact]
    public void Per_model_count_over_the_unit_size_is_clamped()
    {
        var carry = Carry(LokhustHeavy(), PerModelUnit(3, ("enmitic-exterminator", 5)));

        Assert.Equal(3, carry["Enmitic exterminator"]);      // clamped to the unit size
        Assert.False(carry.ContainsKey("Gauss destructor"));
    }

    [Fact]
    public void Per_model_casualties_reduce_the_default_first()
    {
        var sheet = LokhustHeavy();
        var unit = PerModelUnit(3, ("enmitic-exterminator", 1)); // 2 Gauss + 1 Enmitic at full strength

        var two = Carry(sheet, unit, live: 2);
        Assert.Equal(1, two["Gauss destructor"]);      // a Gauss model fell first
        Assert.Equal(1, two["Enmitic exterminator"]);
        Assert.Equal(2, two["Close combat weapon"]);   // always-on tracks the live models

        var one = Carry(sheet, unit, live: 1);
        Assert.False(one.ContainsKey("Gauss destructor")); // both Gauss gone
        Assert.Equal(1, one["Enmitic exterminator"]);
        Assert.Equal(1, one["Close combat weapon"]);
    }
}
