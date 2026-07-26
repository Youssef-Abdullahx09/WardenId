using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.ClientManagement.Application.Features.Clients.List;

public sealed class Handler(
    IClientManagementDbContext dbContext) : IRequestHandler<Query, Result<PaginationListResponse<ClientListItem>>>
{
    public async Task<Result<PaginationListResponse<ClientListItem>>> Handle(
        Query request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Clients
            .Where(c => c.TenantId == request.TenantId && !c.IsDeleted);

        if (request.Enabled is not null)
            query = query.Where(c => c.Enabled == request.Enabled);

        if (request.ClientType is not null)
            query = query.Where(c => c.ClientType == request.ClientType);

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new ClientListItem(
                c.Id, c.ClientId.Value, c.DisplayName, c.ClientType, c.Enabled, c.IsFirstParty, c.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Ok(new PaginationListResponse<ClientListItem>(totalCount, items));
    }
}
