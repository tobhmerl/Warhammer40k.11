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
    /// The weapons in play for <paramref name="unit"/>, in datasheet order: always-on weapons plus the
    /// weapons named by the unit's selected wargear options.
    /// </summary>
    public static IReadOnlyList<WeaponProfile> SelectedWeapons(Datasheet datasheet, RosterUnit unit)
    {
        ArgumentNullException.ThrowIfNull(datasheet);
        ArgumentNullException.ThrowIfNull(unit);

        if (datasheet.Weapons.Count == 0)
            return datasheet.Weapons;

        // Names referenced by ANY option across all groups → these weapons are "optional" (gated by choice).
        var optionalWeaponNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in datasheet.WargearGroups)
            foreach (var option in group.Options)
                foreach (var weapon in datasheet.Weapons)
                    if (NameMatches(weapon.Name, option.Name))
                        optionalWeaponNames.Add(weapon.Name);

        // If nothing was selected, fall back to the whole loadout (back-compat / never-empty).
        var hasSelections = unit.Wargear.Any(w => w.OptionIds.Count > 0);
        if (!hasSelections)
            return datasheet.Weapons;

        // Names referenced by the SELECTED options → the optional weapons actually taken.
        var selectedWeaponNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var selection in unit.Wargear)
        {
            var group = datasheet.WargearGroups.FirstOrDefault(g => string.Equals(g.Id, selection.GroupId, StringComparison.OrdinalIgnoreCase));
            if (group is null)
                continue;
            foreach (var optionId in selection.OptionIds)
            {
                var option = group.Options.FirstOrDefault(o => string.Equals(o.Id, optionId, StringComparison.OrdinalIgnoreCase));
                if (option is null)
                    continue;
                foreach (var weapon in datasheet.Weapons)
                    if (NameMatches(weapon.Name, option.Name))
                        selectedWeaponNames.Add(weapon.Name);
            }
        }

        return datasheet.Weapons
            .Where(w => !optionalWeaponNames.Contains(w.Name) || selectedWeaponNames.Contains(w.Name))
            .ToList();
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
