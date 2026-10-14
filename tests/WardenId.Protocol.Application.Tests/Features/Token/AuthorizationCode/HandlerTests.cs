using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;
using WardenId.Protocol.Application.Features.Token;
using WardenId.Protocol.Application.Features.Token.AuthorizationCode;
using RequiredAction = WardenId.Identity.Integration.Command.Features.Users.Authenticate.RequiredAction;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;

namespace WardenId.Protocol.Application.Tests.Features.Token.AuthorizationCode;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private static Result<SignInUser> User(UserStatus status = UserStatus.Active, params RequiredAction[] actions) =>
        Result.Ok(new SignInUser(UserId, "alice@acme.test", "alice", status, actions));

    private static Result<ClientForAuthentication> Client(
        int? lifetimeSeconds = null, int? refreshSeconds = null, bool enabled = true, AllowedGrant[]? grants = null) =>
        Result.Ok(new ClientForAuthentication(
            Guid.NewGuid(), "web", ClientKind.Confidential, enabled,
            grants ?? [AllowedGrant.AuthorizationCode, AllowedGrant.RefreshToken],
            ClientAuthMethod.SharedSecret, lifetimeSeconds, refreshSeconds, ["https://app.example/cb"], false, [], null));

    private static Task<Result<UserTokenGrant>> HandleAsync(
        FakeSender sender, DateTimeOffset? authTime = null, params string[] scopes)
    {
        var factory = new UserTokenGrantFactory(
            sender,
            Options.Create(new ProtocolTokenOptions { IssuerBaseUri = "https://wardenid.test" }),
            new FakeTimeProvider(Now));
        return new Handler(factory).Handle(
            new Command(TenantId, "acme", "web", UserId, scopes, authTime ?? Now.AddMinutes(-1)),
            CancellationToken.None);
    }

    [Fact]
    public async Task Handle_OpenIdOnly_HasNoUserClaims_AndNoRefreshToken()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client() };

        var result = await HandleAsync(sender, scopes: ["openid"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserId.ToString(), result.Value.Subject);
        Assert.Equal(TenantId, result.Value.TenantId);
        Assert.Equal("https://wardenid.test/tenants/acme/api", result.Value.Audience);
        Assert.Equal(TimeSpan.FromMinutes(15), result.Value.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromMinutes(15), result.Value.IdTokenLifetime);
        Assert.Null(result.Value.RefreshTokenLifetime);
        Assert.Null(result.Value.PreferredUsername);
        Assert.Null(result.Value.Email);
        Assert.Null(result.Value.EmailVerified);
    }

    [Fact]
    public async Task Handle_EmailScope_AddsEmailAndVerified()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client() };

        var result = await HandleAsync(sender, scopes: ["openid", "email"]);

        Assert.Equal("alice@acme.test", result.Value.Email);
        Assert.True(result.Value.EmailVerified);
        Assert.Null(result.Value.PreferredUsername);
    }

    [Fact]
    public async Task Handle_ProfileScope_AddsPreferredUsername()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client() };

        var result = await HandleAsync(sender, scopes: ["openid", "profile"]);

        Assert.Equal("alice", result.Value.PreferredUsername);
        Assert.Null(result.Value.Email);
    }

    [Fact]
    public async Task Handle_UsesClientLifetimeOverride()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client(lifetimeSeconds: 60) };

        var result = await HandleAsync(sender, scopes: ["openid"]);

        Assert.Equal(TimeSpan.FromSeconds(60), result.Value.AccessTokenLifetime);
    }

    [Fact]
    public async Task Handle_OfflineAccess_FreshSession_GetsSlidingLifetime()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client() };

        var result = await HandleAsync(sender, Now.AddMinutes(-1), "openid", "offline_access");

        Assert.Equal(TimeSpan.FromDays(30), result.Value.RefreshTokenLifetime);
    }

    [Fact]
    public async Task Handle_OfflineAccess_UsesClientRefreshOverride()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client(refreshSeconds: 3600) };

        var result = await HandleAsync(sender, Now.AddMinutes(-1), "openid", "offline_access");

        Assert.Equal(TimeSpan.FromHours(1), result.Value.RefreshTokenLifetime);
    }

    // The refresh grant can be removed between /authorize and redemption: no refresh token then.
    [Fact]
    public async Task Handle_OfflineAccess_ClientWithoutRefreshGrant_GetsNoRefreshLifetime()
    {
        var sender = new FakeSender
        {
            SignInUserResponse = User(),
            ClientResponse = Client(grants: [AllowedGrant.AuthorizationCode])
        };

        var result = await HandleAsync(sender, Now.AddMinutes(-1), "openid", "offline_access");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.RefreshTokenLifetime);
    }

    [Fact]
    public async Task Handle_OfflineAccess_NearAbsoluteCap_IsClampedToRemaining()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client() };

        // 90-day cap, session started 85 days ago -> 5 days left (< 30-day sliding).
        var result = await HandleAsync(sender, Now.AddDays(-85), "openid", "offline_access");

        Assert.Equal(TimeSpan.FromDays(5), result.Value.RefreshTokenLifetime);
    }

    [Fact]
    public async Task Handle_OfflineAccess_PastAbsoluteCap_IsSessionExpired()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client() };

        var result = await HandleAsync(sender, Now.AddDays(-90), "openid", "offline_access");

        Assert.Equal(ProtocolErrorCodes.SessionExpired, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_FailsInvalidGrant_WhenUserNotFound()
    {
        var sender = new FakeSender { SignInUserResponse = Result.Fail<SignInUser>("Not found."), ClientResponse = Client() };

        var result = await HandleAsync(sender, scopes: ["openid"]);

        Assert.Equal(ProtocolErrorCodes.InvalidGrant, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Theory]
    [InlineData(UserStatus.Disabled)]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.PendingVerification)]
    public async Task Handle_FailsInvalidGrant_WhenUserNotActive(UserStatus status)
    {
        var sender = new FakeSender { SignInUserResponse = User(status), ClientResponse = Client() };

        var result = await HandleAsync(sender, scopes: ["openid"]);

        Assert.Equal(ProtocolErrorCodes.InvalidGrant, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_FailsInvalidGrant_WhenUserHasPendingActions()
    {
        var sender = new FakeSender { SignInUserResponse = User(UserStatus.Active, RequiredAction.AcceptTos), ClientResponse = Client() };

        var result = await HandleAsync(sender, scopes: ["openid"]);

        Assert.Equal(ProtocolErrorCodes.InvalidGrant, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_FailsInvalidGrant_WhenClientDisabled()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client(enabled: false) };

        var result = await HandleAsync(sender, scopes: ["openid"]);

        Assert.Equal(ProtocolErrorCodes.InvalidGrant, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }
}
