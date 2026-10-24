using FluentResults;
using MediatR;
using WardenId.Identity.Domain;
using WardenId.Identity.Query.Contracts;
using WardenId.Identity.Query.Contracts.Features.Users.List;
using WardenId.SharedKernel.Query.Contracts;
using UserStatus = WardenId.Identity.Query.Contracts.Features.Users.UserStatus;
using UsersListQuery = WardenId.Identity.Query.Contracts.Features.Users.List.Query;

namespace WardenId.Identity.Application.Features.Users.List;

/// <summary>Read side: a tenant's users come from the Dapper query layer (Identity.Query), not the aggregate.</summary>
public sealed class Handler(IUsersQuery usersQuery) : IRequestHandler<Query, Result<PaginationListResponse<Item>>>
{
    public async Task<Result<PaginationListResponse<Item>>> Handle(Query request, CancellationToken cancellationToken)
    {
        // Minimal-API enum binding also accepts numbers, so an undefined member can get this far.
        if (!Enum.IsDefined(request.SortOption) || !Enum.IsDefined(request.SortDirection))
            return Result.Fail(UserErrors.Validation(
                $"SortOption must be one of {string.Join(", ", Enum.GetNames<SortOption>())} " +
                $"and SortDirection one of {string.Join(", ", Enum.GetNames<SortDirection>())}."));

        if (request.Search?.Trim().Length > Email.MaxLength)
            return Result.Fail(UserErrors.Validation($"Search cannot exceed {Email.MaxLength} characters."));

        return await usersQuery.List(
            new UsersListQuery(request.TenantId, request.PageNumber, request.PageSize, request.SortDirection)
            {
                // Soft-deleted users are hidden unless asked for by status, so they can be found to restore.
                IncludeDeleted = request.Status == UserStatus.SoftDeleted,
                Status = request.Status,
                Search = request.Search,
                SortOption = request.SortOption
            },
            cancellationToken);
    }
}
