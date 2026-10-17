using WardenId.Protocol.Application.Features.Grants;

namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeClientGrantRevoker : IClientGrantRevoker
{
    public List<(Guid TenantId, string ClientId)> Calls { get; } = [];

    public Task RevokeAsync(Guid tenantId, string clientId, CancellationToken cancellationToken)
    {
        Calls.Add((tenantId, clientId));
        return Task.CompletedTask;
    }
}
