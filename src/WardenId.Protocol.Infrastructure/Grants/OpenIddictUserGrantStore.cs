using OpenIddict.Abstractions;
using WardenId.Protocol.Application.Features.Grants;
using WardenId.Protocol.Infrastructure.OpenIddict;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Infrastructure.Grants;

/// <summary>Reads OpenIddict authorizations through its manager; tenant and client come from the owner stamp.</summary>
public sealed class OpenIddictUserGrantStore(IOpenIddictAuthorizationManager authorizations) : IUserGrantStore
{
    public async Task<IReadOnlyList<StoredGrant>> ListBySubjectAsync(string subject, CancellationToken cancellationToken)
    {
        var grants = new List<StoredGrant>();
        await foreach (var authorization in authorizations.FindBySubjectAsync(subject, cancellationToken))
            grants.Add(await MapAsync(authorization, cancellationToken));

        return grants;
    }

    public async Task<StoredGrant?> FindAsync(string authorizationId, CancellationToken cancellationToken) =>
        await authorizations.FindByIdAsync(authorizationId, cancellationToken) is { } authorization
            ? await MapAsync(authorization, cancellationToken)
            : null;

    private async Task<StoredGrant> MapAsync(object authorization, CancellationToken cancellationToken) =>
        new(
            (await authorizations.GetIdAsync(authorization, cancellationToken))!,
            await authorizations.GetSubjectAsync(authorization, cancellationToken),
            AuthorizationOwnerProperties.Read(await authorizations.GetPropertiesAsync(authorization, cancellationToken)),
            await authorizations.GetCreationDateAsync(authorization, cancellationToken),
            await authorizations.GetScopesAsync(authorization, cancellationToken),
            ToStatus(await authorizations.GetStatusAsync(authorization, cancellationToken)));

    // A status OpenIddict adds later can't mint tokens as far as we know: report it as inactive, never as valid.
    private static GrantStatus ToStatus(string? status) => status switch
    {
        Statuses.Valid => GrantStatus.Valid,
        Statuses.Revoked => GrantStatus.Revoked,
        Statuses.Rejected => GrantStatus.Rejected,
        _ => GrantStatus.Inactive
    };
}
