using WardenId.ClientManagement.Application.Features.Clients.VerifySecret;
using WardenId.ClientManagement.Integration.Command.Features.Clients.VerifySecret;
using ClientsQuery = WardenId.ClientManagement.Query.Contracts.Features.Clients.VerifySecret.Query;
using QueryItem = WardenId.ClientManagement.Query.Contracts.Features.Clients.VerifySecret.Item;
using QuerySecret = WardenId.ClientManagement.Query.Contracts.Features.Clients.VerifySecret.Secret;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.VerifySecret;

// The sibling namespace WardenId.ClientManagement.Query would otherwise shadow the imported type.
using Query = WardenId.ClientManagement.Integration.Command.Features.Clients.VerifySecret.Query;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string ClientId = "client-abc";
    private const string Secret = "s3cret-primary";

    private static QuerySecret SecretRow(string plainText, DateTimeOffset? expiresAt = null) =>
        new() { HashedValue = ClientSecretHasher.Hash(plainText), ExpiresAt = expiresAt?.UtcDateTime };

    private static QueryItem Row(
        ClientType type = ClientType.Confidential,
        ClientAuthenticationMethod auth = ClientAuthenticationMethod.SharedSecret,
        bool enabled = true,
        params QuerySecret[] secrets) => new()
    {
        Enabled = enabled,
        ClientType = type.ToString(),
        ClientAuthenticationMethod = auth.ToString(),
        Secrets = secrets.Length > 0 ? secrets : [SecretRow(Secret)]
    };

    private static Task<Result> VerifyAsync(QueryItem? row, string presented, FakeClientsQuery? clientsQuery = null)
    {
        clientsQuery ??= new FakeClientsQuery();
        clientsQuery.VerifySecretResponse = Result.Ok(row);
        return new Handler(clientsQuery).Handle(new Query(TenantId, ClientId, presented), CancellationToken.None);
    }

    [Fact]
    public async Task Handle_Succeeds_WhenSecretMatches() =>
        Assert.True((await VerifyAsync(Row(), Secret)).IsSuccess);

    [Fact]
    public async Task Handle_PassesTenantAndClientId_ToTheQueryLayer()
    {
        var clientsQuery = new FakeClientsQuery();

        await VerifyAsync(Row(), Secret, clientsQuery);

        Assert.Equal(new ClientsQuery(TenantId, ClientId), Assert.Single(clientsQuery.VerifySecretCalls));
    }

    [Fact]
    public async Task Handle_Fails_WhenSecretDoesNotMatch() =>
        AssertInvalid(await VerifyAsync(Row(), "wrong"));

    [Fact]
    public async Task Handle_Fails_WhenPresentedSecretIsEmpty_WithoutQuerying()
    {
        var clientsQuery = new FakeClientsQuery();

        AssertInvalid(await VerifyAsync(Row(), "", clientsQuery));
        Assert.Empty(clientsQuery.VerifySecretCalls);
    }

    [Fact]
    public async Task Handle_Fails_WhenOnlyMatchingSecretIsExpired() =>
        AssertInvalid(await VerifyAsync(Row(secrets: [SecretRow("old", DateTimeOffset.UtcNow.AddMinutes(-1))]), "old"));

    [Fact]
    public async Task Handle_AcceptsBothSecrets_DuringRotationOverlap()
    {
        QuerySecret[] secrets = [SecretRow("old", DateTimeOffset.UtcNow.AddDays(1)), SecretRow("new")];

        Assert.True((await VerifyAsync(Row(secrets: secrets), "old")).IsSuccess);
        Assert.True((await VerifyAsync(Row(secrets: secrets), "new")).IsSuccess);
    }

    [Fact]
    public async Task Handle_Fails_ForPublicClient() =>
        AssertInvalid(await VerifyAsync(Row(ClientType.Public), Secret));

    [Fact]
    public async Task Handle_Fails_ForDisabledClient() =>
        AssertInvalid(await VerifyAsync(Row(enabled: false), Secret));

    [Fact]
    public async Task Handle_Fails_ForPrivateKeyJwtClient() =>
        AssertInvalid(await VerifyAsync(Row(auth: ClientAuthenticationMethod.PrivateKeyJwt), Secret));

    [Fact]
    public async Task Handle_Fails_WhenTheQueryFindsNoClient() =>
        AssertInvalid(await VerifyAsync(null, Secret));

    private static void AssertInvalid(Result result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(ClientErrorCodes.InvalidCredentials, result.Errors[0].Metadata[ClientErrorCodes.MetadataKey]);
    }
}
