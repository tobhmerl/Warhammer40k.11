using System.Net.Http.Json;
using System.Text.Json;
using Warhammer40k.Core;
using Warhammer40k.Core.Catalogue;

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

    public async Task<CatalogueData> GetCatalogueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var catalogue = await http.GetFromJsonAsync<CatalogueData>("api/catalogue", cancellationToken);
            return catalogue ?? new CatalogueData();
        }
        catch (HttpRequestException)
        {
            // API not deployed / not running (no /api route).
            return new CatalogueData();
        }
        catch (NotSupportedException)
        {
            // SPA fallback returned HTML instead of JSON.
            return new CatalogueData();
        }
        catch (JsonException)
        {
            return new CatalogueData();
        }
    }

    public async Task<IReadOnlyList<Army>> GetArmiesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var armies = await http.GetFromJsonAsync<List<Army>>("api/armies", cancellationToken);
            return armies ?? new List<Army>();
        }
        catch (HttpRequestException)
        {
            // Not signed in (401) or API unreachable.
            return Array.Empty<Army>();
        }
        catch (NotSupportedException)
        {
            return Array.Empty<Army>();
        }
        catch (JsonException)
        {
            return Array.Empty<Army>();
        }
    }

    public async Task<Army?> GetArmyAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await http.GetFromJsonAsync<Army>($"api/armies/{id}", cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<Army> SaveArmyAsync(Army army, CancellationToken cancellationToken = default)
    {
        using var response = string.IsNullOrWhiteSpace(army.Id)
            ? await http.PostAsJsonAsync("api/armies", army, cancellationToken)
            : await http.PutAsJsonAsync($"api/armies/{army.Id}", army, cancellationToken);

        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<Army>(cancellationToken);
        return saved ?? army;
    }

    public async Task DeleteArmyAsync(string id, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/armies/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
