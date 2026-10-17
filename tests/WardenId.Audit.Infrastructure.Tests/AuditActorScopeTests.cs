using Microsoft.EntityFrameworkCore;
using WardenId.Audit.Infrastructure.Tests.Fakes;

namespace WardenId.Audit.Infrastructure.Tests;

/// <summary>The scope Protocol opens around a known user's Identity writes (sign-in steps, account pages).</summary>
public class AuditActorScopeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    // What AuditContextMiddleware builds for a browser request with no access token.
    private static AuditAmbientContext AnonymousRequest() => new(
        Actor.Anonymous(ActorScope.Platform()),
        TenantId: null,
        Correlation.Create(requestId: "req-1", traceId: "trace-1"),
        ClientContext.Create("203.0.113.7", "Mozilla/5.0").Value);

    [Fact]
    public void BeginEndUser_NamesTheUserInTheirTenant_KeepsClientAndCorrelation_AndRestoresOnDispose()
    {
        var accessor = new AsyncLocalAuditContextAccessor();
        var request = AnonymousRequest();
        using var requestScope = accessor.BeginScope(request);

        using (new AuditActorScope(accessor).BeginEndUser(TenantId, UserId))
        {
            var current = accessor.Current!;
            Assert.Equal(ActorType.EndUser, current.Actor.Type);
            Assert.Equal(UserId.ToString(), current.Actor.Id);
            Assert.Equal(TenantId, current.Actor.Scope.TenantId);
            Assert.Equal(TenantId, current.TenantId);
            Assert.Equal(request.Client, current.Client);
            Assert.Equal(request.Correlation, current.Correlation);
        }

        Assert.Same(request, accessor.Current);
    }

    [Fact]
    public void BeginEndUser_OutsideARequest_UsesUnknownClientAndNoCorrelation()
    {
        var accessor = new AsyncLocalAuditContextAccessor();

        using (new AuditActorScope(accessor).BeginEndUser(TenantId, UserId))
        {
            Assert.Equal(ClientContext.Unknown(), accessor.Current!.Client);
            Assert.Equal(Correlation.None(), accessor.Current.Correlation);
        }

        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task Capture_InsideTheScope_NamesTheUser_InTheTenantPartition()
    {
        var accessor = new AsyncLocalAuditContextAccessor();
        await using var db = TestAuditableDbContext.CreateNew(
            nameof(Capture_InsideTheScope_NamesTheUser_InTheTenantPartition), new AuditCaptureInterceptor(accessor));
        using var requestScope = accessor.BeginScope(AnonymousRequest());

        using (new AuditActorScope(accessor).BeginEndUser(TenantId, UserId))
        {
            db.TestAggregates.Add(TestAggregate.Create("Acme"));
            await db.SaveChangesAsync(UserId, CancellationToken.None);
        }

        var record = await db.AuditEvents.SingleAsync();
        Assert.Equal($"tenant:{TenantId}:enduser:{UserId}", record.Actor);
        Assert.Equal(UserId.ToString(), record.ActorId);
        Assert.Equal(TenantId, record.TenantId);
    }
}
