using FluentResults;
using MediatR;
using WardenId.Protocol.Query.Contracts.Features.Sessions.List;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Protocol.Application.Features.Sessions.List;

public sealed record Query(
    Guid TenantId,
    int PageNumber,
    int PageSize,
    SortDirection SortDirection) : IRequest<Result<PaginationListResponse<Item>>>
{
    /// <summary>Only this user's sessions; null for every user of the tenant.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Only sessions this client (public client_id) took part in; null or blank for any.</summary>
    public string? ClientId { get; init; }

    /// <summary>Also ended and expired sessions; otherwise live ones only.</summary>
    public bool IncludeEnded { get; init; }

    public SortOption SortOption { get; init; } = SortOption.AuthTime;
}
