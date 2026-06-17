using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Warhammer40k.Core;

namespace Warhammer40k.Api;

/// <summary>
/// <c>GET /api/whoami</c> - returns the signed-in user by decoding the
/// <c>x-ms-client-principal</c> header that Azure Static Web Apps injects after login.
/// Returns <see cref="UserInfo.Anonymous"/> when no one is authenticated.
/// </summary>
public class WhoAmI
{
    private static readonly JsonSerializerOptions PrincipalJson = new(JsonSerializerDefaults.Web);

    [Function("WhoAmI")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "whoami")] HttpRequest req)
        => new OkObjectResult(ResolveUser(req));

    private static UserInfo ResolveUser(HttpRequest req)
    {
        if (!req.Headers.TryGetValue("x-ms-client-principal", out var header))
            return UserInfo.Anonymous;

        var encoded = header.ToString();
        if (string.IsNullOrWhiteSpace(encoded))
            return UserInfo.Anonymous;

        ClientPrincipal? principal;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            principal = JsonSerializer.Deserialize<ClientPrincipal>(decoded, PrincipalJson);
        }
        catch (FormatException)
        {
            return UserInfo.Anonymous;
        }

        if (principal?.UserId is null)
            return UserInfo.Anonymous;

        var roles = principal.UserRoles ?? new List<string> { "anonymous" };

        return new UserInfo(
            IsAuthenticated: roles.Contains("authenticated"),
            UserId: principal.UserId,
            UserName: principal.UserDetails,
            IdentityProvider: principal.IdentityProvider,
            Roles: roles);
    }

    /// <summary>Shape of the base64-encoded JSON Static Web Apps puts in <c>x-ms-client-principal</c>.</summary>
    private sealed class ClientPrincipal
    {
        [JsonPropertyName("identityProvider")] public string? IdentityProvider { get; set; }
        [JsonPropertyName("userId")] public string? UserId { get; set; }
        [JsonPropertyName("userDetails")] public string? UserDetails { get; set; }
        [JsonPropertyName("userRoles")] public List<string>? UserRoles { get; set; }
    }
}
