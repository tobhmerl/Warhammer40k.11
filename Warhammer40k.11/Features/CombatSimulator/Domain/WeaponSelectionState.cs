namespace Warhammer40k._11.Features.CombatSimulator.Domain;

/// <summary>
/// UI state for selecting which of an attacker's weapons fire, how many models fire each, and which firing
/// mode is chosen for multi-mode weapons. Lives entirely in the feature. Part of the removable Combat
/// Simulator feature — see <c>Features/CombatSimulator/DELETE.md</c>.
/// </summary>
public sealed class WeaponSelectionState
{
    private sealed class Entry
    {
        public required CombatWeapon Weapon { get; init; }
        public bool Selected { get; set; }
        public int Models { get; set; }
        public int ModeIndex { get; set; }
    }

    private readonly List<Entry> _entries = [];

    /// <summary>When true the selector shows/uses melee weapons; when false, ranged. Defaults to ranged.</summary>
    public bool ShowMelee { get; private set; }

    /// <summary>Rebuilds the selectable list from a unit (weapons of the current class selected by default).</summary>
    public void Reset(CombatUnit? unit)
    {
        _entries.Clear();
        if (unit is null)
            return;
        foreach (var weapon in unit.AllWeapons)
            _entries.Add(new Entry
            {
                Weapon = weapon,
                Selected = weapon.IsMelee == ShowMelee,
                Models = Math.Max(1, weapon.CarriedByModels),
                ModeIndex = 0,
            });
    }

    /// <summary>Switches between ranged and melee: only weapons of the chosen class stay selected.</summary>
    public void SetClass(bool melee)
    {
        ShowMelee = melee;
        foreach (var e in _entries)
            e.Selected = e.Weapon.IsMelee == melee;
    }

    /// <summary>The weapons of the current class, with their original indices, for rendering.</summary>
    public IReadOnlyList<(int Index, CombatWeapon Weapon)> VisibleWeapons =>
        _entries.Select((e, i) => (i, e.Weapon)).Where(t => t.Weapon.IsMelee == ShowMelee).ToList();

    /// <summary>The weapons, in order, for rendering the selector rows.</summary>
    public IReadOnlyList<CombatWeapon> Weapons => _entries.Select(e => e.Weapon).ToList();

    public bool IsSelected(int index) => Valid(index) && _entries[index].Selected;
    public void SetSelected(int index, bool on) { if (Valid(index)) _entries[index].Selected = on; }

    public int Models(int index) => Valid(index) ? _entries[index].Models : 0;
    public void SetModels(int index, int models) { if (Valid(index)) _entries[index].Models = Math.Max(0, models); }

    public int ModeIndex(int index) => Valid(index) ? _entries[index].ModeIndex : 0;
    public void SetMode(int index, int mode) { if (Valid(index)) _entries[index].ModeIndex = Math.Max(0, mode); }

    /// <summary>True when at least one weapon is selected with a positive firing-model count.</summary>
    public bool Any => _entries.Any(e => e.Selected && e.Models > 0);

    /// <summary>
    /// The selected weapons, each resolved to its chosen firing mode and with the user's firing-model count.
    /// </summary>
    public List<CombatWeapon> ResolvedWeapons()
    {
        var result = new List<CombatWeapon>();
        foreach (var e in _entries)
        {
            if (!e.Selected || e.Models <= 0)
                continue;
            var chosen = e.Weapon;
            if (chosen.HasFiringModes && e.ModeIndex >= 0 && e.ModeIndex < chosen.FiringModes.Count)
                chosen = chosen.FiringModes[e.ModeIndex];
            result.Add(chosen with { CarriedByModels = e.Models });
        }
        return result;
    }

    private bool Valid(int index) => index >= 0 && index < _entries.Count;
}
