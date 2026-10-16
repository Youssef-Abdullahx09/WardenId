using FluentResults;
using MediatR;
using WardenId.Protocol.Domain;
using WardenId.Protocol.Query.Contracts;
using WardenId.SharedKernel.Query.Contracts;
using SessionsQuery = WardenId.Protocol.Query.Contracts.Features.Sessions.List.Query;

namespace WardenId.Protocol.Application.Features.Sessions.List;

/// <summary>Read side: a tenant's sessions come from the Dapper query layer (Protocol.Query), not the aggregate.</summary>
public sealed class Handler(
    ISessionsQuery sessionsQuery,
    TimeProvider timeProvider) : IRequestHandler<Query, Result<PaginationListResponse<SessionListItem>>>
{
    public const int MaxPageSize = 100;

    public async Task<Result<PaginationListResponse<SessionListItem>>> Handle(Query request, CancellationToken cancellationToken)
    {
        if (request.PageNumber < 1)
            return Result.Fail(ProtocolErrors.Validation("Page number must be at least 1."));
        if (request.PageSize is < 1 or > MaxPageSize)
            return Result.Fail(ProtocolErrors.Validation($"Page size must be between 1 and {MaxPageSize}."));

        var result = await sessionsQuery.List(
            new SessionsQuery(
                request.TenantId,
                request.UserId,
                request.ClientId,
                request.IncludeEnded,
                timeProvider.GetUtcNow(),
                request.PageNumber,
                request.PageSize),
            cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        var sessions = result.Value.Data
            .Select(item => new SessionListItem(
                item.Sid,
                item.UserId,
                SessionTimes.AsUtc(item.AuthTime),
                SessionTimes.AsUtc(item.ExpiresAt),
                SessionTimes.AsUtc(item.EndedAt),
                SessionTimes.ParseEndReason(item.EndReason),
                item.ClientIds))
            .ToList();

        return Result.Ok(new PaginationListResponse<SessionListItem>(result.Value.RecordsTotal, sessions));
    }
}
