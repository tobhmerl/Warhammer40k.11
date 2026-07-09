using System.Text.Json;
using Microsoft.JSInterop;
using Warhammer40k._11.Features.CombatSimulator.Domain;

namespace Warhammer40k._11.Features.CombatSimulator;

/// <summary>
/// A serializable snapshot of everything the user has set up in the simulator, persisted to localStorage so a
/// trip to Play Mode (or a reload) doesn't lose it. Part of the removable Combat Simulator feature — see
/// <c>Features/CombatSimulator/DELETE.md</c>.
/// </summary>
public sealed class CombatSimSnapshot
{
    public bool MyArmyAttacks { get; set; } = true;
    public bool ShowMelee { get; set; }

    /// <summary>The roster whose units populate the "my army" pool (only one army's units are shown at a time).</summary>
    public string? RosterId { get; set; }

    /// <summary>The raw imported army JSON exports (re-parsed on load, avoiding polymorphic (de)serialization).</summary>
    public List<string> ImportedJson { get; set; } = [];

    /// <summary>Selected attacker/target by display name (ids aren't stable across rebuilds).</summary>
    public string? AttackerName { get; set; }
    public string? TargetName { get; set; }

    /// <summary>Per-weapon selection: keyed by weapon name → (selected, models, mode).</summary>
    public List<WeaponPick> WeaponPicks { get; set; } = [];

    public AttackerModifiers Attacker { get; set; } = new();
    public DefenderModifiers Defender { get; set; } = new();

    public int Iterations { get; set; } = 10_000;
    public int? Seed { get; set; }
}

/// <summary>One weapon's saved selection state.</summary>
public sealed class WeaponPick
{
    public string Name { get; set; } = "";
    public bool Selected { get; set; }
    public int Models { get; set; }
    public int ModeIndex { get; set; }
}

/// <summary>
/// Reads/writes the <see cref="CombatSimSnapshot"/> to browser localStorage via JS interop. Lives in the
/// feature; touches no app/global state. Part of the removable Combat Simulator feature.
/// </summary>
public sealed class CombatSimStateStore(IJSRuntime js)
{
    private const string Key = "combat-sim-state";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    /// <summary>Saves the snapshot. Failures (e.g. storage disabled) are swallowed — persistence is best-effort.</summary>
    public async Task SaveAsync(CombatSimSnapshot snapshot)
    {
        try
        {
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            await js.InvokeVoidAsync("localStorage.setItem", Key, json);
        }
        catch
        {
            // best-effort
        }
    }

    /// <summary>Loads the snapshot, or null when none is stored / it can't be read.</summary>
    public async Task<CombatSimSnapshot?> LoadAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<CombatSimSnapshot>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Clears the stored snapshot.</summary>
    public async Task ClearAsync()
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", Key);
        }
        catch
        {
            // best-effort
        }
    }
}
