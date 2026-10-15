using WardenId.Protocol.Application.Features.BackchannelLogout;

namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeSessionAuthorizationRevoker : ISessionAuthorizationRevoker
{
    public List<IReadOnlyCollection<string>> Calls { get; } = [];

    /// <summary>When set, RevokeAsync fails with this exception (e.g. the OpenIddict database is down).</summary>
    public Exception? Throw { get; set; }

    public Task RevokeAsync(IReadOnlyCollection<string> authorizationIds, CancellationToken cancellationToken)
    {
        if (Throw is not null) return Task.FromException(Throw);

        Calls.Add(authorizationIds.ToList());
        return Task.CompletedTask;
    }
}
