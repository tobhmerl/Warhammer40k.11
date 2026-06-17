using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Warhammer40k.Core;

namespace Warhammer40k.Api;

/// <summary>
/// Pure decoder for the base64 <c>x-ms-client-principal</c> value that Azure Static Web Apps injects
/// after login. Deliberately free of ASP.NET Core types so it can be unit tested without a web host.
/// </summary>
public static class ClientPrincipalParser
{
    private static readonly JsonSerializerOptions PrincipalJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Resolves the signed-in user from the raw base64 header value, or
    /// <see cref="UserInfo.Anonymous"/> when the value is missing or malformed.
    /// </summary>
    public static UserInfo ResolveUser(string? encodedPrincipal)
    {
        var principal = Parse(encodedPrincipal);
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

    private static ClientPrincipal? Parse(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            return null;

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            return JsonSerializer.Deserialize<ClientPrincipal>(decoded, PrincipalJson);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
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

/// <summary>
/// Reads the <c>x-ms-client-principal</c> header from a request and resolves the user via
/// <see cref="ClientPrincipalParser"/>. All server-side identity (including the per-user Table
/// Storage partition key) is derived here — never from client-supplied request bodies or routes.
/// </summary>
public static class ClientPrincipalReader
{
    /// <summary>Resolves the signed-in user from the request, or <see cref="UserInfo.Anonymous"/>.</summary>
    public static UserInfo ResolveUser(HttpRequest req)
    {
        req.Headers.TryGetValue("x-ms-client-principal", out var header);
        return ClientPrincipalParser.ResolveUser(header.ToString());
    }

    /// <summary>
    /// Returns the authenticated user's stable id (use as the Table Storage partition key),
    /// or <c>null</c> when no one is signed in.
    /// </summary>
    public static string? GetUserId(HttpRequest req)
    {
        var user = ResolveUser(req);
        return user.IsAuthenticated ? user.UserId : null;
    }
}
