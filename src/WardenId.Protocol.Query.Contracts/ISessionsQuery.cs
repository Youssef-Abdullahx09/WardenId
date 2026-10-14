using FluentResults;

namespace WardenId.Protocol.Query.Contracts;

/// <summary>Read-side (Dapper) queries over the Protocol schema's sessions.</summary>
public interface ISessionsQuery
{
    Task<Result<List<Features.Sessions.ListForUser.Item>>> ListForUser(
        Features.Sessions.ListForUser.Query query,
        CancellationToken cancellationToken);
}
