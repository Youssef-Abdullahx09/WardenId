namespace WardenId.ClientManagement.Domain.Tests;

public class ClientUpdateDisplayNameTests
{
    private static Client CreateClient() =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Original name", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void UpdateDisplayName_FailsOnEmpty()
    {
        var client = CreateClient();

        var result = client.UpdateDisplayName("");

        Assert.True(result.IsFailed);
        Assert.Equal("Original name", client.DisplayName);
    }

    [Fact]
    public void UpdateDisplayName_FailsWhenTooLong()
    {
        var client = CreateClient();

        var result = client.UpdateDisplayName(new string('a', 201));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void UpdateDisplayName_SucceedsAndTrimsWhitespace()
    {
        var client = CreateClient();

        var result = client.UpdateDisplayName("  New name  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("New name", client.DisplayName);
    }
}
