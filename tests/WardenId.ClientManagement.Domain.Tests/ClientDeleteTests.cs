namespace WardenId.ClientManagement.Domain.Tests;

public class ClientDeleteTests
{
    private static Client CreateClient(ClientAuthenticationMethod method) =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.ClientCredentials,
            method, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void Delete_MarksTheClientDeleted_AndDropsItsSecrets()
    {
        var client = CreateClient(ClientAuthenticationMethod.SharedSecret);
        Assert.True(client.AddSecret(ClientSecret.Create("hashed", "primary", expiresAt: null)).IsSuccess);

        client.Delete();

        Assert.True(client.IsDeleted);
        Assert.NotNull(client.DeletedAt);
        Assert.Empty(client.Secrets);
    }

    [Fact]
    public void Delete_DropsTheClientsSigningKeys()
    {
        var client = CreateClient(ClientAuthenticationMethod.PrivateKeyJwt);
        Assert.True(client.AddSigningKey(ClientSigningKey.Create("kid-1", "{}", "ES256", expiresAt: null)).IsSuccess);

        client.Delete();

        Assert.Empty(client.SigningKeys);
    }
}
