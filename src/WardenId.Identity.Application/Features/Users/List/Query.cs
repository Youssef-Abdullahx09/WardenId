using FluentResults;
using MediatR;
using WardenId.Identity.Domain;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Identity.Application.Features.Users.List;

public sealed record Query(
    Guid TenantId,
    int PageNumber,
    int PageSize,
    UserStatus? Status) : IRequest<Result<PaginationListResponse<UserListItem>>>;
