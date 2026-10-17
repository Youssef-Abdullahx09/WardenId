using FluentResults;
using MediatR;
using WardenId.Protocol.Domain;
using GetReferenceQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.GetReference.Query;

namespace WardenId.Protocol.Application.Features.Grants.RevokeForClient;

/// <summary>
/// Revokes every grant and stored token of the client in the tenant (all users, offline grants included). Login
/// sessions are left alone. Not found for an unknown, deleted or other tenant's client; otherwise idempotent.
/// </summary>
public sealed class Handler(ISender sender, IClientGrantRevoker revoker) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var client = await sender.Send(new GetReferenceQuery(request.TenantId, request.ClientId), cancellationToken);
        if (client.IsFailed) return Result.Fail(ProtocolErrors.ClientNotFound());

        await revoker.RevokeAsync(request.TenantId, client.Value.ClientId, cancellationToken);
        return Result.Ok();
    }
}
