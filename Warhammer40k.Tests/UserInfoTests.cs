using Warhammer40k.Core;

namespace Warhammer40k.Tests;

public class UserInfoTests
{
    [Fact]
    public void Anonymous_is_not_authenticated()
    {
        Assert.False(UserInfo.Anonymous.IsAuthenticated);
        Assert.Null(UserInfo.Anonymous.UserId);
        Assert.Contains("anonymous", UserInfo.Anonymous.Roles);
    }

    [Fact]
    public void Authenticated_user_carries_identity()
    {
        var user = new UserInfo(
            IsAuthenticated: true,
            UserId: "abc123",
            UserName: "tobias",
            IdentityProvider: "github",
            Roles: new[] { "anonymous", "authenticated" });

        Assert.True(user.IsAuthenticated);
        Assert.Equal("abc123", user.UserId);
        Assert.Equal("github", user.IdentityProvider);
        Assert.Contains("authenticated", user.Roles);
    }
}
