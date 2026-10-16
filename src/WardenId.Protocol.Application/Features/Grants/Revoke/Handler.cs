using FluentResults;
using MediatR;
using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.Protocol.Domain;

namespace WardenId.Protocol.Application.Features.Grants.Revoke;

/// <summary>
/// Revokes one authorization and every token under it. An already-revoked grant is revoked again (204): the revoker
/// is idempotent, and it also catches tokens left valid if an earlier family revocation failed halfway.
/// </summary>
public sealed class Handler(IUserGrantStore store, ISessionAuthorizationRevoker revoker) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var grant = await store.FindAsync(request.AuthorizationId, cancellationToken);
        if (grant is null || !grant.BelongsTo(request.TenantId, request.UserId))
            return Result.Fail(ProtocolErrors.GrantNotFound());

        await revoker.RevokeAsync([grant.AuthorizationId], cancellationToken);
        return Result.Ok();
    }
}
