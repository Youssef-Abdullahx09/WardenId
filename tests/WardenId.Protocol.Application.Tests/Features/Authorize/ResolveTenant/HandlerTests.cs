using WardenId.Protocol.Application.Features.Authorize.ResolveTenant;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;
using Query = WardenId.Protocol.Application.Features.Authorize.ResolveTenant.Query;

namespace WardenId.Protocol.Application.Tests.Features.Authorize.ResolveTenant;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ReturnsTenantId_ForActiveTenant()
    {
        var sender = new FakeSender
        {
            ResolveForIssuanceResponse = Result.Ok(new TenantForIssuance(TenantId, "acme", TenantIssuanceStatus.Active))
        };

        var result = await new Handler(sender).Handle(new Query("acme"), CancellationToken.None);

        Assert.Equal(TenantId, result.Value);
    }

    [Fact]
    public async Task Handle_FailsNotFound_ForSuspendedTenant()
    {
        var sender = new FakeSender
        {
            ResolveForIssuanceResponse = Result.Ok(new TenantForIssuance(TenantId, "acme", TenantIssuanceStatus.Suspended))
        };

        var result = await new Handler(sender).Handle(new Query("acme"), CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.NotFound, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_FailsNotFound_WhenTenancySaysNotFound()
    {
        var sender = new FakeSender { ResolveForIssuanceResponse = Result.Fail<TenantForIssuance>("nope") };

        var result = await new Handler(sender).Handle(new Query("acme"), CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.NotFound, result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey]);
    }

    [Theory]
    [InlineData("ACME")]
    [InlineData("a")]
    [InlineData("-acme")]
    public async Task Handle_FailsNotFound_ForInvalidSlug_WithoutCallingTenancy(string slug)
    {
        var sender = new FakeSender();

        var result = await new Handler(sender).Handle(new Query(slug), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Empty(sender.Sent);
    }
}
