using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Features.Login;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning;
using AuthenticatedUser = WardenId.Identity.Integration.Command.Features.Users.Authenticate.AuthenticatedUser;
using AuthenticatedUserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;
using AuthenticatedUserRequiredAction = WardenId.Identity.Integration.Command.Features.Users.Authenticate.RequiredAction;

namespace WardenId.Protocol.Application.Tests.Features.Login;

public class HandlerTests
{
    private static AuthenticatedUser ActiveUser(Guid userId) =>
        new(userId, "someone@example.com", "someone", AuthenticatedUserStatus.Active, []);

    [Fact]
    public async Task Handle_IssuesTokens_WhenUserIsActiveWithNoPendingActions()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var sender = new FakeSender
        {
            AuthenticateResponse = Result.Ok(ActiveUser(userId)),
            SigningKeyResponse = Result.Ok(new ActiveSigningKeyDto(Guid.NewGuid(), "ES256", [1, 2, 3]))
        };
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var handler = new Handler(sender, new FakeJwtIssuer(), refreshTokenRepository, Options.Create(new ProtocolTokenOptions()));

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
        var sender = new FakeSender
        {
            AuthenticateResponse = Result.Ok(ActiveUser(userId) with { PendingActions = [AuthenticatedUserRequiredAction.VerifyEmail] })
        };
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var handler = new Handler(sender, new FakeJwtIssuer(), refreshTokenRepository, Options.Create(new ProtocolTokenOptions()));

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
        var sender = new FakeSender
        {
            AuthenticateResponse = Result.Ok(ActiveUser(userId) with { Status = AuthenticatedUserStatus.Disabled })
        };
        var handler = new Handler(sender, new FakeJwtIssuer(), new FakeRefreshTokenRepository(), Options.Create(new ProtocolTokenOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "someone", "password"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.RequiresAdditionalAction);
    }

    [Fact]
    public async Task Handle_PropagatesFailure_WhenAuthenticationFails()
    {
        var sender = new FakeSender
        {
            AuthenticateResponse = Result.Fail<AuthenticatedUser>(
                new Error("Invalid credentials.").WithMetadata("ErrorCode", "InvalidCredentials"))
        };
        var handler = new Handler(sender, new FakeJwtIssuer(), new FakeRefreshTokenRepository(), Options.Create(new ProtocolTokenOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "someone", "wrong"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("InvalidCredentials", result.Errors[0].Metadata["ErrorCode"]);
    }

    [Fact]
    public async Task Handle_PropagatesFailure_WhenNoActiveSigningKeyExists()
    {
        var userId = Guid.NewGuid();
        var sender = new FakeSender
        {
            AuthenticateResponse = Result.Ok(ActiveUser(userId)),
            SigningKeyResponse = Result.Fail<ActiveSigningKeyDto>(new Error("Tenant has no active signing key."))
        };
        var handler = new Handler(sender, new FakeJwtIssuer(), new FakeRefreshTokenRepository(), Options.Create(new ProtocolTokenOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "someone", "password"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
