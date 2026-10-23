using FluentResults;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Protocol.Query.Contracts;

/// <summary>Read-side (Dapper) queries over the Protocol schema's sessions.</summary>
public interface ISessionsQuery
{
    Task<Result<List<Features.Sessions.ListForUser.Item>>> ListForUser(
        Features.Sessions.ListForUser.Query query,
        CancellationToken cancellationToken);

    Task<Result<PaginationListResponse<Features.Sessions.List.Item>>> List(
        Features.Sessions.List.Query query,
        CancellationToken cancellationToken);

    /// <summary>The tenant's session with this sid in any state, or null when there is none.</summary>
    Task<Result<Features.Sessions.GetLive.Item?>> GetLive(
        Features.Sessions.GetLive.Query query,
        CancellationToken cancellationToken);
}
