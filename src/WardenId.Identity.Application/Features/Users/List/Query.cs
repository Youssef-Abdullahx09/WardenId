using FluentResults;
using MediatR;
using WardenId.Identity.Domain;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Identity.Application.Features.Users.List;

/// <param name="Search">Case-insensitive prefix of the email or username; blank means no filter.</param>
/// <param name="Sort">One of <see cref="UserListSort"/>'s values; null means newest first.</param>
public sealed record Query(
    Guid TenantId,
    int PageNumber,
    int PageSize,
    UserStatus? Status,
    string? Search = null,
    string? Sort = null) : IRequest<Result<PaginationListResponse<UserListItem>>>;
