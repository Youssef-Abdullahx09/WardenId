using WardenId.Protocol.Query.Contracts;
using ListForUser = WardenId.Protocol.Query.Contracts.Features.Sessions.ListForUser;

namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeSessionsQuery : ISessionsQuery
{
    public Result<List<ListForUser.Item>> ListForUserResponse { get; set; } = Result.Ok(new List<ListForUser.Item>());

    public List<ListForUser.Query> ListForUserCalls { get; } = [];

    public Task<Result<List<ListForUser.Item>>> ListForUser(ListForUser.Query query, CancellationToken cancellationToken)
    {
        ListForUserCalls.Add(query);
        return Task.FromResult(ListForUserResponse);
    }
}
