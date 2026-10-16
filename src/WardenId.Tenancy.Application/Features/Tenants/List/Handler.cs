using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.SharedKernel.Query.Contracts;
using WardenId.Tenancy.Application.Abstractions;

namespace WardenId.Tenancy.Application.Features.Tenants.List;

public sealed class Handler(ITenancyDbContext dbContext)
    : IRequestHandler<Query, Result<PaginationListResponse<TenantListItem>>>
{
    public async Task<Result<PaginationListResponse<TenantListItem>>> Handle(
        Query request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Tenants.Where(t => !t.IsDeleted);

        if (request.Status is not null)
            query = query.Where(t => t.Status == request.Status);

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TenantListItem(t.Id, t.Slug.Value, t.DisplayName, t.Status, t.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Ok(new PaginationListResponse<TenantListItem>(totalCount, items));
    }
}
