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

    [Fact]
    public void Profile_AddsTheNames_AndTheFullName()
    {
        var ada = Alice with { FirstName = "Ada", LastName = "Lovelace" };

        var claims = ScopedUserClaims.From(ada, ["openid", "profile"]);

        Assert.Equal("Ada", claims.GivenName);
        Assert.Equal("Lovelace", claims.FamilyName);
        Assert.Equal("Ada Lovelace", claims.Name);
    }

    [Fact]
    public void Profile_WithOneName_UsesItAsTheFullName()
    {
        var claims = ScopedUserClaims.From(Alice with { LastName = "Lovelace" }, ["openid", "profile"]);

        Assert.Null(claims.GivenName);
        Assert.Equal("Lovelace", claims.FamilyName);
        Assert.Equal("Lovelace", claims.Name);
    }

    [Fact]
    public void Profile_WithoutNames_HasNoNameClaims()
    {
        var claims = ScopedUserClaims.From(Alice, ["openid", "profile"]);

        Assert.Null(claims.GivenName);
        Assert.Null(claims.FamilyName);
        Assert.Null(claims.Name);
    }

    [Fact]
    public void WithoutProfile_TheNamesAreLeftOut()
    {
        var claims = ScopedUserClaims.From(Alice with { FirstName = "Ada", LastName = "Lovelace" }, ["openid", "email"]);

        Assert.Null(claims.GivenName);
        Assert.Null(claims.FamilyName);
        Assert.Null(claims.Name);
    }
}
