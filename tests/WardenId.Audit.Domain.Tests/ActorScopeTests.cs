namespace WardenId.Audit.Domain.Tests;

public class ActorScopeTests
{
    [Fact]
    public void Platform_HasNoTenantId()
    {
        var scope = ActorScope.Platform();

        Assert.True(scope.IsPlatform);
        Assert.Null(scope.TenantId);
    }

    [Fact]
    public void ForTenant_IsNotPlatform_AndCarriesTenantId()
    {
        var tenantId = Guid.NewGuid();
        var scope = ActorScope.ForTenant(tenantId);

        Assert.False(scope.IsPlatform);
        Assert.Equal(tenantId, scope.TenantId);
    }

    [Fact]
    public void TwoScopes_WithSameValues_AreEqual()
    {
        var tenantId = Guid.NewGuid();

        Assert.Equal(ActorScope.ForTenant(tenantId), ActorScope.ForTenant(tenantId));
        Assert.Equal(ActorScope.Platform(), ActorScope.Platform());
    }
}
