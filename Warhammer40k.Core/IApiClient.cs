using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Rosters;
using Warhammer40k.Core.Rosters.Validation;

namespace Warhammer40k.Core;

/// <summary>
/// Client-side gateway to the server <c>/api</c> (Azure Static Web Apps managed Functions).
/// The frontend depends only on this abstraction; the concrete implementation wraps
/// <see cref="System.Net.Http.HttpClient"/>.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Returns the currently signed-in user, or <see cref="UserInfo.Anonymous"/> when
    /// no one is authenticated or the API is unreachable.
    /// </summary>
    Task<UserInfo> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the enriched Necron catalogue (datasheets + Pantheon bindings) from <c>/api/catalogue</c>,
    /// or an empty <see cref="CatalogueData"/> when the API is unreachable.
    /// </summary>
    Task<CatalogueData> GetCatalogueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the signed-in user's edited catalogue (<c>PUT /api/catalogue</c>) and returns the persisted,
    /// re-enriched copy. Throws when the save fails so the editor can surface the error.
    /// </summary>
    Task<CatalogueData> SaveCatalogueAsync(CatalogueData catalogue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards the user's catalogue edits (<c>POST /api/catalogue/reset</c>) and returns the embedded default.
    /// </summary>
    Task<CatalogueData> ResetCatalogueAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists the signed-in user's armies (newest first). Empty when not signed in.</summary>
    Task<IReadOnlyList<Army>> GetArmiesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a single army owned by the signed-in user, or <c>null</c> if it does not exist.</summary>
    Task<Army?> GetArmyAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates (when <see cref="Army.Id"/> is empty) or updates an army for the signed-in user
    /// and returns the persisted copy (with server-assigned <see cref="Army.Id"/>/timestamp).
    /// </summary>
    Task<Army> SaveArmyAsync(Army army, CancellationToken cancellationToken = default);

    /// <summary>Deletes an army owned by the signed-in user. No-op if it does not exist.</summary>
    Task DeleteArmyAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Lists the signed-in user's rosters (newest first). Empty when not signed in or the API is unreachable.</summary>
    Task<IReadOnlyList<Roster>> GetRostersAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a single roster owned by the signed-in user, or <c>null</c> if it does not exist.</summary>
    Task<Roster?> GetRosterAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates (when <see cref="Roster.Id"/> is empty) or updates a roster for the signed-in user
    /// and returns the persisted copy (with server-assigned id and timestamps).
    /// </summary>
    Task<Roster> SaveRosterAsync(Roster roster, CancellationToken cancellationToken = default);

    /// <summary>Deletes a roster owned by the signed-in user. No-op if it does not exist.</summary>
    Task DeleteRosterAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a roster server-side (rules R1–R11) and returns the result, or <c>null</c> when the API is
    /// unreachable so the caller can fall back to local validation with the shared Core engine.
    /// </summary>
    Task<ValidationResult?> ValidateRosterAsync(Roster roster, CancellationToken cancellationToken = default);

    /// <summary>Returns the signed-in user's settings, or <see cref="UserSettings.Default"/> when unsaved/unreachable.</summary>
    Task<UserSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves the signed-in user's settings and returns the persisted copy.</summary>
    Task<UserSettings> SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default);

    /// <summary>Exports the signed-in user's data (settings + catalogue + rosters) as a bundle, or <c>null</c> when unreachable.</summary>
    Task<BackupBundle?> GetBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>Restores the signed-in user's data from a bundle, replacing existing rosters/catalogue/settings.</summary>
    Task RestoreBackupAsync(BackupBundle bundle, CancellationToken cancellationToken = default);

    // ---- Rules Assistant (removable feature — see docs/rules-assistant-REMOVE.md) ----

    /// <summary>
    /// Searches the Core-Rules corpus (<c>POST /api/rules/search</c>) and returns the best-matching rules
    /// verbatim with citations, or <c>null</c> when the API is unreachable.
    /// </summary>
    Task<RulesAssistant.RulesAnswer?> SearchRulesAsync(string query, CancellationToken cancellationToken = default);
}