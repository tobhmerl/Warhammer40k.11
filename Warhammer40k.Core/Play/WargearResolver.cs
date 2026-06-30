using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Rosters;

namespace Warhammer40k.Core.Play;

/// <summary>
/// Resolves which of a datasheet's weapons are actually in play for a unit, given the wargear the player
/// chose in setup. Pure and deterministic.
/// </summary>
/// <remarks>
/// Matching model:
/// <list type="bullet">
/// <item><b>Always-on</b> weapons — not referenced by any wargear option (e.g. "Close combat weapon") —
/// are always included.</item>
/// <item><b>Optional</b> weapons — named by a wargear option — are included only when that option is
/// selected. Option labels can be compound ("Overlord's blade and tachyon arrow", "Claws and beamer"),
/// so each label is split on "and"/comma and every weapon whose name contains a token is included; tokens
/// that match no weapon (e.g. "dispersion shield") are simply ignored.</item>
/// <item>If the unit has <b>no</b> selections at all (older rosters predating weapon-pick), the full
/// loadout is returned so Play Mode is never empty.</item>
/// </list>
/// </remarks>
public static class WargearResolver
{
    private static readonly string[] Connectors = [" and ", ",", "&", "+"];

    /// <summary>
    /// The weapons in play for <paramref name="unit"/>, in datasheet order — a flat list without per-model
    /// counts. Equivalent to <see cref="ResolveWeapons"/> projected onto the weapon profiles.
    /// </summary>
    public static IReadOnlyList<WeaponProfile> SelectedWeapons(Datasheet datasheet, RosterUnit unit) =>
        ResolveWeapons(datasheet, unit).Select(r => r.Weapon).ToList();

    /// <summary>
    /// The weapons in play for <paramref name="unit"/>, each with the number of models carrying it, in
    /// datasheet order. Always-on weapons are carried by every model; an ordinary (toggle) option's weapons
    /// are carried by every model when selected; a <see cref="WargearGroup.PerModel"/> group distributes the
    /// unit's models across its options (the first option being the default that absorbs any unassigned
    /// models). Weapons carried by nobody are omitted. A unit with no selections at all keeps its full toggle
    /// loadout (back-compat with rosters predating weapon-pick).
    /// </summary>
    public static IReadOnlyList<ResolvedWeapon> ResolveWeapons(Datasheet datasheet, RosterUnit unit, int? liveModels = null)
    {
        ArgumentNullException.ThrowIfNull(datasheet);
        ArgumentNullException.ThrowIfNull(unit);

        if (datasheet.Weapons.Count == 0)
            return [];

        var models = Math.Max(0, liveModels ?? unit.ModelCount);

        // Weapon name → models carrying it (absent / 0 ⇒ not in play).
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var perModelNames = WeaponNamesForGroups(datasheet, static g => g.PerModel);
        var toggleNames = WeaponNamesForGroups(datasheet, static g => !g.PerModel);

        // 1) Always-on weapons (named by no option) are carried by every model.
        foreach (var weapon in datasheet.Weapons)
            if (!perModelNames.Contains(weapon.Name) && !toggleNames.Contains(weapon.Name))
                counts[weapon.Name] = models;

        // 2) Per-model groups distribute the unit's models across their options.
        foreach (var group in datasheet.WargearGroups.Where(g => g.PerModel))
            ApplyPerModelGroup(datasheet, group, FindSelection(unit, group.Id), models, counts);

        // 3) Toggle groups: a group's selected options are carried by every model; a group with NO selection of
        //    its own keeps its full loadout (never-empty back-compat). Evaluated PER GROUP so picking one group
        //    (e.g. a Lokhust Lord's Equipment amulet) never blanks another group's weapons.
        foreach (var group in datasheet.WargearGroups.Where(g => !g.PerModel))
        {
            var selection = FindSelection(unit, group.Id);
            var groupHasSelection = selection is not null && selection.OptionIds.Count > 0;
            ApplyToggleGroup(datasheet, group, selection, groupHasSelection, models, counts);
        }

        return datasheet.Weapons
            .Where(w => counts.TryGetValue(w.Name, out var c) && c > 0)
            .Select(w => new ResolvedWeapon(w, counts[w.Name]))
            .ToList();
    }

