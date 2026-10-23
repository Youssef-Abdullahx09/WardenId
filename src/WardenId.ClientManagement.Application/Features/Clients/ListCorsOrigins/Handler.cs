using FluentResults;
using MediatR;
using WardenId.ClientManagement.Integration.Command.Features.Clients.ListCorsOrigins;
using WardenId.ClientManagement.Query.Contracts;
using ClientsQuery = WardenId.ClientManagement.Query.Contracts.Features.Clients.ListCorsOrigins.Query;

namespace WardenId.ClientManagement.Application.Features.Clients.ListCorsOrigins;

// The sibling namespace WardenId.ClientManagement.Query would otherwise shadow the imported type.
using Query = WardenId.ClientManagement.Integration.Command.Features.Clients.ListCorsOrigins.Query;

/// <summary>Disabled clients are left out: they can't obtain tokens, so their origins have nothing to call.</summary>
public sealed class Handler(IClientsQuery clientsQuery) : IRequestHandler<Query, Result<IReadOnlyList<string>>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(Query request, CancellationToken cancellationToken)
    {
        var result = await clientsQuery.ListCorsOrigins(new ClientsQuery(request.TenantId), cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        // Ordinal order here, not the database collation's.
        IReadOnlyList<string> origins = result.Value
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return Result.Ok(origins);
    }
}
