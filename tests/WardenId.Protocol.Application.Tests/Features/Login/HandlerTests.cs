using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Features.Login;

namespace WardenId.Protocol.Application.Tests.Features.Login;

public class HandlerTests
{
    private static AuthenticatedUserInfo ActiveUser(Guid userId) =>
        new(userId, "someone@example.com", true, "someone", IsActive: true, HasPendingActions: false);

    [Fact]
    public async Task Handle_IssuesTokens_WhenUserIsActiveWithNoPendingActions()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var authenticator = new FakeAuthenticateUsers { Response = Result.Ok(ActiveUser(userId)) };
        var signingKeyProvider = new FakeActiveSigningKeyProvider
        {
            Response = Result.Ok(new ActiveSigningKey(Guid.NewGuid(), "ES256", [1, 2, 3]))
        };
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var handler = new Handler(
            authenticator, signingKeyProvider, new FakeJwtIssuer(), refreshTokenRepository,
            Options.Create(new ProtocolTokenOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone", "password"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.RequiresAdditionalAction);
        Assert.NotNull(result.Value.Tokens);
        Assert.Equal("Bearer", result.Value.Tokens!.TokenType);
        Assert.Single(refreshTokenRepository.Created);
    }

    [Fact]
    public async Task Handle_SkipsTokenIssuance_WhenUserHasPendingActions()
    {
        var userId = Guid.NewGuid();
        var authenticator = new FakeAuthenticateUsers
        {
            Response = Result.Ok(ActiveUser(userId) with { HasPendingActions = true })
        };
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var handler = new Handler(
            authenticator, new FakeActiveSigningKeyProvider(), new FakeJwtIssuer(), refreshTokenRepository,
            Options.Create(new ProtocolTokenOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "someone", "password"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.RequiresAdditionalAction);
        Assert.Null(result.Value.Tokens);
        Assert.Empty(refreshTokenRepository.Created);
    }

    [Fact]
    public async Task Handle_SkipsTokenIssuance_WhenUserIsNotActive()
    {
        var userId = Guid.NewGuid();
        var authenticator = new FakeAuthenticateUsers
        {
            Response = Result.Ok(ActiveUser(userId) with { IsActive = false })
        };
        var handler = new Handler(
            authenticator, new FakeActiveSigningKeyProvider(), new FakeJwtIssuer(), new FakeRefreshTokenRepository(),
            Options.Create(new ProtocolTokenOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "someone", "password"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.RequiresAdditionalAction);
    }

    [Fact]
    public async Task Handle_PropagatesFailure_WhenAuthenticationFails()
    {
        var authenticator = new FakeAuthenticateUsers
        {
            Response = Result.Fail<AuthenticatedUserInfo>(
                new Error("Invalid credentials.").WithMetadata("ErrorCode", "InvalidCredentials"))
        };
        var handler = new Handler(
            authenticator, new FakeActiveSigningKeyProvider(), new FakeJwtIssuer(), new FakeRefreshTokenRepository(),
            Options.Create(new ProtocolTokenOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "someone", "wrong"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("InvalidCredentials", result.Errors[0].Metadata["ErrorCode"]);
    }

    [Fact]
    public async Task Handle_PropagatesFailure_WhenNoActiveSigningKeyExists()
    {
        var userId = Guid.NewGuid();
        var authenticator = new FakeAuthenticateUsers { Response = Result.Ok(ActiveUser(userId)) };
        var signingKeyProvider = new FakeActiveSigningKeyProvider
        {
            Response = Result.Fail<ActiveSigningKey>(new Error("Tenant has no active signing key."))
        };
        var handler = new Handler(
            authenticator, signingKeyProvider, new FakeJwtIssuer(), new FakeRefreshTokenRepository(),
            Options.Create(new ProtocolTokenOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "someone", "password"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
