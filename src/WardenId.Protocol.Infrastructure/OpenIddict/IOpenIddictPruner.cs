namespace WardenId.Protocol.Infrastructure.OpenIddict;

/// <summary>Deletes expired/redeemed/revoked OpenIddict tokens and authorizations older than a threshold.</summary>
public interface IOpenIddictPruner
{
    Task PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken);
}
