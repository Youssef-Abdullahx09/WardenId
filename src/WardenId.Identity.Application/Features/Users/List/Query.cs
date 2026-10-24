using FluentResults;
using MediatR;
using WardenId.Identity.Query.Contracts.Features.Users;
using WardenId.Identity.Query.Contracts.Features.Users.List;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Identity.Application.Features.Users.List;

public sealed record Query(
    Guid TenantId,
    int PageNumber,
    int PageSize,
    SortDirection SortDirection) : IRequest<Result<PaginationListResponse<Item>>>
{
    public UserStatus? Status { get; init; }

    /// <summary>Case-insensitive prefix of the email, username, first name, last name or full name; blank means no filter.</summary>
    public string? Search { get; init; }

    public SortOption SortOption { get; init; } = SortOption.CreatedAt;
}
