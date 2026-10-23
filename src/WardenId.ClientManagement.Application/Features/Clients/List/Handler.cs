using FluentResults;
using MediatR;
using WardenId.ClientManagement.Domain;
using WardenId.ClientManagement.Query.Contracts;
using WardenId.SharedKernel.Query.Contracts;
using ClientsQuery = WardenId.ClientManagement.Query.Contracts.Features.Clients.List.Query;

namespace WardenId.ClientManagement.Application.Features.Clients.List;

/// <summary>Read side: a tenant's clients come from the Dapper query layer (ClientManagement.Query), not the aggregate.</summary>
public sealed class Handler(
    IClientsQuery clientsQuery) : IRequestHandler<Query, Result<PaginationListResponse<ClientListItem>>>
{
    public async Task<Result<PaginationListResponse<ClientListItem>>> Handle(
        Query request,
        CancellationToken cancellationToken)
    {
        var result = await clientsQuery.List(
            new ClientsQuery(
                request.TenantId,
                request.Enabled,
                request.ClientType?.ToString(),
                request.PageNumber,
                request.PageSize),
            cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        var items = result.Value.Data
            .Select(c => new ClientListItem(
                c.Id,
                c.ClientId,
                c.DisplayName,
                ClientRows.ParseEnum<ClientType>(c.ClientType),
                c.Enabled,
                c.IsFirstParty,
                ClientRows.AsUtc(c.CreatedAt)))
            .ToList();

        return Result.Ok(new PaginationListResponse<ClientListItem>(result.Value.RecordsTotal, items));
    }
}
