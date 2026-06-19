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
}
