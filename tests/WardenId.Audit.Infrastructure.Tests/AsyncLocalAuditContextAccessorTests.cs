namespace WardenId.Audit.Infrastructure.Tests;

public class AsyncLocalAuditContextAccessorTests
{
    private static AuditAmbientContext MakeContext(string actorId) => new(
        Actor.Create(ActorType.AdminUser, actorId, ActorScope.Platform()).Value,
        TenantId: null,
        Correlation.None(),
        ClientContext.Unknown());

    [Fact]
    public void Current_IsNull_BeforeAnyScope()
    {
        var accessor = new AsyncLocalAuditContextAccessor();

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void BeginScope_SetsCurrent_ForTheDurationOfTheScope()
    {
        var accessor = new AsyncLocalAuditContextAccessor();
        var context = MakeContext("admin-1");

        using (accessor.BeginScope(context))
        {
            Assert.Same(context, accessor.Current);
        }

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void NestedScopes_RestorePreviousContext_OnInnerDispose()
    {
        var accessor = new AsyncLocalAuditContextAccessor();
        var outer = MakeContext("outer");
        var inner = MakeContext("inner");

        using (accessor.BeginScope(outer))
        {
            using (accessor.BeginScope(inner))
            {
                Assert.Same(inner, accessor.Current);
            }

            Assert.Same(outer, accessor.Current);
        }

        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task Context_DoesNotLeak_AcrossConcurrentAsyncFlows()
    {
        // I7's whole point is per-flow isolation — prove two logical flows never see each
        // other's ambient actor even when they run concurrently.
        var accessor = new AsyncLocalAuditContextAccessor();

        async Task<string?> RunFlow(string actorId)
        {
            using (accessor.BeginScope(MakeContext(actorId)))
            {
                await Task.Delay(10);
                return accessor.Current?.Actor.Id;
            }
        }

        var results = await Task.WhenAll(RunFlow("flow-a"), RunFlow("flow-b"));

        Assert.Contains("flow-a", results);
        Assert.Contains("flow-b", results);
    }
}
