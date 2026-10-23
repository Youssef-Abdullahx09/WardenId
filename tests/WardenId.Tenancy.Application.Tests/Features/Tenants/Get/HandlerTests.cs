using WardenId.Tenancy.Application.Features.Tenants.Get;
using KeyRow = WardenId.Tenancy.Query.Contracts.Features.Tenants.Get.SigningKeyItem;
using TenantRow = WardenId.Tenancy.Query.Contracts.Features.Tenants.Get.Item;
using TenantsGetQuery = WardenId.Tenancy.Query.Contracts.Features.Tenants.Get.Query;

namespace WardenId.Tenancy.Application.Tests.Features.Tenants.Get;

// A bare \"Query\" would otherwise bind to the WardenId.Tenancy.Query namespace.
using Query = WardenId.Tenancy.Application.Features.Tenants.Get.Query;

public class HandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_PassesTheTenantId_ToTheQueryLayer()
    {
        var tenantsQuery = new FakeTenantsQuery();
        var tenantId = Guid.NewGuid();

        await new Handler(tenantsQuery).Handle(new Query(tenantId), CancellationToken.None);

        Assert.Equal(new TenantsGetQuery(tenantId), Assert.Single(tenantsQuery.GetCalls));
    }

    [Fact]
    public async Task Handle_MapsTheRow_WithUtcTimesAndParsedStatuses_InQueryOrder()
    {
        var tenantId = Guid.NewGuid();
        var activeKeyId = Guid.NewGuid();
        var retiringKeyId = Guid.NewGuid();
        var tenantsQuery = new FakeTenantsQuery
        {
            GetResponse = Result.Ok<TenantRow?>(new TenantRow
            {
                Id = tenantId,
                Slug = "acme",
                DisplayName = "Acme",
                Status = nameof(Status.Suspended),
                SuspendedAtUtc = Now,
                SuspendedReason = "billing",
                CreatedAt = Now.AddDays(-10),
                SigningKeys =
                [
                    new KeyRow
                    {
                        SigningKeyId = activeKeyId, Algorithm = "ES256", Status = nameof(SigningKeyStatus.Active),
                        CreatedAt = Now.AddDays(-1), ActivatedAt = Now.AddDays(-1)
                    },
                    new KeyRow
                    {
                        SigningKeyId = retiringKeyId, Algorithm = "ES256", Status = nameof(SigningKeyStatus.Retiring),
                        CreatedAt = Now.AddDays(-10), ActivatedAt = Now.AddDays(-10), RetiringSince = Now.AddDays(-1)
                    }
                ]
            })
        };

        var result = await new Handler(tenantsQuery).Handle(new Query(tenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var detail = result.Value;
        Assert.Equal(tenantId, detail.Id);
        Assert.Equal("acme", detail.Slug);
        Assert.Equal("Acme", detail.DisplayName);
        Assert.Equal(Status.Suspended, detail.Status);
        Assert.Equal(new DateTimeOffset(Now), detail.SuspendedAtUtc);
        Assert.Equal(TimeSpan.Zero, detail.CreatedAt.Offset);
        Assert.Equal("billing", detail.SuspendedReason);
        Assert.Collection(detail.SigningKeys,
            active =>
            {
                Assert.Equal(activeKeyId, active.KeyId);
                Assert.Equal(SigningKeyStatus.Active, active.Status);
                Assert.True(active.IsActive);
                Assert.Null(active.RetiringSince);
                Assert.Null(active.RetiredAt);
            },
            retiring =>
            {
                Assert.Equal(retiringKeyId, retiring.KeyId);
                Assert.Equal(SigningKeyStatus.Retiring, retiring.Status);
                Assert.False(retiring.IsActive);
                Assert.Equal(new DateTimeOffset(Now.AddDays(-1)), retiring.RetiringSince);
            });
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_ForUnknownTenant()
    {
        var result = await new Handler(new FakeTenantsQuery()).Handle(new Query(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_PropagatesQueryFailures()
    {
        var tenantsQuery = new FakeTenantsQuery { GetResponse = Result.Fail<TenantRow?>("boom") };

        var result = await new Handler(tenantsQuery).Handle(new Query(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("boom", Assert.Single(result.Errors).Message);
    }
}
