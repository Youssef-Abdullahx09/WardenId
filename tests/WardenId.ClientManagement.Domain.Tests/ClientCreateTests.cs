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
    public void Create_UsesTheGivenClientIdWhenProvided()
    {
        var clientId = ClientId.Create("warden-console").Value;

        var result = Client.Create(
            Guid.NewGuid(), ClientType.Public, "Console", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: true, consentRequired: false,
            clientId: clientId);

        Assert.Equal(clientId, result.Value.ClientId);
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

    [Fact]
    public void Create_FailsWithoutAnyGrantType()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Backend client", default,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsForPublicClientWithClientCredentials()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Public, "SPA client", GrantType.AuthorizationCode | GrantType.ClientCredentials,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsForRefreshTokenWithoutAUserFlow()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Backend client", GrantType.ClientCredentials | GrantType.RefreshToken,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_TrimsDescription_AndStoresBlankAsNull()
    {
        var trimmed = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Backend client", GrantType.ClientCredentials,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true,
            description: "  Billing  ").Value;
        var blank = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Backend client", GrantType.ClientCredentials,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true,
            description: "   ").Value;

        Assert.Equal("Billing", trimmed.Description);
        Assert.Null(blank.Description);
    }

    [Fact]
    public void Create_FailsWhenDescriptionTooLong()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Backend client", GrantType.ClientCredentials,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true,
            description: new string('a', 1001));

        Assert.True(result.IsFailed);
    }
}
