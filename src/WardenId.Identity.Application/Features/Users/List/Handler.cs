using FluentResults;
using MediatR;
using WardenId.Identity.Application.Utilities;
using WardenId.Identity.Domain;
using WardenId.Identity.Query.Contracts;
using WardenId.SharedKernel.Query.Contracts;
using ListSortField = WardenId.Identity.Query.Contracts.Features.Users.List.SortField;
using UsersListQuery = WardenId.Identity.Query.Contracts.Features.Users.List.Query;

namespace WardenId.Identity.Application.Features.Users.List;

/// <summary>Read side: a tenant's users come from the Dapper query layer (Identity.Query), not the aggregate.</summary>
public sealed class Handler(IUsersQuery usersQuery) : IRequestHandler<Query, Result<PaginationListResponse<UserListItem>>>
{
    public async Task<Result<PaginationListResponse<UserListItem>>> Handle(Query request, CancellationToken cancellationToken)
    {
        var sortResult = UserListSort.Parse(request.Sort);
        if (sortResult.IsFailed) return Result.Fail(sortResult.Errors);
        var sort = sortResult.Value;

        var search = request.Search?.Trim();
        if (search?.Length > Email.MaxLength)
            return Result.Fail(UserErrors.Validation($"Search cannot exceed {Email.MaxLength} characters."));

        // Emails are stored lowercased (Email.Create) and the username is lowered in SQL, so the prefix is lowered here.
        // The match is a literal prefix (LIKE 'prefix%' with % and _ escaped); see Features/Users/List/Query.sql.
        var result = await usersQuery.List(
            new UsersListQuery(
                request.TenantId,
                // Soft-deleted users are hidden unless asked for by status, so they can be found to restore.
                IncludeDeleted: request.Status == UserStatus.SoftDeleted,
                request.Status?.ToString(),
                string.IsNullOrEmpty(search) ? null : search.ToLowerInvariant(),
                MapSortField(sort.Field),
                sort.Descending ? SortDirection.Desc : SortDirection.Asc,
                request.PageNumber,
                request.PageSize),
            cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        var users = result.Value.Data
            .Select(item => new UserListItem(
                item.Id,
                item.Email,
                item.Username,
                QueryColumns.ParseEnum<UserStatus>(item.Status),
                QueryColumns.AsUtc(item.CreatedAt)))
            .ToList();

        return Result.Ok(new PaginationListResponse<UserListItem>(result.Value.RecordsTotal, users));
    }

    private static ListSortField MapSortField(UserSortField field) => field switch
    {
        UserSortField.CreatedAt => ListSortField.CreatedAt,
        UserSortField.Email => ListSortField.Email,
        UserSortField.Username => ListSortField.Username,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unmapped UserSortField.")
    };
}
