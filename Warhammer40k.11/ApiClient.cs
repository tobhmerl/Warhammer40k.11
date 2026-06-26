using System.Net.Http.Json;
using System.Text.Json;
using Warhammer40k.Core;
using Warhammer40k.Core.Catalogue;
using Warhammer40k.Core.Rosters;
using Warhammer40k.Core.Rosters.Validation;
using Warhammer40k.Core.RulesAssistant;

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

    public async Task<CatalogueData> SaveCatalogueAsync(CatalogueData catalogue, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync("api/catalogue", catalogue, cancellationToken);
        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<CatalogueData>(cancellationToken);
        return saved ?? catalogue;
    }

    public async Task<CatalogueData> ResetCatalogueAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync("api/catalogue/reset", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var reset = await response.Content.ReadFromJsonAsync<CatalogueData>(cancellationToken);
        return reset ?? new CatalogueData();
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

    public async Task<IReadOnlyList<Roster>> GetRostersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var rosters = await http.GetFromJsonAsync<List<Roster>>("api/rosters", cancellationToken);
            return rosters ?? new List<Roster>();
        }
        catch (HttpRequestException)
        {
            // Not signed in (401) or API unreachable.
            return Array.Empty<Roster>();
        }
        catch (NotSupportedException)
        {
            return Array.Empty<Roster>();
        }
        catch (JsonException)
        {
            return Array.Empty<Roster>();
        }
    }

    public async Task<Roster?> GetRosterAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await http.GetFromJsonAsync<Roster>($"api/rosters/{id}", cancellationToken);
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

    public async Task<Roster> SaveRosterAsync(Roster roster, CancellationToken cancellationToken = default)
    {
        using var response = string.IsNullOrWhiteSpace(roster.Id)
            ? await http.PostAsJsonAsync("api/rosters", roster, cancellationToken)
            : await http.PutAsJsonAsync($"api/rosters/{roster.Id}", roster, cancellationToken);

        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<Roster>(cancellationToken);
        return saved ?? roster;
    }

    public async Task DeleteRosterAsync(string id, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/rosters/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ValidationResult?> ValidateRosterAsync(Roster roster, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("api/rosters/validate", roster, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ValidationResult>(cancellationToken);
        }
        catch (HttpRequestException)
        {
            // API unreachable — caller can fall back to local validation with the shared Core engine.
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

    public async Task<UserSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await http.GetFromJsonAsync<UserSettings>("api/settings", cancellationToken);
            return settings ?? UserSettings.Default;
        }
        catch (HttpRequestException)
        {
            return UserSettings.Default;
        }
        catch (NotSupportedException)
        {
            return UserSettings.Default;
        }
        catch (JsonException)
        {
            return UserSettings.Default;
        }
    }

    public async Task<UserSettings> SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync("api/settings", settings, cancellationToken);
        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<UserSettings>(cancellationToken);
        return saved ?? settings;
    }

    public async Task<BackupBundle?> GetBackupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await http.GetFromJsonAsync<BackupBundle>("api/backup", cancellationToken);
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

    public async Task RestoreBackupAsync(BackupBundle bundle, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/restore", bundle, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // ---- Rules Assistant (removable feature — see docs/rules-assistant-REMOVE.md) ----

    public async Task<RulesAnswer?> SearchRulesAsync(string query, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("api/rules/search", new { query }, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RulesAnswer>(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null; // API unreachable — the panel shows an offline note.
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
}