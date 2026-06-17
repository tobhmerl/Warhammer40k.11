using System.Net.Http.Json;
using System.Text.Json;
using Warhammer40k.Core;

namespace Warhammer40k._11;

/// <summary>
/// <see cref="IApiClient"/> implementation that calls the same-origin Static Web Apps
/// managed API (<c>/api</c>). Falls back to <see cref="UserInfo.Anonymous"/> when the API
/// is unreachable (e.g. running the UI on its own with <c>dotnet run</c>).
/// </summary>
internal sealed class ApiClient(HttpClient http) : IApiClient
{
    public async Task<UserInfo> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await http.GetFromJsonAsync<UserInfo>("api/whoami", cancellationToken);
            return user ?? UserInfo.Anonymous;
        }
        catch (HttpRequestException)
        {
            // API not deployed / not running (no /api route).
            return UserInfo.Anonymous;
        }
        catch (NotSupportedException)
        {
            // SPA fallback returned HTML instead of JSON.
            return UserInfo.Anonymous;
        }
        catch (JsonException)
        {
            return UserInfo.Anonymous;
        }
    }
}
