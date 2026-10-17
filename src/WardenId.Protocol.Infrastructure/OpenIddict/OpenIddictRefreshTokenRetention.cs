using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Abstractions;

namespace WardenId.Protocol.Infrastructure.OpenIddict;

/// <summary>OpenIddict's pruning keeps dead token rows for PruneMinimumAge; nothing is pruned when pruning is off.</summary>
public sealed class OpenIddictRefreshTokenRetention(IOptions<ProtocolOpenIddictPruningOptions> options) : IRefreshTokenRetention
{
    public TimeSpan? MinimumRetention => options.Value.PruningEnabled ? options.Value.PruneMinimumAge : null;
}
