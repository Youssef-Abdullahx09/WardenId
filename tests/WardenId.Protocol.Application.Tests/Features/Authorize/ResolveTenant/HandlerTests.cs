using WardenId.Protocol.Application.Features.Authorize.ResolveTenant;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;
using ResolveTenantQuery = WardenId.Protocol.Application.Features.Authorize.ResolveTenant.Query;

namespace WardenId.Protocol.Application.Tests.Features.Authorize.ResolveTenant;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ReturnsTenantIdAndDisplayName_ForActiveTenant()
    {
        var sender = new FakeSender
        {
            ResolveForIssuanceResponse = Result.Ok(
                new TenantForIssuance(TenantId, "acme", TenantIssuanceStatus.Active, "Acme: Café Ops"))
        };

        var result = await new Handler(sender).Handle(new ResolveTenantQuery("acme"), CancellationToken.None);

        Assert.Equal(new ResolvedTenant(TenantId, "Acme: Café Ops"), result.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_FallsBackToTheSlug_WhenTheDisplayNameIsEmpty(string displayName)
    {
        var sender = new FakeSender
        {
            ResolveForIssuanceResponse = Result.Ok(
                new TenantForIssuance(TenantId, "acme", TenantIssuanceStatus.Active, displayName))
        };

        var result = await new Handler(sender).Handle(new ResolveTenantQuery("acme"), CancellationToken.None);

        Assert.Equal("acme", result.Value.DisplayName);
    }

    [Fact]
    public async Task Handle_FailsNotFound_ForSuspendedTenant()
    {
        var sender = new FakeSender
        {
            ResolveForIssuanceResponse = Result.Ok(new TenantForIssuance(TenantId, "acme", TenantIssuanceStatus.Suspended, "Acme"))
        };

        var result = await new Handler(sender).Handle(new ResolveTenantQuery("acme"), CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.NotFound, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_FailsNotFound_WhenTenancySaysNotFound()
    {
        var sender = new FakeSender { ResolveForIssuanceResponse = Result.Fail<TenantForIssuance>("nope") };

        var result = await new Handler(sender).Handle(new ResolveTenantQuery("acme"), CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.NotFound, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Theory]
    [InlineData("ACME")]
    [InlineData("a")]
    [InlineData("-acme")]
    public async Task Handle_FailsNotFound_ForInvalidSlug_WithoutCallingTenancy(string slug)
    {
        var sender = new FakeSender();

        var result = await new Handler(sender).Handle(new ResolveTenantQuery(slug), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Empty(sender.Sent);
    }
}
