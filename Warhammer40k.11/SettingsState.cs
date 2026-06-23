using Microsoft.JSInterop;
using Warhammer40k.Core;

namespace Warhammer40k._11;

/// <summary>
/// Scoped app state for user settings (AB8): loads the user's settings once, applies the chosen theme via the
/// <c>tombworld</c> JS helper, and exposes the default points limit to the New-Roster wizard. Components can
/// subscribe to <see cref="Changed"/> to refresh when settings change.
/// </summary>
public sealed class SettingsState(IApiClient api, IJSRuntime js)
{
    private bool _initialized;

    /// <summary>The current settings (defaults until <see cref="InitializeAsync"/> completes).</summary>
    public UserSettings Current { get; private set; } = UserSettings.Default;

    /// <summary>Default points limit pre-filled in the New-Roster wizard.</summary>
    public int DefaultPointsLimit => Current.DefaultPointsLimit;

    /// <summary>Whether the Play Mode control HUD is pinned (sticky) rather than floating/auto-hiding.</summary>
    public bool PlayHudSticky => Current.PlayHudSticky;

    /// <summary>Raised after settings load or change.</summary>
    public event Action? Changed;

    /// <summary>Loads settings and applies the theme. Safe to call repeatedly; only the first call does work.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;

        Current = await api.GetSettingsAsync();
        await ApplyThemeAsync(Current.Theme);
        Changed?.Invoke();
    }

    /// <summary>Persists new settings, re-applies the theme, and notifies subscribers. Returns false on failure.</summary>
    public async Task<bool> SaveAsync(UserSettings settings)
    {
        try
        {
            Current = await api.SaveSettingsAsync(settings);
            await ApplyThemeAsync(Current.Theme);
            Changed?.Invoke();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Applies a theme immediately without persisting (live preview while choosing in Settings).</summary>
    public Task PreviewThemeAsync(string theme) => ApplyThemeAsync(theme);

    /// <summary>Re-applies the currently saved theme (e.g. after a cancelled preview).</summary>
    public Task RevertThemeAsync() => ApplyThemeAsync(Current.Theme);

    /// <summary>Adopts settings that were already persisted elsewhere (e.g. after a backup restore) and applies the theme.</summary>
    public async Task ApplyAsync(UserSettings settings)
    {
        Current = settings;
        await ApplyThemeAsync(Current.Theme);
        Changed?.Invoke();
    }

    private async Task ApplyThemeAsync(string theme)
    {
        try
        {
            await js.InvokeVoidAsync("tombworld.setTheme", AppThemes.Normalize(theme));
        }
        catch (JSException)
        {
            // Ignore — styling falls back to the default theme.
        }
        catch (InvalidOperationException)
        {
            // JS runtime not available yet (e.g. before first render).
        }
    }
}
