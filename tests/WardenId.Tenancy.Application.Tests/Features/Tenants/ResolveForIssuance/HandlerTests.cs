using WardenId.Tenancy.Application.Features.Tenants.ResolveForIssuance;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;
using TenantRow = WardenId.Tenancy.Query.Contracts.Features.Tenants.ResolveForIssuance.Item;
using TenantsResolveQuery = WardenId.Tenancy.Query.Contracts.Features.Tenants.ResolveForIssuance.Query;

namespace WardenId.Tenancy.Application.Tests.Features.Tenants.ResolveForIssuance;

// A bare \"Query\" would otherwise bind to the WardenId.Tenancy.Query namespace.
using Query = WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance.Query;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static FakeTenantsQuery QueryReturning(string status) => new()
    {
        ResolveForIssuanceResponse = Result.Ok<TenantRow?>(new TenantRow
        {
            Id = TenantId, Slug = "acme", DisplayName = "Display acme", Status = status
        })
    };

    private static Task<Result<TenantForIssuance>> ResolveAsync(FakeTenantsQuery tenantsQuery, string slug) =>
        new Handler(tenantsQuery).Handle(new Query(slug), CancellationToken.None);

    [Theory]
    [InlineData(Status.Active, TenantIssuanceStatus.Active)]
    [InlineData(Status.Suspended, TenantIssuanceStatus.Suspended)]
    public async Task Handle_ReturnsTenant_WithIssuanceStatus(Status status, TenantIssuanceStatus expected)
    {
        var tenantsQuery = QueryReturning(status.ToString());

        var result = await ResolveAsync(tenantsQuery, "acme");

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantId, result.Value.TenantId);
        Assert.Equal("acme", result.Value.Slug);
        Assert.Equal("Display acme", result.Value.DisplayName);
        Assert.Equal(expected, result.Value.Status);
        Assert.Equal(new TenantsResolveQuery("acme"), Assert.Single(tenantsQuery.ResolveForIssuanceCalls));
    }

    [Theory]
    [InlineData(nameof(Status.Provisioning))]
    [InlineData(nameof(Status.Archived))]
    [InlineData("SomeFutureStatus")]
    public async Task Handle_ReturnsNotFound_ForNonServableStatus(string status) =>
        AssertNotFound(await ResolveAsync(QueryReturning(status), "acme"));

    [Fact]
    public async Task Handle_ReturnsNotFound_ForUnknownSlug() =>
        AssertNotFound(await ResolveAsync(new FakeTenantsQuery(), "other"));

    [Fact]
    public async Task Handle_ReturnsNotFound_ForBlankSlug_WithoutQuerying()
    {
        var tenantsQuery = QueryReturning(nameof(Status.Active));

        AssertNotFound(await ResolveAsync(tenantsQuery, " "));
        Assert.Empty(tenantsQuery.ResolveForIssuanceCalls);
    }

    private static void AssertNotFound(Result<TenantForIssuance> result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(TenantErrorCodes.NotFound, result.Errors[0].Metadata[TenantErrorCodes.MetadataKey]);
    }
}
