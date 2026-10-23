using FluentResults;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Tenancy.Query.Contracts;

/// <summary>Read-side (Dapper) queries over the Tenancy schema's tenants. A null value means no matching tenant.</summary>
public interface ITenantsQuery
{
    Task<Result<Features.Tenants.Get.Item?>> Get(
        Features.Tenants.Get.Query query,
        CancellationToken cancellationToken);

    Task<Result<Features.Tenants.Get.Item?>> GetBySlug(
        Features.Tenants.GetBySlug.Query query,
        CancellationToken cancellationToken);

    Task<Result<PaginationListResponse<Features.Tenants.List.Item>>> List(
        Features.Tenants.List.Query query,
        CancellationToken cancellationToken);

    Task<Result<Features.Tenants.ResolveForIssuance.Item?>> ResolveForIssuance(
        Features.Tenants.ResolveForIssuance.Query query,
        CancellationToken cancellationToken);
}
