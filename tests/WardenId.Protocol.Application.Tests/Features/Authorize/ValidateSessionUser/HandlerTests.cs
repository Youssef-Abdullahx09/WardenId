using FluentResults;
using SignInUser = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.SignInUser;
using WardenId.Protocol.Application.Features.Authorize.ValidateSessionUser;
using WardenId.Protocol.Domain;
using RequiredAction = WardenId.Identity.Integration.Command.Features.Users.Authenticate.RequiredAction;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;

namespace WardenId.Protocol.Application.Tests.Features.Authorize.ValidateSessionUser;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Result<SignInUser> User(UserStatus status = UserStatus.Active, params RequiredAction[] actions) =>
        Result.Ok(new SignInUser(UserId, "alice@acme.test", "alice", status, actions));

    private static Task<Result> HandleAsync(Result<SignInUser> user) =>
        new Handler(new FakeSender { SignInUserResponse = user }).Handle(new Query(TenantId, UserId), CancellationToken.None);

    [Fact]
    public async Task Handle_ActiveUser_Succeeds() => Assert.True((await HandleAsync(User())).IsSuccess);

    [Theory]
    [InlineData(UserStatus.Disabled)]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.PendingVerification)]
    public async Task Handle_NonActiveUser_FailsInvalidGrant(UserStatus status) => AssertInvalidGrant(await HandleAsync(User(status)));

    [Fact]
    public async Task Handle_PendingAction_FailsInvalidGrant() =>
        AssertInvalidGrant(await HandleAsync(User(UserStatus.Active, RequiredAction.ResetPassword)));

    [Fact]
    public async Task Handle_UserNotFound_FailsInvalidGrant() =>
        AssertInvalidGrant(await HandleAsync(Result.Fail<SignInUser>("not found")));

    private static void AssertInvalidGrant(Result result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.InvalidGrant, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }
}
