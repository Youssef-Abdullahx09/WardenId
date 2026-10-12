using Microsoft.Extensions.Options;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;
using WardenId.Protocol.Application.Features.Token.AuthorizationCode;
using RequiredAction = WardenId.Identity.Integration.Command.Features.Users.Authenticate.RequiredAction;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;

namespace WardenId.Protocol.Application.Tests.Features.Token.AuthorizationCode;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Result<SignInUser> User(UserStatus status = UserStatus.Active, params RequiredAction[] actions) =>
        Result.Ok(new SignInUser(UserId, "alice@acme.test", "alice", status, actions));

    private static Result<ClientForAuthentication> Client(int? lifetimeSeconds = null) =>
        Result.Ok(new ClientForAuthentication(
            Guid.NewGuid(), "web", ClientKind.Confidential, true, [AllowedGrant.AuthorizationCode],
            ClientAuthMethod.SharedSecret, lifetimeSeconds, ["https://app.example/cb"], false));

    private static Task<Result<AuthorizationCodeGrant>> HandleAsync(FakeSender sender, params string[] scopes) =>
        new Handler(sender, Options.Create(new ProtocolTokenOptions { IssuerBaseUri = "https://wardenid.test" }))
            .Handle(new Command(TenantId, "acme", "web", UserId, scopes), CancellationToken.None);

    [Fact]
    public async Task Handle_OpenIdOnly_HasNoUserClaims()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client() };

        var result = await HandleAsync(sender, "openid");

        Assert.True(result.IsSuccess);
        Assert.Equal(UserId.ToString(), result.Value.Subject);
        Assert.Equal(TenantId, result.Value.TenantId);
        Assert.Equal("https://wardenid.test/tenants/acme/api", result.Value.Audience);
        Assert.Equal(TimeSpan.FromMinutes(15), result.Value.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromMinutes(15), result.Value.IdTokenLifetime);
        Assert.Null(result.Value.PreferredUsername);
        Assert.Null(result.Value.Email);
        Assert.Null(result.Value.EmailVerified);
    }

    [Fact]
    public async Task Handle_EmailScope_AddsEmailAndVerified()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client() };

        var result = await HandleAsync(sender, "openid", "email");

        Assert.Equal("alice@acme.test", result.Value.Email);
        Assert.True(result.Value.EmailVerified);
        Assert.Null(result.Value.PreferredUsername);
    }

    [Fact]
    public async Task Handle_ProfileScope_AddsPreferredUsername()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client() };

        var result = await HandleAsync(sender, "openid", "profile");

        Assert.Equal("alice", result.Value.PreferredUsername);
        Assert.Null(result.Value.Email);
    }

    [Fact]
    public async Task Handle_UsesClientLifetimeOverride()
    {
        var sender = new FakeSender { SignInUserResponse = User(), ClientResponse = Client(lifetimeSeconds: 60) };

        var result = await HandleAsync(sender, "openid");

        Assert.Equal(TimeSpan.FromSeconds(60), result.Value.AccessTokenLifetime);
    }

    [Fact]
    public async Task Handle_FailsInvalidGrant_WhenUserNotFound()
    {
        var sender = new FakeSender { SignInUserResponse = Result.Fail<SignInUser>("Not found."), ClientResponse = Client() };

        var result = await HandleAsync(sender, "openid");

        Assert.Equal(ProtocolErrorCodes.InvalidGrant, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Theory]
    [InlineData(UserStatus.Disabled)]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.PendingVerification)]
    public async Task Handle_FailsInvalidGrant_WhenUserNotActive(UserStatus status)
    {
        var sender = new FakeSender { SignInUserResponse = User(status), ClientResponse = Client() };

        var result = await HandleAsync(sender, "openid");

        Assert.Equal(ProtocolErrorCodes.InvalidGrant, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_FailsInvalidGrant_WhenUserHasPendingActions()
    {
        var sender = new FakeSender { SignInUserResponse = User(UserStatus.Active, RequiredAction.AcceptTos), ClientResponse = Client() };

        var result = await HandleAsync(sender, "openid");

        Assert.True(result.IsFailed);
    }
}
