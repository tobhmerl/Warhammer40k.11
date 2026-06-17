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
}
