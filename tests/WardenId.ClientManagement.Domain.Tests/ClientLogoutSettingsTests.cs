namespace WardenId.ClientManagement.Domain.Tests;

public class ClientLogoutSettingsTests
{
    private static Client CreateClient() =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    private static BackchannelLogoutUri Bcl(string value) => BackchannelLogoutUri.Create(value).Value;

    private static RedirectUri Uri(string value) => RedirectUri.Create(value).Value;

    [Fact]
    public void NewClient_HasNoBackchannelLogoutUri()
    {
        Assert.Null(CreateClient().BackchannelLogoutUri);
    }

    [Fact]
    public void SetBackchannelLogoutUri_StoresValue_AndRaisesEventWithUri()
    {
        var client = CreateClient();

        client.SetBackchannelLogoutUri(Bcl("https://app.example.com/bcl"));

        Assert.Equal("https://app.example.com/bcl", client.BackchannelLogoutUri!.Value);
        var changed = Assert.Single(client.DomainEvents.OfType<ClientBackchannelLogoutUriChangedDomainEvent>());
        Assert.Equal("https://app.example.com/bcl", changed.Uri);
    }

    [Fact]
    public void SetBackchannelLogoutUri_RaisesEventOnlyOnChange()
    {
        var client = CreateClient();

        client.SetBackchannelLogoutUri(Bcl("https://app.example.com/bcl"));
        client.SetBackchannelLogoutUri(Bcl("https://app.example.com/bcl")); // equal value, new instance
        client.SetBackchannelLogoutUri(null);
        client.SetBackchannelLogoutUri(null);

        var events = client.DomainEvents.OfType<ClientBackchannelLogoutUriChangedDomainEvent>().ToList();
        Assert.Equal(2, events.Count);
        Assert.Equal("https://app.example.com/bcl", events[0].Uri);
        Assert.Null(events[1].Uri);
        Assert.Null(client.BackchannelLogoutUri);
    }

    [Fact]
    public void SetBackchannelLogoutUri_NullOnClientWithoutUri_RaisesNoEvent()
    {
        var client = CreateClient();

        client.SetBackchannelLogoutUri(null);

        Assert.Empty(client.DomainEvents.OfType<ClientBackchannelLogoutUriChangedDomainEvent>());
    }

    [Fact]
    public void ReplacePostLogoutRedirectUris_ReplacesWholeSet_AndKeepsOverlap()
    {
        var client = CreateClient();
        client.AddPostLogoutRedirectUri(Uri("https://app.example.com/a"));
        client.AddPostLogoutRedirectUri(Uri("https://app.example.com/b"));

        client.ReplacePostLogoutRedirectUris([Uri("https://app.example.com/b"), Uri("https://app.example.com/c")]);

        Assert.Equal(
            new[] { "https://app.example.com/b", "https://app.example.com/c" },
            client.PostLogoutRedirectUris.Select(u => u.Value).OrderBy(v => v, StringComparer.Ordinal));
    }

    [Fact]
    public void ReplacePostLogoutRedirectUris_DedupesInput()
    {
        var client = CreateClient();

        client.ReplacePostLogoutRedirectUris([Uri("https://app.example.com/a"), Uri("https://app.example.com/a")]);

        Assert.Single(client.PostLogoutRedirectUris);
    }

    [Fact]
    public void ReplacePostLogoutRedirectUris_EmptyClearsAll_AndLeavesRedirectUrisAlone()
    {
        var client = CreateClient();
        client.AddRedirectUri(Uri("https://app.example.com/cb"));
        client.AddPostLogoutRedirectUri(Uri("https://app.example.com/a"));

        client.ReplacePostLogoutRedirectUris([]);

        Assert.Empty(client.PostLogoutRedirectUris);
        Assert.Single(client.RedirectUris);
    }
}
