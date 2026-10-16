using WardenId.Protocol.Query.Contracts;
using WardenId.SharedKernel.Query.Contracts;
using List = WardenId.Protocol.Query.Contracts.Features.Sessions.List;
using ListForUser = WardenId.Protocol.Query.Contracts.Features.Sessions.ListForUser;

namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeSessionsQuery : ISessionsQuery
{
    public Result<List<ListForUser.Item>> ListForUserResponse { get; set; } = Result.Ok(new List<ListForUser.Item>());

    public List<ListForUser.Query> ListForUserCalls { get; } = [];

    public Result<PaginationListResponse<List.Item>> ListResponse { get; set; } =
        Result.Ok(new PaginationListResponse<List.Item>(0, []));

    public List<List.Query> ListCalls { get; } = [];

    public Task<Result<List<ListForUser.Item>>> ListForUser(ListForUser.Query query, CancellationToken cancellationToken)
    {
        ListForUserCalls.Add(query);
        return Task.FromResult(ListForUserResponse);
    }

    public Task<Result<PaginationListResponse<List.Item>>> List(List.Query query, CancellationToken cancellationToken)
    {
        ListCalls.Add(query);
        return Task.FromResult(ListResponse);
    }
}
