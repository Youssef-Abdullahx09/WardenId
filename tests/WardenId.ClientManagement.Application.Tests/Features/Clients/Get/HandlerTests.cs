using WardenId.ClientManagement.Application.Features.Clients.Get;
using GetQuery = WardenId.ClientManagement.Application.Features.Clients.Get.Query;
using ClientsQuery = WardenId.ClientManagement.Query.Contracts.Features.Clients.Get.Query;
using QueryItem = WardenId.ClientManagement.Query.Contracts.Features.Clients.Get.Item;
using QuerySecret = WardenId.ClientManagement.Query.Contracts.Features.Clients.Get.Secret;
using QuerySigningKey = WardenId.ClientManagement.Query.Contracts.Features.Clients.Get.SigningKey;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.Get;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Expiry = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_MapsTheRow_WithParsedEnums_UtcTimes_AndChildren()
    {
        var id = Guid.NewGuid();
        var secretId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var scopeId = Guid.NewGuid();
        var row = new QueryItem
        {
            Id = id,
            ClientId = "client-abc",
            TenantId = TenantId,
            ClientType = nameof(ClientType.Confidential),
            DisplayName = "svc",
            Description = "desc",
            Enabled = true,
            IsFirstParty = true,
            GrantType = (GrantType.AuthorizationCode | GrantType.RefreshToken).ToString(),
            RequirePkce = true,
            ClientAuthenticationMethod = nameof(ClientAuthenticationMethod.PrivateKeyJwt),
            ConsentRequired = true,
            AccessTokenSeconds = 600,
            IdTokenSeconds = 300,
            RefreshTokenSeconds = 86400,
            RedirectUris = ["https://app.example/cb"],
            PostLogoutRedirectUris = ["https://app.example/bye"],
            CorsOrigins = ["https://app.example"],
            AssignedScopeIds = [scopeId],
            BackchannelLogoutUri = "https://app.example/bcl",
            Secrets = [new QuerySecret { Id = secretId, Label = "primary", ExpiresAt = Expiry.UtcDateTime }],
            SigningKeys = [new QuerySigningKey { Id = keyId, Kid = "kid-1", Algorithm = "ES256" }]
        };
        var clientsQuery = new FakeClientsQuery { GetResponse = Result.Ok<QueryItem?>(row) };

        var result = await new Handler(clientsQuery).Handle(new GetQuery(TenantId, id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var detail = result.Value;
        Assert.Equal(new ClientsQuery(TenantId, id), Assert.Single(clientsQuery.GetCalls));
        Assert.Equal(id, detail.Id);
        Assert.Equal("client-abc", detail.ClientId);
        Assert.Equal(TenantId, detail.TenantId);
        Assert.Equal(ClientType.Confidential, detail.ClientType);
        Assert.Equal("svc", detail.DisplayName);
        Assert.Equal("desc", detail.Description);
        Assert.True(detail.Enabled);
        Assert.True(detail.IsFirstParty);
        Assert.Equal(GrantType.AuthorizationCode | GrantType.RefreshToken, detail.GrantType);
        Assert.True(detail.RequirePkce);
        Assert.Equal(ClientAuthenticationMethod.PrivateKeyJwt, detail.ClientAuthenticationMethod);
        Assert.True(detail.ConsentRequired);
        Assert.Equal(600, detail.AccessTokenSeconds);
        Assert.Equal(300, detail.IdTokenSeconds);
        Assert.Equal(86400, detail.RefreshTokenSeconds);
        Assert.Equal(new[] { "https://app.example/cb" }, detail.RedirectUris);
        Assert.Equal(new[] { "https://app.example/bye" }, detail.PostLogoutRedirectUris);
        Assert.Equal(new[] { "https://app.example" }, detail.CorsOrigins);
        Assert.Equal(new[] { scopeId }, detail.AssignedScopeIds);
        Assert.Equal("https://app.example/bcl", detail.BackchannelLogoutUri);
        Assert.Equal(new ClientSecretSummary(secretId, "primary", Expiry), Assert.Single(detail.Secrets));
        Assert.Equal(new ClientSigningKeySummary(keyId, "kid-1", "ES256", null), Assert.Single(detail.SigningKeys));
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTheQueryFindsNoClient()
    {
        var result = await new Handler(new FakeClientsQuery())
            .Handle(new GetQuery(TenantId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(ClientErrorCodes.NotFound, result.Errors[0].Metadata[ClientErrorCodes.MetadataKey]);
    }
}
