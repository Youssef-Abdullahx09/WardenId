namespace WardenId.Audit.Domain.Tests;

public class ActorTests
{
    [Fact]
    public void Create_WithEmptyId_Fails()
    {
        var result = Actor.Create(ActorType.AdminUser, "", ActorScope.Platform());

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_WithWhitespaceId_Fails()
    {
        var result = Actor.Create(ActorType.AdminUser, "   ", ActorScope.Platform());

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void ToSerializedForm_PlatformActor_MatchesSpecFormat()
    {
        var actor = Actor.Create(ActorType.AdminUser, "abc-123", ActorScope.Platform()).Value;

        Assert.Equal("platform:adminuser:abc-123", actor.ToSerializedForm());
    }

    [Fact]
    public void ToSerializedForm_TenantActor_MatchesSpecFormat()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var actor = Actor.Create(ActorType.OAuthClient, "client-1", ActorScope.ForTenant(tenantId)).Value;

        Assert.Equal($"tenant:{tenantId}:oauthclient:client-1", actor.ToSerializedForm());
    }

    [Fact]
    public void Anonymous_NeverFails_AndCarriesGivenScope()
    {
        var actor = Actor.Anonymous(ActorScope.Platform());

        Assert.Equal(ActorType.Anonymous, actor.Type);
        Assert.False(string.IsNullOrWhiteSpace(actor.Id));
    }

    [Fact]
    public void Create_WithOnBehalfOf_PreservesImpersonationChain()
    {
        var real = Actor.Create(ActorType.AdminUser, "real-admin", ActorScope.Platform()).Value;
        var impersonator = Actor.Create(ActorType.AdminUser, "support-agent", ActorScope.Platform(), real).Value;

        Assert.Same(real, impersonator.OnBehalfOf);
    }
}
