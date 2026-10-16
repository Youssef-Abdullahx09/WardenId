using WardenId.Protocol.Application.Features.Grants;
using WardenId.Protocol.Application.Features.Grants.ListForUser;
using ListForUserQuery = WardenId.Protocol.Application.Features.Grants.ListForUser.Query;

namespace WardenId.Protocol.Application.Tests.Features.Grants.ListForUser;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OtherTenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Task<Result<IReadOnlyList<UserGrant>>> HandleAsync(FakeUserGrantStore store) =>
        new Handler(store).Handle(new ListForUserQuery(TenantId, UserId), CancellationToken.None);

    [Fact]
    public async Task AsksTheStoreForTheUsersSubject()
    {
        var store = new FakeUserGrantStore();

        await HandleAsync(store);

        Assert.Equal(UserId.ToString(), Assert.Single(store.ListCalls));
    }

    [Fact]
    public async Task MapsGrants_NewestFirst_WithOfflineAccessFromScope()
    {
        var store = new FakeUserGrantStore();
        var older = GrantBuilder.Grant(TenantId, UserId, "web-rt", GrantStatus.Valid, GrantBuilder.Now.AddDays(-2),
            "openid", "offline_access");
        var newer = GrantBuilder.Grant(TenantId, UserId, "spa", GrantStatus.Revoked, GrantBuilder.Now, "openid", "profile");
        store.Grants.AddRange([older, newer]);

        var result = await HandleAsync(store);

        Assert.True(result.IsSuccess);
        Assert.Collection(
            result.Value,
            g =>
            {
                Assert.Equal(newer.AuthorizationId, g.AuthorizationId);
                Assert.Equal("spa", g.ClientId);
                Assert.Equal(GrantBuilder.Now, g.CreatedAt);
                Assert.Equal(new[] { "openid", "profile" }, g.Scopes);
                Assert.False(g.OfflineAccess);
                Assert.Equal(GrantStatus.Revoked, g.Status);
            },
            g =>
            {
                Assert.Equal(older.AuthorizationId, g.AuthorizationId);
                Assert.Equal("web-rt", g.ClientId);
                Assert.True(g.OfflineAccess);
                Assert.Equal(GrantStatus.Valid, g.Status);
            });
    }

    // Subject ids are globally unique user ids, but the tenant stamp is what proves a row belongs to {tenantId}.
    [Fact]
    public async Task OnlyReturnsGrantsStampedWithTheTenant()
    {
        var store = new FakeUserGrantStore();
        var mine = GrantBuilder.Grant(TenantId, UserId);
        store.Grants.AddRange([mine, GrantBuilder.Grant(OtherTenantId, UserId), GrantBuilder.Unstamped(UserId)]);

        var result = await HandleAsync(store);

        Assert.Equal(mine.AuthorizationId, Assert.Single(result.Value).AuthorizationId);
    }

    [Fact]
    public async Task NoGrants_ReturnsEmpty()
    {
        var result = await HandleAsync(new FakeUserGrantStore());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
