namespace WardenId.ClientManagement.Domain.Tests;

public class ClientScopeTests
{
    private static Client CreateClient() =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void AssignScope_AddsTheScopeIdAndRaisesEvent()
    {
        var client = CreateClient();
        var scopeId = Guid.NewGuid();

        client.AssignScope(scopeId);

        Assert.Contains(scopeId, client.AssignedScopeIds);
        Assert.Contains(client.DomainEvents, e => e is ClientScopesChangedDomainEvent changed && changed.Assigned);
    }

    [Fact]
    public void AssignScope_IsIdempotent_NoDuplicateOrSecondEvent()
    {
        var client = CreateClient();
        var scopeId = Guid.NewGuid();

        client.AssignScope(scopeId);
        client.AssignScope(scopeId);

        Assert.Single(client.AssignedScopeIds);
        Assert.Single(client.DomainEvents.OfType<ClientScopesChangedDomainEvent>());
    }

    [Fact]
    public void RemoveScope_RemovesTheScopeIdAndRaisesEvent()
    {
        var client = CreateClient();
        var scopeId = Guid.NewGuid();
        client.AssignScope(scopeId);

        client.RemoveScope(scopeId);

        Assert.DoesNotContain(scopeId, client.AssignedScopeIds);
        Assert.Contains(client.DomainEvents, e => e is ClientScopesChangedDomainEvent changed && !changed.Assigned);
    }

    [Fact]
    public void RemoveScope_NoOpAndNoEventWhenNotAssigned()
    {
        var client = CreateClient();

        client.RemoveScope(Guid.NewGuid());

        Assert.DoesNotContain(client.DomainEvents, e => e is ClientScopesChangedDomainEvent);
    }
}
