using Bcl = WardenId.ClientManagement.Application.Features.Clients.BackchannelLogoutUri.Set;
using CorsReplace = WardenId.ClientManagement.Application.Features.Clients.CorsOrigins.Replace;
using Create = WardenId.ClientManagement.Application.Features.Clients.Create;
using Delete = WardenId.ClientManagement.Application.Features.Clients.Delete;
using PostLogoutAdd = WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Add;
using PostLogoutRemove = WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Remove;
using PostLogoutReplace = WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Replace;
using RedirectAdd = WardenId.ClientManagement.Application.Features.Clients.RedirectUris.Add;
using RedirectRemove = WardenId.ClientManagement.Application.Features.Clients.RedirectUris.Remove;
using RedirectReplace = WardenId.ClientManagement.Application.Features.Clients.RedirectUris.Replace;
using SecretExpire = WardenId.ClientManagement.Application.Features.Clients.Secrets.Expire;
using SecretIssue = WardenId.ClientManagement.Application.Features.Clients.Secrets.Issue;
using UpdateClient = WardenId.ClientManagement.Application.Features.Clients.Update;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients;

/// <summary>Every client mutation records the calling user, never the tenant id, as the audit actor.</summary>
public class AuditActorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private const string Uri = "https://app.example/cb";

    private static async Task AssertActorAsync(Client client, Func<InMemoryClientRepository, Guid, Task<IResultBase>> act)
    {
        await using var db = await ClientBuilder.SeedAsync(client);
        var repository = new InMemoryClientRepository(db);

        var result = await act(repository, client.Id);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal(ActorId, repository.LastActorId);
    }

    private static Client WithRedirectUris(Client client)
    {
        client.AddRedirectUri(RedirectUri.Create(Uri).Value);
        client.AddPostLogoutRedirectUri(RedirectUri.Create(Uri).Value);
        return client;
    }

    [Fact]
    public async Task Create_RecordsCaller()
    {
        await using var db = await ClientBuilder.SeedAsync();
        var repository = new InMemoryClientRepository(db);

        var result = await new Create.Handler(repository).Handle(
            new Create.Command(TenantId, ClientType.Confidential, "svc", GrantType.ClientCredentials,
                ClientAuthenticationMethod.SharedSecret, false, false, null, ActorId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActorId, repository.LastActorId);
    }

    [Fact]
    public Task Delete_RecordsCaller() =>
        AssertActorAsync(ClientBuilder.Confidential(TenantId), async (repo, id) =>
            await new Delete.Handler(repo).Handle(new Delete.Command(TenantId, id, ActorId), CancellationToken.None));

    [Fact]
    public Task Update_RecordsCaller() =>
        AssertActorAsync(ClientBuilder.Confidential(TenantId), async (repo, id) =>
            await new UpdateClient.Handler(repo).Handle(
                new UpdateClient.Command(TenantId, id, "renamed", null, null, null, null, null, null, null, null, false, ActorId),
                CancellationToken.None));

    [Fact]
    public Task AddRedirectUri_RecordsCaller() =>
        AssertActorAsync(ClientBuilder.Public(TenantId), async (repo, id) =>
            await new RedirectAdd.Handler(repo).Handle(new RedirectAdd.Command(TenantId, id, Uri, ActorId), CancellationToken.None));

    [Fact]
    public Task RemoveRedirectUri_RecordsCaller() =>
        AssertActorAsync(WithRedirectUris(ClientBuilder.Public(TenantId)), async (repo, id) =>
            await new RedirectRemove.Handler(repo).Handle(new RedirectRemove.Command(TenantId, id, Uri, ActorId), CancellationToken.None));

    [Fact]
    public Task ReplaceRedirectUris_RecordsCaller() =>
        AssertActorAsync(ClientBuilder.Public(TenantId), async (repo, id) =>
            await new RedirectReplace.Handler(repo).Handle(new RedirectReplace.Command(TenantId, id, [Uri], ActorId), CancellationToken.None));

    [Fact]
    public Task AddPostLogoutRedirectUri_RecordsCaller() =>
        AssertActorAsync(ClientBuilder.Public(TenantId), async (repo, id) =>
            await new PostLogoutAdd.Handler(repo).Handle(new PostLogoutAdd.Command(TenantId, id, Uri, ActorId), CancellationToken.None));

    [Fact]
    public Task RemovePostLogoutRedirectUri_RecordsCaller() =>
        AssertActorAsync(WithRedirectUris(ClientBuilder.Public(TenantId)), async (repo, id) =>
            await new PostLogoutRemove.Handler(repo).Handle(new PostLogoutRemove.Command(TenantId, id, Uri, ActorId), CancellationToken.None));

    [Fact]
    public Task ReplacePostLogoutRedirectUris_RecordsCaller() =>
        AssertActorAsync(ClientBuilder.Public(TenantId), async (repo, id) =>
            await new PostLogoutReplace.Handler(repo).Handle(new PostLogoutReplace.Command(TenantId, id, [Uri], ActorId), CancellationToken.None));

    [Fact]
    public Task ReplaceCorsOrigins_RecordsCaller() =>
        AssertActorAsync(ClientBuilder.Public(TenantId), async (repo, id) =>
            await new CorsReplace.Handler(repo).Handle(
                new CorsReplace.Command(TenantId, id, ["https://app.example"], ActorId), CancellationToken.None));

    [Fact]
    public Task SetBackchannelLogoutUri_RecordsCaller() =>
        AssertActorAsync(ClientBuilder.Confidential(TenantId, GrantType.AuthorizationCode), async (repo, id) =>
            await new Bcl.Handler(repo).Handle(new Bcl.Command(TenantId, id, "https://app.example/bcl", ActorId), CancellationToken.None));

    [Fact]
    public Task IssueSecret_RecordsCaller() =>
        AssertActorAsync(ClientBuilder.Confidential(TenantId), async (repo, id) =>
            await new SecretIssue.Handler(repo).Handle(new SecretIssue.Command(TenantId, id, "next", null, ActorId), CancellationToken.None));

    [Fact]
    public async Task ExpireSecret_RecordsCaller()
    {
        var client = ClientBuilder.Confidential(TenantId);
        var secretId = client.Secrets.Single().Id;

        await AssertActorAsync(client, async (repo, id) =>
            await new SecretExpire.Handler(repo).Handle(new SecretExpire.Command(TenantId, id, secretId, ActorId), CancellationToken.None));
    }
}
