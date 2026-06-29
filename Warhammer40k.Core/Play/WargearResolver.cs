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

        // 3) Toggle groups: selected options' weapons are carried by every model. A unit with no selections of
        //    any kind keeps its full toggle loadout (never-empty back-compat).
        var hasAnySelection = unit.Wargear.Any(w => w.OptionIds.Count > 0 || w.Counts.Count > 0);
        foreach (var group in datasheet.WargearGroups.Where(g => !g.PerModel))
            ApplyToggleGroup(datasheet, group, FindSelection(unit, group.Id), hasAnySelection, models, counts);

        return datasheet.Weapons
            .Where(w => counts.TryGetValue(w.Name, out var c) && c > 0)
            .Select(w => new ResolvedWeapon(w, counts[w.Name]))
            .ToList();
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
    private static void ApplyToggleGroup(Datasheet datasheet, WargearGroup group, WargearSelection? selection, bool hasAnySelection, int models, Dictionary<string, int> counts)
    {
        foreach (var option in group.Options)
        {
            var selected = !hasAnySelection
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
