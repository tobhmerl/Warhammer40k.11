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
        Make("Hand of the Dynasty"),
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
                RequiresModelKeyword = "Cryptek",
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

    private static Detachment MakePantheon(string name)
    {
        var d = Make(name);
        d.AppliesPantheonBindings = true;
        return d;
    }
}
