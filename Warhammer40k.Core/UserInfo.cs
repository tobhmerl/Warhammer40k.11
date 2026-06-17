namespace Warhammer40k.Core;

/// <summary>
/// Represents the currently authenticated user as resolved by Azure Static Web Apps
/// built-in authentication (the decoded <c>x-ms-client-principal</c>).
/// </summary>
/// <param name="IsAuthenticated">True when a user is signed in.</param>
/// <param name="UserId">Stable, provider-issued user id used to partition this user's data.</param>
/// <param name="UserName">Human-friendly display name (SWA <c>userDetails</c>).</param>
/// <param name="IdentityProvider">The login provider, e.g. "github" or "aad".</param>
/// <param name="Roles">Roles assigned to the user (always includes "anonymous"; "authenticated" when signed in).</param>
public sealed record UserInfo(
    bool IsAuthenticated,
    string? UserId,
    string? UserName,
    string? IdentityProvider,
    IReadOnlyList<string> Roles)
{
    /// <summary>A shared instance representing a signed-out visitor.</summary>
    public static UserInfo Anonymous { get; } =
        new(false, null, null, null, new[] { "anonymous" });
}
