using WardenId.ClientManagement.Application.Features.Clients.GetForAuthentication;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using ClientsQuery = WardenId.ClientManagement.Query.Contracts.Features.Clients.GetForAuthentication.Query;
using QueryItem = WardenId.ClientManagement.Query.Contracts.Features.Clients.GetForAuthentication.Item;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.GetForAuthentication;

// The sibling namespace WardenId.ClientManagement.Query would otherwise shadow the imported type.
using Query = WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication.Query;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string ClientId = "client-abc";

    private static QueryItem Row(
        ClientType type = ClientType.Confidential,
        GrantType grants = GrantType.ClientCredentials,
        ClientAuthenticationMethod auth = ClientAuthenticationMethod.SharedSecret,
        bool enabled = true) => new()
    {
        Id = Guid.NewGuid(),
        ClientId = ClientId,
        ClientType = type.ToString(),
        Enabled = enabled,
        GrantType = grants.ToString(),
        ClientAuthenticationMethod = auth.ToString()
    };

    private static Task<Result<ClientForAuthentication>> HandleAsync(FakeClientsQuery clientsQuery, string clientId = ClientId) =>
        new Handler(clientsQuery).Handle(new Query(TenantId, clientId), CancellationToken.None);

    [Fact]
    public async Task Handle_PassesTenantAndClientId_ToTheQueryLayer()
    {
        var clientsQuery = new FakeClientsQuery();

        await HandleAsync(clientsQuery);

        Assert.Equal(new ClientsQuery(TenantId, ClientId), Assert.Single(clientsQuery.GetForAuthenticationCalls));
    }

    [Fact]
    public async Task Handle_ReturnsClient_WithMappedFields()
    {
        var row = new QueryItem
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            ClientType = nameof(ClientType.Confidential),
            Enabled = true,
            // As EF's string converter persists a [Flags] value.
            GrantType = (GrantType.AuthorizationCode | GrantType.ClientCredentials | GrantType.RefreshToken).ToString(),
            ClientAuthenticationMethod = nameof(ClientAuthenticationMethod.SharedSecret),
            AccessTokenSeconds = 600,
            IdTokenSeconds = 300,
            RefreshTokenSeconds = 86400
        };
        var clientsQuery = new FakeClientsQuery { GetForAuthenticationResponse = Result.Ok<QueryItem?>(row) };

        var result = await HandleAsync(clientsQuery);

        Assert.True(result.IsSuccess);
        var value = result.Value;
        Assert.Equal(row.Id, value.Id);
        Assert.Equal(ClientId, value.ClientId);
        Assert.Equal(ClientKind.Confidential, value.Kind);
        Assert.True(value.Enabled);
        Assert.Equal(
            new[] { AllowedGrant.AuthorizationCode, AllowedGrant.ClientCredentials, AllowedGrant.RefreshToken },
            value.AllowedGrants);
        Assert.Equal(ClientAuthMethod.SharedSecret, value.AuthenticationMethod);
        Assert.Equal(600, value.AccessTokenLifetimeSeconds);
        Assert.Equal(86400, value.RefreshTokenLifetimeSeconds);
        Assert.Equal(300, value.IdTokenLifetimeSeconds);
    }

    [Fact]
    public async Task Handle_MapsPublicKind_AndPrivateKeyJwt()
    {
        var clientsQuery = new FakeClientsQuery
        {
            GetForAuthenticationResponse = Result.Ok<QueryItem?>(
                Row(ClientType.Public, auth: ClientAuthenticationMethod.PrivateKeyJwt))
        };

        var result = await HandleAsync(clientsQuery);

        Assert.Equal(ClientKind.Public, result.Value.Kind);
        Assert.Equal(ClientAuthMethod.PrivateKeyJwt, result.Value.AuthenticationMethod);
    }

    [Fact]
    public async Task Handle_ReturnsDisabledClient_WithEnabledFalse()
    {
        var clientsQuery = new FakeClientsQuery { GetForAuthenticationResponse = Result.Ok<QueryItem?>(Row(enabled: false)) };

        var result = await HandleAsync(clientsQuery);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Enabled);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTheQueryFindsNoClient() =>
        AssertNotFound(await HandleAsync(new FakeClientsQuery()));

    [Fact]
    public async Task Handle_ReturnsNotFound_ForEmptyClientId_WithoutQuerying()
    {
        var clientsQuery = new FakeClientsQuery { GetForAuthenticationResponse = Result.Ok<QueryItem?>(Row()) };

        AssertNotFound(await HandleAsync(clientsQuery, ""));
        Assert.Empty(clientsQuery.GetForAuthenticationCalls);
    }

    [Fact]
    public async Task Handle_MapsUris_RequirePkce_AndBackchannelLogoutUri()
    {
        var row = new QueryItem
        {
            Id = Guid.NewGuid(),
            ClientId = ClientId,
            ClientType = nameof(ClientType.Public),
            Enabled = true,
            GrantType = nameof(GrantType.AuthorizationCode),
            ClientAuthenticationMethod = nameof(ClientAuthenticationMethod.SharedSecret),
            RedirectUris = ["https://spa.example/cb", "https://spa.example/silent"],
            RequirePkce = true,
            PostLogoutRedirectUris = ["https://spa.example/bye"],
            BackchannelLogoutUri = "https://spa.example/bcl"
        };
        var clientsQuery = new FakeClientsQuery { GetForAuthenticationResponse = Result.Ok<QueryItem?>(row) };

        var result = await HandleAsync(clientsQuery);

        Assert.Equal(new[] { "https://spa.example/cb", "https://spa.example/silent" }, result.Value.RedirectUris);
        Assert.True(result.Value.RequirePkce);
        Assert.Equal(new[] { "https://spa.example/bye" }, result.Value.PostLogoutRedirectUris);
        Assert.Equal("https://spa.example/bcl", result.Value.BackchannelLogoutUri);
    }

    [Fact]
    public async Task Handle_ClientWithoutLogoutSettings_HasNoPostLogoutUris_AndNullBackchannelUri()
    {
        var clientsQuery = new FakeClientsQuery { GetForAuthenticationResponse = Result.Ok<QueryItem?>(Row()) };

        var result = await HandleAsync(clientsQuery);

        Assert.Empty(result.Value.RedirectUris);
        Assert.Empty(result.Value.PostLogoutRedirectUris);
        Assert.Null(result.Value.BackchannelLogoutUri);
        Assert.Null(result.Value.AccessTokenLifetimeSeconds);
    }

    [Fact]
    public async Task Handle_PropagatesQueryFailure()
    {
        var clientsQuery = new FakeClientsQuery { GetForAuthenticationResponse = Result.Fail("boom") };

        var result = await HandleAsync(clientsQuery);

        Assert.True(result.IsFailed);
        Assert.Equal("boom", result.Errors[0].Message);
    }

    private static void AssertNotFound(Result<ClientForAuthentication> result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(ClientErrorCodes.NotFound, result.Errors[0].Metadata[ClientErrorCodes.MetadataKey]);
    }
}
