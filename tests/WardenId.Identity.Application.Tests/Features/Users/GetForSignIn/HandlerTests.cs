using WardenId.Identity.Application.Features.Users.GetForSignIn;
using ContractRequiredAction = WardenId.Identity.Integration.Command.Features.Users.Authenticate.RequiredAction;
using ContractUserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;
using SignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;

namespace WardenId.Identity.Application.Tests.Features.Users.GetForSignIn;

public class HandlerTests
{
    private static Task<(Guid TenantId, User User, FakeUsersQuery Repository)> SeedAsync()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        return Task.FromResult((tenantId, user, new FakeUsersQuery().With(user)));
    }

    [Fact]
    public async Task Handle_ReturnsUser_WithMappedStatusAndActions()
    {
        var (tenantId, user, repository) = await SeedAsync();

        var result = await new Handler(repository).Handle(new SignInQuery(tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal("someone@example.com", result.Value.Email);
        Assert.Equal("someone", result.Value.Username);
        Assert.Equal(ContractUserStatus.PendingVerification, result.Value.Status);
        Assert.Contains(ContractRequiredAction.VerifyEmail, result.Value.PendingActions);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForOtherTenant()
    {
        var (_, user, repository) = await SeedAsync();

        var result = await new Handler(repository).Handle(new SignInQuery(Guid.NewGuid(), user.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForUnknownUser()
    {
        var (tenantId, _, repository) = await SeedAsync();

        var result = await new Handler(repository).Handle(new SignInQuery(tenantId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForSoftDeletedUser()
    {
        var (tenantId, user, repository) = await SeedAsync();
        Assert.True(user.SoftDelete().IsSuccess);

        var result = await new Handler(repository).Handle(new SignInQuery(tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_PassesTheQueryLayersFailureThrough()
    {
        var (tenantId, user, query) = await SeedAsync();
        query.Failure = new Error("sql file missing");

        var result = await new Handler(query).Handle(new SignInQuery(tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("sql file missing", result.Errors[0].Message);
    }
}
