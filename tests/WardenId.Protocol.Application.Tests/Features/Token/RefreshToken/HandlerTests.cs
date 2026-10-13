using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;
using WardenId.Protocol.Application.Features.Token;
using WardenId.Protocol.Application.Features.Token.RefreshToken;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;
using GetForAuthenticationQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication.Query;

namespace WardenId.Protocol.Application.Tests.Features.Token.RefreshToken;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private static Result<SignInUser> User(UserStatus status = UserStatus.Active) =>
        Result.Ok(new SignInUser(UserId, "alice@acme.test", "alice", status, []));

    private static Result<ClientForAuthentication> Client(int? refreshSeconds = null) =>
        Result.Ok(new ClientForAuthentication(
            Guid.NewGuid(), "web", ClientKind.Confidential, true,
            [AllowedGrant.AuthorizationCode, AllowedGrant.RefreshToken],
            ClientAuthMethod.SharedSecret, null, refreshSeconds, ["https://app.example/cb"], false));

    private static Task<Result<UserTokenGrant>> HandleAsync(FakeSender sender, DateTimeOffset authTime, params string[] scopes) =>
        HandleAsync(sender, authTime, scopes, requestedScopes: []);

    private static Task<Result<UserTokenGrant>> HandleAsync(
        FakeSender sender, DateTimeOffset authTime, string[] grantedScopes, string[] requestedScopes) =>
        new Handler(new UserTokenGrantFactory(
                sender,
                Options.Create(new ProtocolTokenOptions { IssuerBaseUri = "https://wardenid.test" }),
                new FakeTimeProvider(Now)))
            .Handle(
                new Command(TenantId, "acme", "web", UserId, grantedScopes, requestedScopes, authTime),
                CancellationToken.None);

    [Fact]
    public async Task Handle_ActiveUser_ReturnsFreshScopedClaims_AndSlidingLifetime()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client() };

        var result = await HandleAsync(sender, Now.AddDays(-1), "openid", "email", "offline_access");

        Assert.True(result.IsSuccess);
        Assert.Equal(UserId.ToString(), result.Value.Subject);
        Assert.Equal("alice@acme.test", result.Value.Email);
        Assert.Null(result.Value.PreferredUsername);
        Assert.Equal(TimeSpan.FromDays(30), result.Value.RefreshTokenLifetime);
        Assert.Equal(TenantId, Assert.IsType<GetForSignInQuery>(sender.Sent[0]).TenantId);
        Assert.Equal("web", Assert.IsType<GetForAuthenticationQuery>(sender.Sent[1]).ClientId);
    }

    // RFC 6749 §6: a narrowed `scope` filters claims, but the refresh token keeps the original grant.
    [Fact]
    public async Task Handle_NarrowedScope_FiltersClaims_ButKeepsRefreshLifetime()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client() };

        var result = await HandleAsync(
            sender, Now.AddDays(-1), grantedScopes: ["openid", "email", "offline_access"], requestedScopes: ["openid"]);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Email);
        Assert.Null(result.Value.EmailVerified);
        Assert.Equal(TimeSpan.FromDays(30), result.Value.RefreshTokenLifetime);
    }

    [Fact]
    public async Task Handle_ClientWithoutRefreshGrant_GetsNoRefreshLifetime()
    {
        var sender = new FakeSender
        {
            SignInUserResponse = User(),
            ClientResponse = Result.Ok(new ClientForAuthentication(
                Guid.NewGuid(), "web", ClientKind.Confidential, true, [AllowedGrant.AuthorizationCode],
                ClientAuthMethod.SharedSecret, null, null, ["https://app.example/cb"], false))
        };

        var result = await HandleAsync(sender, Now.AddDays(-1), "openid", "offline_access");

        Assert.Null(result.Value.RefreshTokenLifetime);
    }

    [Fact]
    public async Task Handle_ClampsToAbsoluteCap()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client() };

        var result = await HandleAsync(sender, Now.AddDays(-89).AddHours(-12), "openid", "offline_access");

        Assert.Equal(TimeSpan.FromHours(12), result.Value.RefreshTokenLifetime);
    }

    [Fact]
    public async Task Handle_ClientRefreshOverride_Wins_WhenShorterThanCap()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client(refreshSeconds: 600) };

        var result = await HandleAsync(sender, Now.AddDays(-1), "openid", "offline_access");

        Assert.Equal(TimeSpan.FromMinutes(10), result.Value.RefreshTokenLifetime);
    }

    [Fact]
    public async Task Handle_PastCap_IsSessionExpired()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client() };

        var result = await HandleAsync(sender, Now.AddDays(-91), "openid", "offline_access");

        Assert.Equal(ProtocolErrorCodes.SessionExpired, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Theory]
    [InlineData(UserStatus.Disabled)]
    [InlineData(UserStatus.Locked)]
    public async Task Handle_UnusableUser_IsInvalidGrant(UserStatus status)
    {
        var sender = new FakeSender { SignInUserResponse = User(status), ClientResponse = Client() };

        var result = await HandleAsync(sender, Now.AddDays(-1), "openid", "offline_access");

        Assert.Equal(ProtocolErrorCodes.InvalidGrant, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_UnknownUser_IsInvalidGrant()
    {
        var sender = new FakeSender { SignInUserResponse = Result.Fail<SignInUser>("Not found."), ClientResponse = Client() };

        var result = await HandleAsync(sender, Now.AddDays(-1), "openid", "offline_access");

        Assert.Equal(ProtocolErrorCodes.InvalidGrant, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_MissingClient_IsInvalidGrant()
    {
        var sender = new FakeSender
        {
            SignInUserResponse = User(),
            ClientResponse = Result.Fail<ClientForAuthentication>("Not found.")
        };

        var result = await HandleAsync(sender, Now.AddDays(-1), "openid", "offline_access");

        Assert.Equal(ProtocolErrorCodes.InvalidGrant, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }
}
