using Microsoft.Extensions.Options;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.Protocol.Application.Features.Token.ClientCredentials;

namespace WardenId.Protocol.Application.Tests.Features.Token.ClientCredentials;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Result<ClientForAuthentication> Client(int? lifetimeSeconds = null) =>
        Result.Ok(new ClientForAuthentication(
            Guid.NewGuid(), "svc", ClientKind.Confidential, true, [AllowedGrant.ClientCredentials],
            ClientAuthMethod.SharedSecret, lifetimeSeconds, [], false));

    private static Handler CreateHandler(FakeSender sender, ProtocolTokenOptions? options = null) =>
        new(sender, Options.Create(options ?? new ProtocolTokenOptions { IssuerBaseUri = "https://wardenid.test" }));

    [Fact]
    public async Task Handle_UsesDefaultAudienceTemplate_AndDefaultLifetime()
    {
        var sender = new FakeSender { ClientResponse = Client() };

        var result = await CreateHandler(sender).Handle(new Command(TenantId, "acme", "svc"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("svc", result.Value.Subject);
        Assert.Equal(TenantId, result.Value.TenantId);
        Assert.Equal("https://wardenid.test/tenants/acme/api", result.Value.Audience);
        Assert.Equal(TimeSpan.FromMinutes(15), result.Value.AccessTokenLifetime);
    }

    [Fact]
    public async Task Handle_UsesConfiguredAudienceTemplate()
    {
        var sender = new FakeSender { ClientResponse = Client() };
        var options = new ProtocolTokenOptions { IssuerBaseUri = "https://wardenid.test", DefaultAudience = "urn:{issuer}:resources" };

        var result = await CreateHandler(sender, options).Handle(new Command(TenantId, "acme", "svc"), CancellationToken.None);

        Assert.Equal("urn:https://wardenid.test/tenants/acme:resources", result.Value.Audience);
    }

    [Fact]
    public async Task Handle_DoesNotDoubleSlash_WhenBaseUriHasTrailingSlash()
    {
        var sender = new FakeSender { ClientResponse = Client() };
        var options = new ProtocolTokenOptions { IssuerBaseUri = "https://wardenid.test/" };

        var result = await CreateHandler(sender, options).Handle(new Command(TenantId, "acme", "svc"), CancellationToken.None);

        Assert.Equal("https://wardenid.test/tenants/acme/api", result.Value.Audience);
    }

    [Fact]
    public async Task Handle_UsesClientLifetimeOverride()
    {
        var sender = new FakeSender { ClientResponse = Client(lifetimeSeconds: 60) };

        var result = await CreateHandler(sender).Handle(new Command(TenantId, "acme", "svc"), CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(60), result.Value.AccessTokenLifetime);
    }

    [Fact]
    public async Task Handle_PropagatesClientLookupFailure()
    {
        var sender = new FakeSender
        {
            ClientResponse = Result.Fail<ClientForAuthentication>(new Error("Client not found.").WithMetadata("ErrorCode", "NotFound"))
        };

        var result = await CreateHandler(sender).Handle(new Command(TenantId, "acme", "svc"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("NotFound", result.Errors[0].Metadata["ErrorCode"]);
    }
}
