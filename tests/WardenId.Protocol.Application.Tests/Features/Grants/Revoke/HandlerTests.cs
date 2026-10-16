using WardenId.Protocol.Application.Features.Grants;
using WardenId.Protocol.Application.Features.Grants.Revoke;

namespace WardenId.Protocol.Application.Tests.Features.Grants.Revoke;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Task<Result> HandleAsync(
        FakeUserGrantStore store, FakeSessionAuthorizationRevoker revoker, string authorizationId) =>
        new Handler(store, revoker).Handle(new Command(TenantId, UserId, authorizationId), CancellationToken.None);

    private static void AssertNotFound(Result result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.NotFound, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task UsersGrantInTenant_IsRevokedWithItsTokens()
    {
        var store = new FakeUserGrantStore();
        var grant = GrantBuilder.Grant(TenantId, UserId);
        store.Grants.Add(grant);
        var revoker = new FakeSessionAuthorizationRevoker();

        var result = await HandleAsync(store, revoker, grant.AuthorizationId);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { grant.AuthorizationId }, Assert.Single(revoker.Calls));
    }

    // Idempotent like the token-family revoker: a retry, or tokens left valid by a failed earlier write, still gets 204.
    [Fact]
    public async Task AlreadyRevokedGrant_IsRevokedAgain()
    {
        var store = new FakeUserGrantStore();
        var grant = GrantBuilder.Grant(TenantId, UserId, status: GrantStatus.Revoked);
        store.Grants.Add(grant);
        var revoker = new FakeSessionAuthorizationRevoker();

        var result = await HandleAsync(store, revoker, grant.AuthorizationId);

        Assert.True(result.IsSuccess);
        Assert.Single(revoker.Calls);
    }

    [Fact]
    public async Task UnknownGrant_IsNotFound()
    {
        var revoker = new FakeSessionAuthorizationRevoker();

        var result = await HandleAsync(new FakeUserGrantStore(), revoker, Guid.NewGuid().ToString());

        AssertNotFound(result);
        Assert.Empty(revoker.Calls);
    }

    [Fact]
    public async Task AnotherUsersGrant_IsNotFound()
    {
        var store = new FakeUserGrantStore();
        var grant = GrantBuilder.Grant(TenantId, Guid.NewGuid());
        store.Grants.Add(grant);
        var revoker = new FakeSessionAuthorizationRevoker();

        var result = await HandleAsync(store, revoker, grant.AuthorizationId);

        AssertNotFound(result);
        Assert.Empty(revoker.Calls);
    }

    [Fact]
    public async Task AnotherTenantsGrant_IsNotFound()
    {
        var store = new FakeUserGrantStore();
        var grant = GrantBuilder.Grant(Guid.NewGuid(), UserId);
        store.Grants.Add(grant);
        var revoker = new FakeSessionAuthorizationRevoker();

        var result = await HandleAsync(store, revoker, grant.AuthorizationId);

        AssertNotFound(result);
        Assert.Empty(revoker.Calls);
    }

    [Fact]
    public async Task UnstampedGrant_IsNotFound()
    {
        var store = new FakeUserGrantStore();
        var grant = GrantBuilder.Unstamped(UserId);
        store.Grants.Add(grant);
        var revoker = new FakeSessionAuthorizationRevoker();

        var result = await HandleAsync(store, revoker, grant.AuthorizationId);

        AssertNotFound(result);
        Assert.Empty(revoker.Calls);
    }
}
