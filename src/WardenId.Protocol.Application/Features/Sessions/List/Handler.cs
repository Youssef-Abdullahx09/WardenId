using FluentResults;
using MediatR;
using WardenId.Protocol.Domain;
using WardenId.Protocol.Query.Contracts;
using WardenId.Protocol.Query.Contracts.Features.Sessions.List;
using WardenId.SharedKernel.Query.Contracts;
using SessionsListQuery = WardenId.Protocol.Query.Contracts.Features.Sessions.List.Query;

namespace WardenId.Protocol.Application.Features.Sessions.List;

/// <summary>Read side: a tenant's sessions come from the Dapper query layer (Protocol.Query), not the aggregate.</summary>
public sealed class Handler(
    ISessionsQuery sessionsQuery,
    TimeProvider timeProvider) : IRequestHandler<Query, Result<PaginationListResponse<Item>>>
{
    public const int MaxPageSize = 100;

    public async Task<Result<PaginationListResponse<Item>>> Handle(Query request, CancellationToken cancellationToken)
    {
        if (request.PageNumber < 1)
            return Result.Fail(ProtocolErrors.Validation("Page number must be at least 1."));
        if (request.PageSize is < 1 or > MaxPageSize)
            return Result.Fail(ProtocolErrors.Validation($"Page size must be between 1 and {MaxPageSize}."));

        // Minimal-API enum binding also accepts numbers, so an undefined member can get this far.
        if (!Enum.IsDefined(request.SortOption) || !Enum.IsDefined(request.SortDirection))
            return Result.Fail(ProtocolErrors.Validation(
                $"SortOption must be one of {string.Join(", ", Enum.GetNames<SortOption>())} " +
                $"and SortDirection one of {string.Join(", ", Enum.GetNames<SortDirection>())}."));

        return await sessionsQuery.List(
            new SessionsListQuery(request.TenantId, request.PageNumber, request.PageSize, request.SortDirection)
            {
                UserId = request.UserId,
                ClientId = request.ClientId,
                IncludeEnded = request.IncludeEnded,
                Now = timeProvider.GetUtcNow(),
                SortOption = request.SortOption
            },
            cancellationToken);
    }
}
