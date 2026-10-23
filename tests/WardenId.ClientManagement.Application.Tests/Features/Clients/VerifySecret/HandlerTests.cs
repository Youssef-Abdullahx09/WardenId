using WardenId.ClientManagement.Application.Features.Clients.VerifySecret;
using WardenId.ClientManagement.Integration.Command.Features.Clients.VerifySecret;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.VerifySecret;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static async Task<Result> VerifyAsync(Client client, string presented, Guid? tenantId = null)
    {
        await using var db = await ClientBuilder.SeedAsync(client);
        return await new Handler(db).Handle(
            new Query(tenantId ?? TenantId, client.ClientId.Value, presented), CancellationToken.None);
    }

    [Fact]
    public async Task Handle_Succeeds_WhenSecretMatches() =>
        Assert.True((await VerifyAsync(ClientBuilder.Confidential(TenantId), ClientBuilder.Secret)).IsSuccess);

    [Fact]
    public async Task Handle_Fails_WhenSecretDoesNotMatch() =>
        AssertInvalid(await VerifyAsync(ClientBuilder.Confidential(TenantId), "wrong"));

    [Fact]
    public async Task Handle_Fails_WhenPresentedSecretIsEmpty() =>
        AssertInvalid(await VerifyAsync(ClientBuilder.Confidential(TenantId), ""));

    [Fact]
    public async Task Handle_Fails_WhenOnlyMatchingSecretIsExpired()
    {
        var expired = ClientSecret.Create(ClientSecretHasher.Hash("old"), "old", DateTimeOffset.UtcNow.AddMinutes(-1));
        AssertInvalid(await VerifyAsync(ClientBuilder.Confidential(TenantId, secrets: [expired]), "old"));
    }

    [Fact]
    public async Task Handle_AcceptsBothSecrets_DuringRotationOverlap()
    {
        var old = ClientSecret.Create(ClientSecretHasher.Hash("old"), "old", DateTimeOffset.UtcNow.AddDays(1));
        var fresh = ClientSecret.Create(ClientSecretHasher.Hash("new"), "new", null);

        Assert.True((await VerifyAsync(ClientBuilder.Confidential(TenantId, secrets: [old, fresh]), "old")).IsSuccess);
        Assert.True((await VerifyAsync(ClientBuilder.Confidential(TenantId, secrets: [old, fresh]), "new")).IsSuccess);
    }

    [Fact]
    public async Task Handle_Fails_ForPublicClient() =>
        AssertInvalid(await VerifyAsync(ClientBuilder.Public(TenantId), ClientBuilder.Secret));

    [Fact]
    public async Task Handle_Fails_ForDisabledClient()
    {
        var client = ClientBuilder.Confidential(TenantId);
        client.Disable();
        AssertInvalid(await VerifyAsync(client, ClientBuilder.Secret));
    }

    [Fact]
    public async Task Handle_Fails_ForPrivateKeyJwtClient() =>
        AssertInvalid(await VerifyAsync(
            ClientBuilder.Confidential(TenantId, auth: ClientAuthenticationMethod.PrivateKeyJwt), ClientBuilder.Secret));

    [Fact]
    public async Task Handle_Fails_ForSoftDeletedClient()
    {
        var client = ClientBuilder.Confidential(TenantId);
        client.Delete();
        AssertInvalid(await VerifyAsync(client, ClientBuilder.Secret));
    }

    [Fact]
    public async Task Handle_Fails_ForOtherTenant() =>
        AssertInvalid(await VerifyAsync(ClientBuilder.Confidential(TenantId), ClientBuilder.Secret, Guid.NewGuid()));

    private static void AssertInvalid(Result result)
    {
        Assert.True(result.IsFailed);
        Assert.Equal(ClientErrorCodes.InvalidCredentials, result.Errors[0].Metadata[ClientErrorCodes.MetadataKey]);
    }
}