    /// <summary>
    /// Whether a datasheet ability is "in play" for <paramref name="unit"/> given its wargear. An ability whose
    /// name exactly matches a <see cref="WargearOption"/> — i.e. it is a piece of selectable wargear (e.g. a
    /// Lokhust Lord's "Nanoscarab amulet" or a Tomb Blade's "Nebuloscope") — is active only when that option is
    /// selected; an ability matched by no option is always active. Gates the ability's prose, its derived chips
    /// (Feel No Pain / Invulnerable), and its weapon self-effects on the chosen wargear.
    /// </summary>
    public static bool IsAbilityActive(Datasheet datasheet, RosterUnit unit, string abilityName)
    {
        ArgumentNullException.ThrowIfNull(datasheet);
        ArgumentNullException.ThrowIfNull(unit);
        if (string.IsNullOrWhiteSpace(abilityName))
            return true;

        var name = abilityName.Trim();
        var governed = false;
        foreach (var group in datasheet.WargearGroups)
        {
            foreach (var option in group.Options)
            {
                if (!string.Equals(name, option.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;
                governed = true;
                if (IsOptionSelected(unit, group.Id, option.Id))
                    return true;
            }
        }

        return !governed; // governed by wargear but nothing selected → inactive; ungoverned → always active
    }

    /// <summary>True when <paramref name="unit"/> has picked <paramref name="optionId"/> within <paramref name="groupId"/>.</summary>
    public static bool IsOptionSelected(RosterUnit unit, string groupId, string optionId)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var selection = unit.Wargear.FirstOrDefault(w => string.Equals(w.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
        return selection is not null && selection.OptionIds.Contains(optionId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The names of a datasheet's weapons governed by a <see cref="WargearGroup.PerModel"/> loadout group (e.g. a
    /// Lokhust Heavy Destroyers' Gauss Destructor / Enmitic Exterminator). Lets Play Mode track casualties per
    /// weapon when each model can carry a different one.
    /// </summary>
    public static IReadOnlySet<string> PerModelWeaponNames(Datasheet datasheet)
    {
        ArgumentNullException.ThrowIfNull(datasheet);
        return WeaponNamesForGroups(datasheet, static g => g.PerModel);
    }

    private static HashSet<string> WeaponNamesForGroups(Datasheet datasheet, Func<WargearGroup, bool> predicate)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in datasheet.WargearGroups.Where(predicate))
            foreach (var option in group.Options)
                foreach (var weapon in datasheet.Weapons)
                    if (NameMatches(weapon.Name, option.Name))
                        names.Add(weapon.Name);
        return names;
    }

    private static WargearSelection? FindSelection(RosterUnit unit, string groupId) =>
        unit.Wargear.FirstOrDefault(w => string.Equals(w.GroupId, groupId, StringComparison.OrdinalIgnoreCase));

    // Distributes the unit's models across a per-model group's options. Non-default options take their stored
    // count (clamped to what's left, in option order); the first option absorbs the remainder, so the totals
    // always sum to the model count and a unit with no stored counts resolves to all-of-the-first-option.
    private static void ApplyPerModelGroup(Datasheet datasheet, WargearGroup group, WargearSelection? selection, int models, Dictionary<string, int> counts)
    {
        if (group.Options.Count == 0 || models <= 0)
            return;

        var assigned = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var remaining = models;

        foreach (var option in group.Options.Skip(1))
        {
            var want = selection?.Counts.FirstOrDefault(c => string.Equals(c.OptionId, option.Id, StringComparison.OrdinalIgnoreCase))?.Models ?? 0;
            var take = Math.Clamp(want, 0, remaining);
            assigned[option.Id] = take;
            remaining -= take;
        }

        assigned[group.Options[0].Id] = remaining; // default option absorbs whatever is left

        foreach (var option in group.Options)
        {
            if (!assigned.TryGetValue(option.Id, out var n) || n <= 0)
                continue;
            foreach (var weapon in datasheet.Weapons)
                if (NameMatches(weapon.Name, option.Name))
                    counts[weapon.Name] = n;
        }
    }

    // Includes a toggle group's selected options' weapons at full model count. With no selections anywhere,
    // the whole toggle loadout is included (rosters predating weapon-pick are never shown empty).
    private static void ApplyToggleGroup(Datasheet datasheet, WargearGroup group, WargearSelection? selection, bool groupHasSelection, int models, Dictionary<string, int> counts)
    {
        foreach (var option in group.Options)
        {
            var selected = !groupHasSelection
                || (selection is not null && selection.OptionIds.Contains(option.Id, StringComparer.OrdinalIgnoreCase));
            if (!selected)
                continue;
            foreach (var weapon in datasheet.Weapons)
                if (NameMatches(weapon.Name, option.Name))
                    counts[weapon.Name] = models;
        }
    }

    // A weapon matches an option label when the label (or one of its "and"/comma-separated tokens) contains
    // the weapon name, case-insensitively. Substring (not equality) handles "Overlord's blade and tachyon
    // arrow" → "Overlord's blade", and "Claws and beamer" tokens → "Vicious claws"/"Transdimensional beamer".
    private static bool NameMatches(string weaponName, string optionLabel)
    {
        if (string.IsNullOrWhiteSpace(weaponName) || string.IsNullOrWhiteSpace(optionLabel))
            return false;

        if (optionLabel.Contains(weaponName, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var token in Tokenize(optionLabel))
        {
            if (token.Length == 0)
                continue;
            // Match in either direction so a short token ("beamer") hits a longer weapon name
            // ("Transdimensional beamer") and a short weapon name hits a longer token.
            if (weaponName.Contains(token, StringComparison.OrdinalIgnoreCase)
                || token.Contains(weaponName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static IEnumerable<string> Tokenize(string label)
    {
        var parts = label.Split(Connectors, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
            yield return part;
    }
}

/// <summary>A weapon in play together with how many models in the unit carry it.</summary>
public sealed record ResolvedWeapon(WeaponProfile Weapon, int Models);
