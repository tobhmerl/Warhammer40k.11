namespace Warhammer40k.Core;

/// <summary>
/// Client-side gateway to the server <c>/api</c> (Azure Static Web Apps managed Functions).
/// The frontend depends only on this abstraction; the concrete implementation wraps
/// <see cref="System.Net.Http.HttpClient"/>. Repository operations are added in M1.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Returns the currently signed-in user, or <see cref="UserInfo.Anonymous"/> when
    /// no one is authenticated or the API is unreachable.
    /// </summary>
    Task<UserInfo> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
