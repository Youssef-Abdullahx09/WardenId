namespace WardenId.ClientManagement.Domain.Tests;

public class ClientUriCollectionsTests
{
    private static Client CreateClient() =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void AddRedirectUri_DedupesIdenticalValues()
    {
        var client = CreateClient();
        var uri = RedirectUri.Create("https://app.example.com/callback").Value;

        client.AddRedirectUri(uri);
        client.AddRedirectUri(uri);

        Assert.Single(client.RedirectUris);
    }

    [Fact]
    public void RemoveRedirectUri_RemovesTheMatchingEntry()
    {
        var client = CreateClient();
        var uri = RedirectUri.Create("https://app.example.com/callback").Value;
        client.AddRedirectUri(uri);

        client.RemoveRedirectUri(uri);

        Assert.Empty(client.RedirectUris);
    }

    [Fact]
    public void ValidateRedirectUri_TrueOnlyForRegisteredUris()
    {
        var client = CreateClient();
        var registered = RedirectUri.Create("https://app.example.com/callback").Value;
        var other = RedirectUri.Create("https://evil.example.com/callback").Value;
        client.AddRedirectUri(registered);

        Assert.True(client.ValidateRedirectUri(registered));
        Assert.False(client.ValidateRedirectUri(other));
    }

    [Fact]
    public void PostLogoutRedirectUris_AddAndRemoveIndependentlyOfRedirectUris()
    {
        var client = CreateClient();
        var uri = RedirectUri.Create("https://app.example.com/logged-out").Value;

        client.AddPostLogoutRedirectUri(uri);

        Assert.Single(client.PostLogoutRedirectUris);
        Assert.Empty(client.RedirectUris);

        client.RemovePostLogoutRedirectUri(uri);

        Assert.Empty(client.PostLogoutRedirectUris);
    }

    [Fact]
    public void CorsOrigins_AddDedupesAndRemoveWorks()
    {
        var client = CreateClient();
        var origin = CorsOrigin.Create("https://app.example.com").Value;

        client.AddCorsOrigin(origin);
        client.AddCorsOrigin(origin);

        Assert.Single(client.CorsOrigins);

        client.RemoveCorsOrigin(origin);

        Assert.Empty(client.CorsOrigins);
    }
}
