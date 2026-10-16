using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Features.Clients.Update;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients.Update;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static Command Patch(
        Guid clientId,
        string? description = null,
        GrantType? grantType = null,
        bool? isFirstParty = null,
        string? displayName = null,
        Guid? tenantId = null) =>
        new(
            tenantId ?? TenantId, clientId, displayName, description, Enabled: null, isFirstParty,
            ConsentRequired: null, grantType, AccessTokenSeconds: null, IdTokenSeconds: null,
            RefreshTokenSeconds: null, ClearTokenLifetimeOverrides: false, ActorId);

    private static async Task<Client> StoredAsync(TestClientManagementDbContext db, Guid id)
    {
        db.ChangeTracker.Clear();
        return await db.Clients.SingleAsync(c => c.Id == id);
    }

    private static void AssertErrorCode(Result result, string code) =>
        Assert.Equal(code, result.Errors[0].Metadata[ClientErrorCodes.MetadataKey]);

    [Fact]
    public async Task Handle_DescriptionGrantsAndFirstParty_PersistsThem()
    {
        var client = ClientBuilder.Confidential(TenantId, GrantType.AuthorizationCode);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(new InMemoryClientRepository(db)).Handle(
            Patch(client.Id, description: "  Billing  ",
                grantType: GrantType.AuthorizationCode | GrantType.RefreshToken, isFirstParty: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = await StoredAsync(db, client.Id);
        Assert.Equal("Billing", stored.Description);
        Assert.Equal(GrantType.AuthorizationCode | GrantType.RefreshToken, stored.GrantType);
        Assert.True(stored.IsFirstParty);
    }

    [Fact]
    public async Task Handle_EmptyDescription_ClearsIt()
    {
        var client = ClientBuilder.Confidential(TenantId);
        client.UpdateDescription("Billing");
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(new InMemoryClientRepository(db))
            .Handle(Patch(client.Id, description: ""), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null((await StoredAsync(db, client.Id)).Description);
    }

    [Fact]
    public async Task Handle_NullFields_LeaveSettingsUnchanged()
    {
        var client = ClientBuilder.Confidential(TenantId, GrantType.ClientCredentials);
        client.UpdateDescription("Billing");
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(new InMemoryClientRepository(db))
            .Handle(Patch(client.Id, displayName: "Renamed"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = await StoredAsync(db, client.Id);
        Assert.Equal("Renamed", stored.DisplayName);
        Assert.Equal("Billing", stored.Description);
        Assert.Equal(GrantType.ClientCredentials, stored.GrantType);
        Assert.False(stored.IsFirstParty);
    }

    [Fact]
    public async Task Handle_ClientCredentialsOnPublicClient_FailsValidation_AndPersistsNothing()
    {
        var client = ClientBuilder.Public(TenantId);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(new InMemoryClientRepository(db)).Handle(
            Patch(client.Id, description: "changed", grantType: GrantType.AuthorizationCode | GrantType.ClientCredentials),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        AssertErrorCode(result, ClientErrorCodes.Validation);
        var stored = await StoredAsync(db, client.Id);
        Assert.Equal(GrantType.AuthorizationCode, stored.GrantType);
        Assert.Null(stored.Description);
    }

    [Fact]
    public async Task Handle_DescriptionTooLong_FailsValidation()
    {
        var client = ClientBuilder.Confidential(TenantId);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(new InMemoryClientRepository(db))
            .Handle(Patch(client.Id, description: new string('a', 1001)), CancellationToken.None);

        AssertErrorCode(result, ClientErrorCodes.Validation);
    }

    [Fact]
    public async Task Handle_ClientOfOtherTenant_ReturnsNotFound()
    {
        var client = ClientBuilder.Confidential(TenantId);
        await using var db = await ClientBuilder.SeedAsync(client);

        var result = await new Handler(new InMemoryClientRepository(db))
            .Handle(Patch(client.Id, isFirstParty: true, tenantId: Guid.NewGuid()), CancellationToken.None);

        AssertErrorCode(result, ClientErrorCodes.NotFound);
    }
}
