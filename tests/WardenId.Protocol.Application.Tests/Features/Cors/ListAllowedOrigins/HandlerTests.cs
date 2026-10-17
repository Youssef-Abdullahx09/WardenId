using WardenId.Protocol.Application.Features.Cors.ListAllowedOrigins;
using AllowedOriginsQuery = WardenId.Protocol.Application.Features.Cors.ListAllowedOrigins.Query;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;
using ListCorsOriginsQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.ListCorsOrigins.Query;
using ResolveForIssuanceQuery = WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance.Query;

namespace WardenId.Protocol.Application.Tests.Features.Cors.ListAllowedOrigins;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static StubSender Tenant(TenantIssuanceStatus status) =>
        new StubSender().Returns<ResolveForIssuanceQuery, Result<TenantForIssuance>>(
            Result.Ok(new TenantForIssuance(TenantId, "acme", status)));

    [Fact]
    public async Task ActiveTenant_ReturnsItsClientsOrigins()
    {
        var sender = Tenant(TenantIssuanceStatus.Active)
            .Returns<ListCorsOriginsQuery, Result<IReadOnlyList<string>>>(Result.Ok<IReadOnlyList<string>>(["https://spa.example"]));

        var origins = await new Handler(sender).Handle(new AllowedOriginsQuery("acme"), CancellationToken.None);

        Assert.Equal(new[] { "https://spa.example" }, origins);
        Assert.Contains(new ListCorsOriginsQuery(TenantId), sender.Sent);
    }

    [Fact]
    public async Task SuspendedTenant_AllowsNoOrigin()
    {
        var sender = Tenant(TenantIssuanceStatus.Suspended);

        Assert.Empty(await new Handler(sender).Handle(new AllowedOriginsQuery("acme"), CancellationToken.None));
    }

    [Fact]
    public async Task UnknownTenant_AllowsNoOrigin()
    {
        var sender = new StubSender().Returns<ResolveForIssuanceQuery, Result<TenantForIssuance>>(
            Result.Fail<TenantForIssuance>("Not found."));

        Assert.Empty(await new Handler(sender).Handle(new AllowedOriginsQuery("nope"), CancellationToken.None));
    }

    [Fact]
    public async Task MalformedSlug_AllowsNoOrigin_WithoutALookup()
    {
        var sender = new StubSender();

        Assert.Empty(await new Handler(sender).Handle(new AllowedOriginsQuery("ACME"), CancellationToken.None));
        Assert.Empty(sender.Sent);
    }
}
