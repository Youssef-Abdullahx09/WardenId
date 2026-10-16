using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Identity.Application.Features.Users.List;

public sealed class Handler(IIdentityDbContext dbContext) : IRequestHandler<Query, Result<PaginationListResponse<UserListItem>>>
{
    public async Task<Result<PaginationListResponse<UserListItem>>> Handle(Query request, CancellationToken cancellationToken)
    {
        // Soft-deleted users are hidden unless asked for by status, so they can be found to restore.
        var includeDeleted = request.Status == UserStatus.SoftDeleted;
        var query = dbContext.Users.Where(u => u.TenantId == request.TenantId && (includeDeleted || !u.IsDeleted));

        if (request.Status is not null)
            query = query.Where(u => u.Status == request.Status);

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserListItem(u.Id, u.Email.Value, u.Username.Value, u.Status, u.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Ok(new PaginationListResponse<UserListItem>(totalCount, items));
    }
}
