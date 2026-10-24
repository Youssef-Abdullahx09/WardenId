using FluentResults;
using MediatR;
using WardenId.ClientManagement.Domain;
using WardenId.ClientManagement.Query.Contracts;
using WardenId.ClientManagement.Query.Contracts.Features.Clients.List;
using WardenId.SharedKernel.Query.Contracts;
using ClientsListQuery = WardenId.ClientManagement.Query.Contracts.Features.Clients.List.Query;

namespace WardenId.ClientManagement.Application.Features.Clients.List;

/// <summary>Read side: a tenant's clients come from the Dapper query layer (ClientManagement.Query), not the aggregate.</summary>
public sealed class Handler(IClientsQuery clientsQuery) : IRequestHandler<Query, Result<PaginationListResponse<Item>>>
{
    public async Task<Result<PaginationListResponse<Item>>> Handle(Query request, CancellationToken cancellationToken)
    {
        // Minimal-API enum binding also accepts numbers, so an undefined member can get this far.
        if (!Enum.IsDefined(request.SortOption) || !Enum.IsDefined(request.SortDirection))
            return Result.Fail(ClientErrors.Validation(
                $"SortOption must be one of {string.Join(", ", Enum.GetNames<SortOption>())} " +
                $"and SortDirection one of {string.Join(", ", Enum.GetNames<SortDirection>())}."));

        return await clientsQuery.List(
            new ClientsListQuery(request.TenantId, request.PageNumber, request.PageSize, request.SortDirection)
            {
                Enabled = request.Enabled,
                ClientType = request.ClientType,
                SortOption = request.SortOption
            },
            cancellationToken);
    }
}
