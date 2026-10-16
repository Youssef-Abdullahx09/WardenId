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
        var sortResult = UserListSort.Parse(request.Sort);
        if (sortResult.IsFailed) return Result.Fail(sortResult.Errors);

        var search = request.Search?.Trim();
        if (search?.Length > Email.MaxLength)
            return Result.Fail(UserErrors.Validation($"Search cannot exceed {Email.MaxLength} characters."));

        // Soft-deleted users are hidden unless asked for by status, so they can be found to restore.
        var includeDeleted = request.Status == UserStatus.SoftDeleted;
        var query = dbContext.Users.Where(u => u.TenantId == request.TenantId && (includeDeleted || !u.IsDeleted));

        if (request.Status is not null)
            query = query.Where(u => u.Status == request.Status);

        if (!string.IsNullOrEmpty(search))
            query = WhereEmailOrUsernameStartsWith(query, search.ToLowerInvariant());

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await OrderBy(query, sortResult.Value)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserListItem(u.Id, u.Email.Value, u.Username.Value, u.Status, u.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Ok(new PaginationListResponse<UserListItem>(totalCount, items));
    }

    // Email and Username are value objects stored as strings; the (string)(object) casts let EF
    // compare the stored column. Emails are stored lowercased (Email.Create), usernames keep their
    // case, so only the username is lowered. StartsWith becomes LIKE 'prefix%' with the prefix's
    // own % and _ escaped. No index serves the match today (the default-collation unique indexes
    // can't do LIKE ranges): the TenantId index bounds the scan to one tenant. A prefix rather than
    // a contains match keeps a plain btree index possible later (text_pattern_ops on Email,
    // lower("Username")); contains would need pg_trgm.
    private static IQueryable<User> WhereEmailOrUsernameStartsWith(IQueryable<User> query, string prefix) =>
        query.Where(u =>
            ((string)(object)u.Email).StartsWith(prefix) ||
            ((string)(object)u.Username).ToLower().StartsWith(prefix));

    // Id breaks ties (CreatedAt can repeat), so pages don't overlap or skip rows.
    private static IOrderedQueryable<User> OrderBy(IQueryable<User> query, UserListSort sort) =>
        (sort.Field, sort.Descending) switch
        {
            (UserSortField.Email, false) => query.OrderBy(u => (string)(object)u.Email).ThenBy(u => u.Id),
            (UserSortField.Email, true) => query.OrderByDescending(u => (string)(object)u.Email).ThenByDescending(u => u.Id),
            (UserSortField.Username, false) => query.OrderBy(u => (string)(object)u.Username).ThenBy(u => u.Id),
            (UserSortField.Username, true) => query.OrderByDescending(u => (string)(object)u.Username).ThenByDescending(u => u.Id),
            (_, false) => query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id),
            (_, true) => query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
        };
}
