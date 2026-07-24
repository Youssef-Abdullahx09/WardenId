namespace WardenId.ClientManagement.Domain.Tests;

public class ClientEnabledTests
{
    private static Client CreateClient() =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void Disable_SetsEnabledFalseAndRaisesEvent()
    {
        var client = CreateClient();

        client.Disable();

        Assert.False(client.Enabled);
        Assert.Contains(client.DomainEvents, e => e is ClientDisabledDomainEvent);
    }

    [Fact]
    public void Enable_SetsEnabledTrueWithoutRaisingAnEvent()
    {
        var client = CreateClient();
        client.Disable();
        var eventCountAfterDisable = client.DomainEvents.Count;

        client.Enable();

        Assert.True(client.Enabled);
        Assert.Equal(eventCountAfterDisable, client.DomainEvents.Count);
    }
}
