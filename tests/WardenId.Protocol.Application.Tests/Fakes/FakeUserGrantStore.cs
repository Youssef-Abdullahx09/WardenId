using WardenId.Protocol.Application.Features.Grants;

namespace WardenId.Protocol.Application.Tests.Fakes;

/// <summary>OpenIddict's authorization table in memory: answers by subject and by id like the real store.</summary>
public sealed class FakeUserGrantStore : IUserGrantStore
{
    public List<StoredGrant> Grants { get; } = [];

    public List<string> ListCalls { get; } = [];

    public Task<IReadOnlyList<StoredGrant>> ListBySubjectAsync(string subject, CancellationToken cancellationToken)
    {
        ListCalls.Add(subject);
        IReadOnlyList<StoredGrant> grants = Grants.Where(g => g.Subject == subject).ToList();
        return Task.FromResult(grants);
    }

    public Task<StoredGrant?> FindAsync(string authorizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Grants.SingleOrDefault(g => g.AuthorizationId == authorizationId));
}
