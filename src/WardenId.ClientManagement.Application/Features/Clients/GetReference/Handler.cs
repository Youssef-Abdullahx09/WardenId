using FluentResults;
using MediatR;
using WardenId.ClientManagement.Domain;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetReference;
using WardenId.ClientManagement.Query.Contracts;
using ClientsQuery = WardenId.ClientManagement.Query.Contracts.Features.Clients.GetReference.Query;

namespace WardenId.ClientManagement.Application.Features.Clients.GetReference;

// The sibling namespace WardenId.ClientManagement.Query would otherwise shadow the imported type.
using Query = WardenId.ClientManagement.Integration.Command.Features.Clients.GetReference.Query;

/// <summary>Read side: the client comes from the Dapper query layer (ClientManagement.Query), not the aggregate.</summary>
public sealed class Handler(IClientsQuery clientsQuery) : IRequestHandler<Query, Result<ClientReference>>
{
    public async Task<Result<ClientReference>> Handle(Query request, CancellationToken cancellationToken)
    {
        var result = await clientsQuery.GetReference(
            new ClientsQuery(request.TenantId, request.Id),
            cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        var client = result.Value;
        return client is null
            ? Result.Fail(ClientErrors.NotFound)
            : Result.Ok(new ClientReference(client.Id, client.ClientId));
    }
}
