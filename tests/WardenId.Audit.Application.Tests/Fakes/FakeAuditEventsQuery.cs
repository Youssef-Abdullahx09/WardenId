using WardenId.Audit.Query.Contracts;
using WardenId.SharedKernel.Query.Contracts;
using Get = WardenId.Audit.Query.Contracts.Features.AuditEvents.Get;
using List = WardenId.Audit.Query.Contracts.Features.AuditEvents.List;

namespace WardenId.Audit.Application.Tests.Fakes;

public sealed class FakeAuditEventsQuery : IAuditEventsQuery
{
    public Result<PaginationListResponse<List.Item>> ListResponse { get; set; } =
        Result.Ok(new PaginationListResponse<List.Item>(0, []));

    public List<List.Query> ListCalls { get; } = [];

    public Result<Get.Item?> GetResponse { get; set; } = Result.Ok<Get.Item?>(null);

    public List<Get.Query> GetCalls { get; } = [];

    public Task<Result<PaginationListResponse<List.Item>>> List(List.Query query, CancellationToken cancellationToken)
    {
        ListCalls.Add(query);
        return Task.FromResult(ListResponse);
    }

    public Task<Result<Get.Item?>> Get(Get.Query query, CancellationToken cancellationToken)
    {
        GetCalls.Add(query);
        return Task.FromResult(GetResponse);
    }
}
