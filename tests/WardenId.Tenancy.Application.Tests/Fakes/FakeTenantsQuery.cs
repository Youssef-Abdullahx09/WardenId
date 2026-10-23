using WardenId.SharedKernel.Query.Contracts;
using WardenId.Tenancy.Query.Contracts;
using Get = WardenId.Tenancy.Query.Contracts.Features.Tenants.Get;
using GetBySlug = WardenId.Tenancy.Query.Contracts.Features.Tenants.GetBySlug;
using List = WardenId.Tenancy.Query.Contracts.Features.Tenants.List;
using ResolveForIssuance = WardenId.Tenancy.Query.Contracts.Features.Tenants.ResolveForIssuance;

namespace WardenId.Tenancy.Application.Tests.Fakes;

public sealed class FakeTenantsQuery : ITenantsQuery
{
    public Result<Get.Item?> GetResponse { get; set; } = Result.Ok<Get.Item?>(null);
    public List<Get.Query> GetCalls { get; } = [];

    public Result<Get.Item?> GetBySlugResponse { get; set; } = Result.Ok<Get.Item?>(null);
    public List<GetBySlug.Query> GetBySlugCalls { get; } = [];

    public Result<PaginationListResponse<List.Item>> ListResponse { get; set; } =
        Result.Ok(new PaginationListResponse<List.Item>(0, []));
    public List<List.Query> ListCalls { get; } = [];

    public Result<ResolveForIssuance.Item?> ResolveForIssuanceResponse { get; set; } =
        Result.Ok<ResolveForIssuance.Item?>(null);
    public List<ResolveForIssuance.Query> ResolveForIssuanceCalls { get; } = [];

    public Task<Result<Get.Item?>> Get(Get.Query query, CancellationToken cancellationToken)
    {
        GetCalls.Add(query);
        return Task.FromResult(GetResponse);
    }

    public Task<Result<Get.Item?>> GetBySlug(GetBySlug.Query query, CancellationToken cancellationToken)
    {
        GetBySlugCalls.Add(query);
        return Task.FromResult(GetBySlugResponse);
    }

    public Task<Result<PaginationListResponse<List.Item>>> List(List.Query query, CancellationToken cancellationToken)
    {
        ListCalls.Add(query);
        return Task.FromResult(ListResponse);
    }

    public Task<Result<ResolveForIssuance.Item?>> ResolveForIssuance(
        ResolveForIssuance.Query query, CancellationToken cancellationToken)
    {
        ResolveForIssuanceCalls.Add(query);
        return Task.FromResult(ResolveForIssuanceResponse);
    }
}
