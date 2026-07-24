namespace WardenId.ClientManagement.Domain.Tests;

public class ClientCreateTests
{
    [Fact]
    public void Create_FailsOnEmptyDisplayName()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenDisplayNameTooLong()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, new string('a', 201), GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_ForcesPkceForPublicClients()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Public, "SPA client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.RequirePkce);
    }

    [Fact]
    public void Create_DoesNotForcePkceForConfidentialClients()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Backend client", GrantType.ClientCredentials,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.RequirePkce);
    }

    [Fact]
    public void Create_AssignsAGeneratedClientId()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Backend client", GrantType.ClientCredentials,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.Equal(32, result.Value.ClientId.Value.Length);
    }

    [Fact]
    public void Create_RaisesClientCreatedDomainEvent()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Backend client", GrantType.ClientCredentials,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        var domainEvent = Assert.Single(result.Value.DomainEvents);
        Assert.IsType<ClientCreatedDomainEvent>(domainEvent);
    }

    [Fact]
    public void Create_DefaultsEnabledToTrue()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Backend client", GrantType.ClientCredentials,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.True(result.Value.Enabled);
    }
}
