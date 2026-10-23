using FluentResults;
using MediatR;
using WardenId.Tenancy.Application.Utilities;
using WardenId.Tenancy.Domain;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys;
using WardenId.Tenancy.Query.Contracts;
using SigningKeysGetPublicKeysQuery = WardenId.Tenancy.Query.Contracts.Features.SigningKeys.GetPublicKeys.Query;

namespace WardenId.Tenancy.Application.Features.SigningKeys.GetPublicKeys;

// Inside this namespace, a bare \"Query\" would otherwise bind to the WardenId.Tenancy.Query namespace.
using Query = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys.Query;

/// <summary>
/// Public keys for discovery/JWKS. Only Active and Suspended tenants are servable — suspension
/// is enforced where tokens are issued, not where they're verified. Every ineligible case returns
/// the same NotFound so the response never reveals that a tenant exists in another state.
/// </summary>
public sealed class Handler(ISigningKeysQuery signingKeysQuery) : IRequestHandler<Query, Result<TenantPublicKeys>>
{
    public async Task<Result<TenantPublicKeys>> Handle(Query request, CancellationToken cancellationToken)
    {
        // Route constraint has already enforced slug format; the query compares it exactly.
        var result = await signingKeysQuery.GetPublicKeys(new SigningKeysGetPublicKeysQuery(request.Slug), cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        if (result.Value is not { } tenant
            || QueryRows.TryParseStatus(tenant.Status) is not (Status.Active or Status.Suspended))
            return Result.Fail(TenantErrors.NotFound);

        // Retiring keys stay published so tokens signed before a rotation still verify;
        // Retired keys are withdrawn.
        var jwks = tenant.PublicKeys
            .Where(k => QueryRows.TryParseSigningKeyStatus(k.Status) is SigningKeyStatus.Active or SigningKeyStatus.Retiring)
            .Select(k => k.PublicKeyJwk)
            .ToList();

        return Result.Ok(new TenantPublicKeys(tenant.Id, tenant.Slug, jwks));
    }
}
