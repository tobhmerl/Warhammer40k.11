using System.ComponentModel.DataAnnotations;

namespace Warhammer40k.Core;

/// <summary>
/// A user's app preferences (AB8): the default points limit pre-filled in the New-Roster wizard and the
/// chosen UI theme. Persisted per user; falls back to <see cref="Default"/> when none is saved.
/// </summary>
public sealed class UserSettings
{
    /// <summary>Points limit pre-selected for a new roster (Strike-Force presets still apply, §1).</summary>
    [Range(0, 100_000, ErrorMessage = "Default points must be between 0 and 100,000.")]
    public int DefaultPointsLimit { get; set; } = 2000;

    /// <summary>The chosen UI theme id (one of <see cref="AppThemes.All"/>); defaults to phosphor green.</summary>
    public string Theme { get; set; } = AppThemes.Default;

    /// <summary>A fresh settings object with built-in defaults.</summary>
    public static UserSettings Default => new();
}

/// <summary>The selectable UI themes (accent palettes). The default matches the original Necron phosphor look.</summary>
public static class AppThemes
{
    public const string Default = "phosphor";

    /// <summary>Theme id + display name pairs offered in Settings.</summary>
    public static IReadOnlyList<AppTheme> All { get; } =
    [
        new("phosphor", "Phosphor Green"),
        new("arcane", "Arcane Blue"),
        new("ember", "Ember Amber"),
        new("blood", "Blood Crimson"),
    ];

    /// <summary>Returns the theme id if known, otherwise the default.</summary>
    public static string Normalize(string? id) =>
        id is not null && All.Any(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase))
            ? id
            : Default;
}

/// <summary>A selectable UI theme.</summary>
/// <param name="Id">Stable id used as the <c>data-theme</c> attribute value.</param>
/// <param name="Name">Display name shown in Settings.</param>
public sealed record AppTheme(string Id, string Name);
