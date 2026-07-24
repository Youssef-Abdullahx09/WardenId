namespace WardenId.ClientManagement.Domain.Tests;

public class ClientSigningKeyBehaviorTests
{
    private static Client CreateClient(ClientAuthenticationMethod method) =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.ClientCredentials,
            method, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void AddSigningKey_FailsWhenNotPrivateKeyJwt()
    {
        var client = CreateClient(ClientAuthenticationMethod.SharedSecret);
        var key = ClientSigningKey.Create("kid-1", "{}", "ES256", expiresAt: null);

        var result = client.AddSigningKey(key);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void AddSigningKey_SucceedsWhenPrivateKeyJwt()
    {
        var client = CreateClient(ClientAuthenticationMethod.PrivateKeyJwt);
        var key = ClientSigningKey.Create("kid-1", "{}", "ES256", expiresAt: null);

        var result = client.AddSigningKey(key);

        Assert.True(result.IsSuccess);
        Assert.Single(client.SigningKeys);
    }
}
