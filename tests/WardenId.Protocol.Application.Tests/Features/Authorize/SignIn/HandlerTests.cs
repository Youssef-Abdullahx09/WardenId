using WardenId.Protocol.Application.Features.Authorize.SignIn;
using AuthenticatedUser = WardenId.Identity.Integration.Command.Features.Users.Authenticate.AuthenticatedUser;
using RequiredAction = WardenId.Identity.Integration.Command.Features.Users.Authenticate.RequiredAction;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;

namespace WardenId.Protocol.Application.Tests.Features.Authorize.SignIn;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Task<Result<SignInOutcome>> SignInAsync(Result<AuthenticatedUser> authentication) =>
        new Handler(new FakeSender { AuthenticateResponse = authentication })
            .Handle(new Command(TenantId, "alice", "pw"), CancellationToken.None);

    private static Result<AuthenticatedUser> User(UserStatus status, params RequiredAction[] actions) =>
        Result.Ok(new AuthenticatedUser(UserId, "alice@acme.test", "alice", status, actions));

    [Fact]
    public async Task Handle_SignsIn_ActiveUserWithoutActions()
    {
        var result = await SignInAsync(User(UserStatus.Active));

        Assert.True(result.IsSuccess);
        Assert.Equal(UserId, result.Value.UserId);
        Assert.Null(result.Value.BlockedReason);
    }

    [Fact]
    public async Task Handle_FailsInvalidCredentials_WhenAuthenticationFails()
    {
        var result = await SignInAsync(Result.Fail<AuthenticatedUser>("Account locked."));

        Assert.Equal(ProtocolErrorCodes.InvalidCredentials, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
        Assert.Equal("Invalid username or password.", result.Errors[0].Message);
    }

    [Theory]
    [InlineData(UserStatus.Disabled)]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.SoftDeleted)]
    public async Task Handle_FailsInvalidCredentials_ForUnusableStatus(UserStatus status)
    {
        var result = await SignInAsync(User(status));

        Assert.Equal(ProtocolErrorCodes.InvalidCredentials, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Theory]
    [InlineData(RequiredAction.VerifyEmail, SignInBlockReason.VerifyEmail)]
    [InlineData(RequiredAction.ResetPassword, SignInBlockReason.ResetPassword)]
    [InlineData(RequiredAction.AcceptTos, SignInBlockReason.AcceptTos)]
    [InlineData(RequiredAction.EnrollMfa, SignInBlockReason.EnrollMfa)]
    public async Task Handle_Blocks_ForEachPendingAction(RequiredAction action, SignInBlockReason expected)
    {
        var result = await SignInAsync(User(UserStatus.Active, action));

        Assert.Equal(expected, result.Value.BlockedReason);
    }

    [Fact]
    public async Task Handle_BlockReason_FollowsFixedPriority()
    {
        var result = await SignInAsync(User(UserStatus.PendingVerification, RequiredAction.EnrollMfa, RequiredAction.VerifyEmail));

        Assert.Equal(SignInBlockReason.VerifyEmail, result.Value.BlockedReason);
    }

    [Fact]
    public async Task Handle_Blocks_PendingVerificationWithoutActions()
    {
        var result = await SignInAsync(User(UserStatus.PendingVerification));

        Assert.Equal(SignInBlockReason.PendingVerification, result.Value.BlockedReason);
    }
}
