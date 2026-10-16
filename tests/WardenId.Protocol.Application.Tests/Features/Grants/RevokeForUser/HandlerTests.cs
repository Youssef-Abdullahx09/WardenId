using WardenId.Protocol.Application.Features.Grants;
using WardenId.Protocol.Application.Features.Grants.RevokeForUser;

namespace WardenId.Protocol.Application.Tests.Features.Grants.RevokeForUser;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Task<Result> HandleAsync(FakeUserGrantStore store, FakeSessionAuthorizationRevoker revoker) =>
        new Handler(store, revoker).Handle(new Command(TenantId, UserId), CancellationToken.None);

    [Fact]
    public async Task RevokesEveryValidGrantOfTheUserInTheTenant_InOneCall()
    {
        var store = new FakeUserGrantStore();
        var first = GrantBuilder.Grant(TenantId, UserId, "web");
        var second = GrantBuilder.Grant(TenantId, UserId, "web-rt", scopes: ["openid", "offline_access"]);
        store.Grants.AddRange(
        [
            first,
            second,
            GrantBuilder.Grant(TenantId, UserId, status: GrantStatus.Revoked),
            GrantBuilder.Grant(Guid.NewGuid(), UserId),
            GrantBuilder.Grant(TenantId, Guid.NewGuid()),
            GrantBuilder.Unstamped(UserId)
        ]);
        var revoker = new FakeSessionAuthorizationRevoker();

        var result = await HandleAsync(store, revoker);

        Assert.True(result.IsSuccess);
        var revoked = Assert.Single(revoker.Calls);
        Assert.Equal(
            new[] { first.AuthorizationId, second.AuthorizationId }.Order(),
            revoked.Order());
    }

    [Fact]
    public async Task NoGrants_SucceedsWithoutRevoking()
    {
        var revoker = new FakeSessionAuthorizationRevoker();

        var result = await HandleAsync(new FakeUserGrantStore(), revoker);

        Assert.True(result.IsSuccess);
        Assert.Empty(revoker.Calls);
    }
}
