namespace WardenId.ClientManagement.Domain.Tests;

public class ClientUpdateConsentRequiredTests
{
    private static Client CreateClient() =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void UpdateConsentRequired_SetsTheFlag()
    {
        var client = CreateClient();

        client.UpdateConsentRequired(false);

        Assert.False(client.ConsentRequired);
    }
}
