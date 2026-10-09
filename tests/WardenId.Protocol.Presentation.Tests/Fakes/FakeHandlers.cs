using MediatR;
using Microsoft.AspNetCore.DataProtection;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;
using GetActiveForSigningQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.Query;
using GetForAuthenticationQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication.Query;
using GetPublicKeysQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys.Query;
using ResolveForIssuanceQuery = WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance.Query;
using TenancyActiveSigningKey = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.ActiveSigningKey;
using VerifySecretQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.VerifySecret.Query;

namespace WardenId.Protocol.Presentation.Tests.Fakes;

internal static class Fail
{
    public static Error NotFound => new Error("Not found.").WithMetadata("ErrorCode", "NotFound");
}

public sealed class ResolveForIssuanceHandler(FakeDirectory directory)
    : IRequestHandler<ResolveForIssuanceQuery, Result<TenantForIssuance>>
{
    public Task<Result<TenantForIssuance>> Handle(ResolveForIssuanceQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(directory.Tenants.TryGetValue(request.Slug, out var t)
            ? Result.Ok(new TenantForIssuance(t.TenantId, t.Slug, t.Status))
            : Result.Fail<TenantForIssuance>(Fail.NotFound));
}

public sealed class GetPublicKeysHandler(FakeDirectory directory)
    : IRequestHandler<GetPublicKeysQuery, Result<TenantPublicKeys>>
{
    public Task<Result<TenantPublicKeys>> Handle(GetPublicKeysQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(directory.Tenants.TryGetValue(request.Slug, out var t)
            ? Result.Ok(new TenantPublicKeys(t.TenantId, t.Slug, []))
            : Result.Fail<TenantPublicKeys>(Fail.NotFound));
}

public sealed class GetActiveForSigningHandler(FakeDirectory directory, IDataProtectionProvider dataProtection)
    : IRequestHandler<GetActiveForSigningQuery, Result<TenancyActiveSigningKey>>
{
    public Task<Result<TenancyActiveSigningKey>> Handle(GetActiveForSigningQuery request, CancellationToken cancellationToken)
    {
        var tenant = directory.Tenants.Values.Single(t => t.TenantId == request.TenantId);
        var encrypted = dataProtection.CreateProtector(SigningKeyProtectionPurposes.PrivateKey)
            .Protect(tenant.Key.ExportPkcs8PrivateKey());
        return Task.FromResult(Result.Ok(new TenancyActiveSigningKey(tenant.KeyId, "ES256", encrypted, tenant.Slug)));
    }
}

public sealed class GetForAuthenticationHandler(FakeDirectory directory)
    : IRequestHandler<GetForAuthenticationQuery, Result<ClientForAuthentication>>
{
    public Task<Result<ClientForAuthentication>> Handle(GetForAuthenticationQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(directory.FindClient(request.TenantId, request.ClientId) is { } c
            ? Result.Ok(new ClientForAuthentication(c.Id, c.ClientId, c.Kind, c.Enabled, c.Grants, c.Auth, c.AccessTokenLifetimeSeconds))
            : Result.Fail<ClientForAuthentication>(Fail.NotFound));
}

public sealed class VerifySecretHandler(FakeDirectory directory) : IRequestHandler<VerifySecretQuery, Result>
{
    public Task<Result> Handle(VerifySecretQuery request, CancellationToken cancellationToken)
    {
        var client = directory.FindClient(request.TenantId, request.ClientId);
        var ok = client is { Enabled: true, Kind: ClientKind.Confidential, Auth: ClientAuthMethod.SharedSecret }
                 && request.PresentedSecret.Length > 0
                 && client.Secrets.Contains(request.PresentedSecret);
        return Task.FromResult(ok ? Result.Ok() : Result.Fail(new Error("Invalid client credentials.")));
    }
}
