using WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;

namespace WardenId.Protocol.Application.Tests;

public class ScopedUserClaimsTests
{
    private static readonly SignInUser Alice = new(Guid.NewGuid(), "alice@acme.test", "alice", UserStatus.Active, []);

    [Fact]
    public void OpenIdOnly_HasNoClaims()
    {
        var claims = ScopedUserClaims.From(Alice, ["openid"]);

        Assert.Null(claims.PreferredUsername);
        Assert.Null(claims.Email);
        Assert.Null(claims.EmailVerified);
    }

    [Fact]
    public void Profile_AddsPreferredUsername()
    {
        var claims = ScopedUserClaims.From(Alice, ["openid", "profile"]);

        Assert.Equal("alice", claims.PreferredUsername);
        Assert.Null(claims.Email);
    }

    [Fact]
    public void Email_AddsEmailAndVerified()
    {
        var claims = ScopedUserClaims.From(Alice, ["openid", "email"]);

        Assert.Equal("alice@acme.test", claims.Email);
        Assert.True(claims.EmailVerified);
        Assert.Null(claims.PreferredUsername);
    }
}
