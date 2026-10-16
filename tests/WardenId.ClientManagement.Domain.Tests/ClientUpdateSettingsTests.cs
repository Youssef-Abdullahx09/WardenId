namespace WardenId.ClientManagement.Domain.Tests;

public class ClientUpdateSettingsTests
{
    private static Client CreateClient(
        ClientType clientType = ClientType.Confidential,
        GrantType grants = GrantType.AuthorizationCode) =>
        Client.Create(
            Guid.NewGuid(), clientType, "Test client", grants,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true,
            description: "Original").Value;

    [Fact]
    public void UpdateDescription_SetsTrimmedValue()
    {
        var client = CreateClient();

        var result = client.UpdateDescription("  Billing back office  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Billing back office", client.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDescription_BlankClearsIt(string blank)
    {
        var client = CreateClient();

        var result = client.UpdateDescription(blank);

        Assert.True(result.IsSuccess);
        Assert.Null(client.Description);
    }

    [Fact]
    public void UpdateDescription_FailsWhenTooLong_AndKeepsOldValue()
    {
        var client = CreateClient();

        var result = client.UpdateDescription(new string('a', 1001));

        Assert.True(result.IsFailed);
        Assert.Equal("Original", client.Description);
    }

    [Fact]
    public void UpdateIsFirstParty_SetsTheFlag()
    {
        var client = CreateClient();

        client.UpdateIsFirstParty(true);

        Assert.True(client.IsFirstParty);
    }

    [Fact]
    public void UpdateGrantTypes_StoresValue_AndRaisesEventWithPreviousAndCurrent()
    {
        var client = CreateClient();

        var result = client.UpdateGrantTypes(GrantType.AuthorizationCode | GrantType.RefreshToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(GrantType.AuthorizationCode | GrantType.RefreshToken, client.GrantType);
        var changed = Assert.Single(client.DomainEvents.OfType<ClientGrantTypesChangedDomainEvent>());
        Assert.Equal(GrantType.AuthorizationCode, changed.Previous);
        Assert.Equal(GrantType.AuthorizationCode | GrantType.RefreshToken, changed.Current);
    }

    [Fact]
    public void UpdateGrantTypes_SameValue_RaisesNoEvent()
    {
        var client = CreateClient();

        var result = client.UpdateGrantTypes(GrantType.AuthorizationCode);

        Assert.True(result.IsSuccess);
        Assert.Empty(client.DomainEvents.OfType<ClientGrantTypesChangedDomainEvent>());
    }

    [Fact]
    public void UpdateGrantTypes_None_Fails()
    {
        var client = CreateClient();

        var result = client.UpdateGrantTypes(default);

        Assert.True(result.IsFailed);
        Assert.Equal(GrantType.AuthorizationCode, client.GrantType);
    }

    [Fact]
    public void UpdateGrantTypes_UndefinedFlag_Fails()
    {
        var client = CreateClient();

        var result = client.UpdateGrantTypes(GrantType.AuthorizationCode | (GrantType)16);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void UpdateGrantTypes_ClientCredentialsOnPublicClient_Fails()
    {
        var client = CreateClient(ClientType.Public);

        var result = client.UpdateGrantTypes(GrantType.AuthorizationCode | GrantType.ClientCredentials);

        Assert.True(result.IsFailed);
        Assert.Equal(GrantType.AuthorizationCode, client.GrantType);
        Assert.Empty(client.DomainEvents.OfType<ClientGrantTypesChangedDomainEvent>());
    }

    [Fact]
    public void UpdateGrantTypes_RefreshTokenWithoutAUserFlow_Fails()
    {
        var client = CreateClient();

        var result = client.UpdateGrantTypes(GrantType.ClientCredentials | GrantType.RefreshToken);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void UpdateGrantTypes_RefreshTokenWithDeviceCode_Succeeds()
    {
        var client = CreateClient();

        var result = client.UpdateGrantTypes(GrantType.DeviceCode | GrantType.RefreshToken);

        Assert.True(result.IsSuccess);
    }
}
