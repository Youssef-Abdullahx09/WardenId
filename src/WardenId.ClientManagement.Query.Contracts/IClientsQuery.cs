using FluentResults;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.ClientManagement.Query.Contracts;

/// <summary>
/// Read-side (Dapper) queries over the ClientManagement schema's clients. Soft-deleted clients are never returned;
/// a single-client lookup that matches nothing succeeds with a null item.
/// </summary>
public interface IClientsQuery
{
    Task<Result<Features.Clients.Get.Item?>> Get(
        Features.Clients.Get.Query query,
        CancellationToken cancellationToken);

    Task<Result<PaginationListResponse<Features.Clients.List.Item>>> List(
        Features.Clients.List.Query query,
        CancellationToken cancellationToken);

    Task<Result<Features.Clients.GetForAuthentication.Item?>> GetForAuthentication(
        Features.Clients.GetForAuthentication.Query query,
        CancellationToken cancellationToken);

    Task<Result<Features.Clients.GetReference.Item?>> GetReference(
        Features.Clients.GetReference.Query query,
        CancellationToken cancellationToken);

    Task<Result<List<string>>> ListCorsOrigins(
        Features.Clients.ListCorsOrigins.Query query,
        CancellationToken cancellationToken);

    Task<Result<Features.Clients.VerifySecret.Item?>> VerifySecret(
        Features.Clients.VerifySecret.Query query,
        CancellationToken cancellationToken);
}
