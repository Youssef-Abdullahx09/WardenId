namespace WardenId.ClientManagement.Domain.Tests;

public class ClientTokenLifetimeTests
{
    private static Client CreateClient() =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void UpdateTokenLifetimeOverrides_SetsTheOverrides()
    {
        var client = CreateClient();
        var overrides = TokenLifetimeOverrides.Create(3600, 3600, 86400).Value;

        client.UpdateTokenLifetimeOverrides(overrides);

        Assert.Equal(overrides, client.TokenLifetimeOverrides);
    }

    [Fact]
    public void UpdateTokenLifetimeOverrides_CanClearBackToNull()
    {
        var client = CreateClient();
        client.UpdateTokenLifetimeOverrides(TokenLifetimeOverrides.Create(3600, 3600, 86400).Value);

        client.UpdateTokenLifetimeOverrides(null);

        Assert.Null(client.TokenLifetimeOverrides);
    }
}
