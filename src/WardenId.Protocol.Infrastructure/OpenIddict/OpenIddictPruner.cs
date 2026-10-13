using OpenIddict.Abstractions;

namespace WardenId.Protocol.Infrastructure.OpenIddict;

public sealed class OpenIddictPruner(IOpenIddictTokenManager tokens, IOpenIddictAuthorizationManager authorizations)
    : IOpenIddictPruner
{
    public async Task PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        // Tokens first: authorization pruning removes only authorizations without valid tokens.
        await tokens.PruneAsync(threshold, cancellationToken);
        await authorizations.PruneAsync(threshold, cancellationToken);
    }
}
