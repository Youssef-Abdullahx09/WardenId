namespace WardenId.ClientManagement.Domain.Tests;

public class ClientSecretBehaviorTests
{
    private static Client CreateClient(ClientType clientType = ClientType.Confidential) =>
        Client.Create(
            Guid.NewGuid(), clientType, "Test client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void AddSecret_FailsForPublicClients()
    {
        var client = CreateClient(ClientType.Public);
        var secret = ClientSecret.Create("hashed", "primary", expiresAt: null);

        var result = client.AddSecret(secret);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void AddSecret_SucceedsForConfidentialClients()
    {
        var client = CreateClient();
        var secret = ClientSecret.Create("hashed", "primary", expiresAt: null);

        var result = client.AddSecret(secret);

        Assert.True(result.IsSuccess);
        Assert.Single(client.Secrets);
    }

    [Fact]
    public void RotateSecret_AddsNewSecretAndExpiresNonExpiringOnes()
    {
        var client = CreateClient();
        var original = ClientSecret.Create("hashed-old", "primary", expiresAt: null);
        client.AddSecret(original);
        var overlapExpiry = DateTimeOffset.UtcNow.AddDays(7);
        var replacement = ClientSecret.Create("hashed-new", "primary", expiresAt: null);

        var result = client.RotateSecret(replacement, overlapExpiry);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, client.Secrets.Count);
        Assert.Equal(overlapExpiry, original.ExpiresAt);
        Assert.Null(replacement.ExpiresAt);
    }

    [Fact]
    public void RotateSecret_RaisesClientSecretRotatedDomainEvent()
    {
        var client = CreateClient();
        var replacement = ClientSecret.Create("hashed-new", "primary", expiresAt: null);

        client.RotateSecret(replacement, DateTimeOffset.UtcNow.AddDays(7));

        Assert.Contains(client.DomainEvents, e => e is ClientSecretRotatedDomainEvent);
    }

    [Fact]
    public void ExpireSecret_SetsExpiryToNow()
    {
        var client = CreateClient();
        var secret = ClientSecret.Create("hashed", "primary", expiresAt: null);
        client.AddSecret(secret);

        var result = client.ExpireSecret(secret.Id);

        Assert.True(result.IsSuccess);
        Assert.True(secret.IsExpired(DateTimeOffset.UtcNow.AddSeconds(1)));
    }

    [Fact]
    public void ExpireSecret_FailsWhenSecretNotFound()
    {
        var client = CreateClient();

        var result = client.ExpireSecret(Guid.NewGuid());

        Assert.True(result.IsFailed);
    }
}
