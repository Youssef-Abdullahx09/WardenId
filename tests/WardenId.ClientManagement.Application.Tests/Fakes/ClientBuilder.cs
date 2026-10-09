namespace WardenId.ClientManagement.Application.Tests.Fakes;

public static class ClientBuilder
{
    public const string Secret = "s3cret-primary";

    public static Client Confidential(
        Guid tenantId,
        GrantType grants = GrantType.ClientCredentials,
        ClientAuthenticationMethod auth = ClientAuthenticationMethod.SharedSecret,
        params ClientSecret[] secrets)
    {
        var client = Client.Create(tenantId, ClientType.Confidential, "svc", grants, auth, false, false).Value;
        var toAdd = secrets.Length > 0 ? secrets : [ClientSecret.Create(ClientSecretHasher.Hash(Secret), "primary", null)];
        foreach (var secret in toAdd) Assert.True(client.AddSecret(secret).IsSuccess);
        return client;
    }

    public static Client Public(Guid tenantId) =>
        Client.Create(tenantId, ClientType.Public, "spa", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, false, false).Value;

    public static async Task<TestClientManagementDbContext> SeedAsync(params Client[] clients)
    {
        var db = TestClientManagementDbContext.CreateNew(Guid.NewGuid().ToString());
        db.Clients.AddRange(clients);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }
}
