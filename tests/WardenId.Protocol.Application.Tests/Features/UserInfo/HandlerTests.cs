using WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;
using WardenId.Protocol.Application.Features.UserInfo;
using RequiredAction = WardenId.Identity.Integration.Command.Features.Users.Authenticate.RequiredAction;
using UserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;
using UserInfoQuery = WardenId.Protocol.Application.Features.UserInfo.Query;

namespace WardenId.Protocol.Application.Tests.Features.UserInfo;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Result<SignInUser> User(UserStatus status = UserStatus.Active, params RequiredAction[] actions) =>
        Result.Ok(new SignInUser(UserId, "alice@acme.test", "alice", status, actions));

    private static Task<Result<UserInfoClaims>> HandleAsync(FakeSender sender, params string[] scopes) =>
        new Handler(sender).Handle(new UserInfoQuery(TenantId, UserId, scopes), CancellationToken.None);

    [Fact]
    public async Task ActiveUser_ReturnsSubjectAndScopedClaims()
    {
        var sender = new FakeSender { SignInUserResponse = User() };

        var result = await HandleAsync(sender, "openid", "profile", "email");

        Assert.True(result.IsSuccess);
        Assert.Equal(UserId.ToString(), result.Value.Subject);
        Assert.Equal("alice", result.Value.Claims.PreferredUsername);
        Assert.Equal("alice@acme.test", result.Value.Claims.Email);
        Assert.True(result.Value.Claims.EmailVerified);
        var query = Assert.IsType<GetForSignInQuery>(Assert.Single(sender.Sent));
        Assert.Equal(TenantId, query.TenantId);
        Assert.Equal(UserId, query.UserId);
    }

    [Fact]
    public async Task OpenIdOnly_ReturnsSubjectOnly()
    {
        var sender = new FakeSender { SignInUserResponse = User() };

        var result = await HandleAsync(sender, "openid");

        Assert.Equal(UserId.ToString(), result.Value.Subject);
        Assert.Null(result.Value.Claims.PreferredUsername);
        Assert.Null(result.Value.Claims.Email);
    }

    [Theory]
    [InlineData(UserStatus.Disabled)]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.PendingVerification)]
    public async Task UnusableUser_IsInvalidGrant(UserStatus status)
    {
        var sender = new FakeSender { SignInUserResponse = User(status) };

        var result = await HandleAsync(sender, "openid");

        Assert.Equal(ProtocolErrorCodes.InvalidGrant, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task UserWithPendingAction_IsInvalidGrant()
    {
        var sender = new FakeSender { SignInUserResponse = User(UserStatus.Active, RequiredAction.AcceptTos) };

        var result = await HandleAsync(sender, "openid");

        Assert.Equal(ProtocolErrorCodes.InvalidGrant, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task UnknownUser_IsInvalidGrant()
    {
        var sender = new FakeSender { SignInUserResponse = Result.Fail<SignInUser>("Not found.") };

        var result = await HandleAsync(sender, "openid");

        Assert.Equal(ProtocolErrorCodes.InvalidGrant, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }
}
