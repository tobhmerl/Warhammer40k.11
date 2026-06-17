using System.Text;
using System.Text.Json;
using Warhammer40k.Api;
using Warhammer40k.Core;

namespace Warhammer40k.Tests;

public class ClientPrincipalParserTests
{
    private static string Encode(object principal) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(principal)));

    [Fact]
    public void Missing_header_resolves_to_anonymous()
    {
        Assert.Same(UserInfo.Anonymous, ClientPrincipalParser.ResolveUser(null));
        Assert.Same(UserInfo.Anonymous, ClientPrincipalParser.ResolveUser(""));
        Assert.Same(UserInfo.Anonymous, ClientPrincipalParser.ResolveUser("   "));
    }

    [Fact]
    public void Malformed_base64_resolves_to_anonymous()
    {
        var user = ClientPrincipalParser.ResolveUser("!!!not-base64!!!");

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.UserId);
    }

    [Fact]
    public void Valid_principal_is_decoded_and_authenticated()
    {
        var encoded = Encode(new
        {
            identityProvider = "github",
            userId = "user-123",
            userDetails = "tobias",
            userRoles = new[] { "anonymous", "authenticated" },
        });

        var user = ClientPrincipalParser.ResolveUser(encoded);

        Assert.True(user.IsAuthenticated);
        Assert.Equal("user-123", user.UserId);
        Assert.Equal("tobias", user.UserName);
        Assert.Equal("github", user.IdentityProvider);
        Assert.Contains("authenticated", user.Roles);
    }

    [Fact]
    public void Principal_without_authenticated_role_is_not_authenticated()
    {
        var encoded = Encode(new
        {
            identityProvider = "github",
            userId = "user-123",
            userDetails = "tobias",
            userRoles = new[] { "anonymous" },
        });

        var user = ClientPrincipalParser.ResolveUser(encoded);

        Assert.False(user.IsAuthenticated);
        Assert.Equal("user-123", user.UserId);
    }
}
