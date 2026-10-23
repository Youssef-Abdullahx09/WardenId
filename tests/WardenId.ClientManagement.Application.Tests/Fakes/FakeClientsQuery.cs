using WardenId.ClientManagement.Query.Contracts;
using WardenId.SharedKernel.Query.Contracts;
using Get = WardenId.ClientManagement.Query.Contracts.Features.Clients.Get;
using GetForAuthentication = WardenId.ClientManagement.Query.Contracts.Features.Clients.GetForAuthentication;
using GetReference = WardenId.ClientManagement.Query.Contracts.Features.Clients.GetReference;
using List = WardenId.ClientManagement.Query.Contracts.Features.Clients.List;
using ListCorsOrigins = WardenId.ClientManagement.Query.Contracts.Features.Clients.ListCorsOrigins;
using VerifySecret = WardenId.ClientManagement.Query.Contracts.Features.Clients.VerifySecret;

namespace WardenId.ClientManagement.Application.Tests.Fakes;

/// <summary>Canned responses per query, recording every call.</summary>
public sealed class FakeClientsQuery : IClientsQuery
{
    public Result<Get.Item?> GetResponse { get; set; } = Result.Ok<Get.Item?>(null);
    public List<Get.Query> GetCalls { get; } = [];

    public Result<PaginationListResponse<List.Item>> ListResponse { get; set; } =
        Result.Ok(new PaginationListResponse<List.Item>(0, []));
    public List<List.Query> ListCalls { get; } = [];

    public Result<GetForAuthentication.Item?> GetForAuthenticationResponse { get; set; } =
        Result.Ok<GetForAuthentication.Item?>(null);
    public List<GetForAuthentication.Query> GetForAuthenticationCalls { get; } = [];

    public Result<GetReference.Item?> GetReferenceResponse { get; set; } = Result.Ok<GetReference.Item?>(null);
    public List<GetReference.Query> GetReferenceCalls { get; } = [];

    public Result<List<string>> ListCorsOriginsResponse { get; set; } = Result.Ok(new List<string>());
    public List<ListCorsOrigins.Query> ListCorsOriginsCalls { get; } = [];

    public Result<VerifySecret.Item?> VerifySecretResponse { get; set; } = Result.Ok<VerifySecret.Item?>(null);
    public List<VerifySecret.Query> VerifySecretCalls { get; } = [];

    public Task<Result<Get.Item?>> Get(Get.Query query, CancellationToken cancellationToken)
    {
        GetCalls.Add(query);
        return Task.FromResult(GetResponse);
    }

    public Task<Result<PaginationListResponse<List.Item>>> List(List.Query query, CancellationToken cancellationToken)
    {
        ListCalls.Add(query);
        return Task.FromResult(ListResponse);
    }

    public Task<Result<GetForAuthentication.Item?>> GetForAuthentication(
        GetForAuthentication.Query query, CancellationToken cancellationToken)
    {
        GetForAuthenticationCalls.Add(query);
        return Task.FromResult(GetForAuthenticationResponse);
    }

    public Task<Result<GetReference.Item?>> GetReference(GetReference.Query query, CancellationToken cancellationToken)
    {
        GetReferenceCalls.Add(query);
        return Task.FromResult(GetReferenceResponse);
    }

    public Task<Result<List<string>>> ListCorsOrigins(ListCorsOrigins.Query query, CancellationToken cancellationToken)
    {
        ListCorsOriginsCalls.Add(query);
        return Task.FromResult(ListCorsOriginsResponse);
    }

    public Task<Result<VerifySecret.Item?>> VerifySecret(VerifySecret.Query query, CancellationToken cancellationToken)
    {
        VerifySecretCalls.Add(query);
        return Task.FromResult(VerifySecretResponse);
    }
}
