using MediatR;
using Microsoft.AspNetCore.DataProtection;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;
using AuthenticateCommand = WardenId.Identity.Integration.Command.Features.Users.Authenticate.Command;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;
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
            ? Result.Ok(new ClientForAuthentication(c.Id, c.ClientId, c.Kind, c.Enabled, c.Grants, c.Auth, c.AccessTokenLifetimeSeconds, c.RedirectUris, c.RequirePkce))
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

public sealed class AuthenticateHandler(FakeDirectory directory)
    : IRequestHandler<AuthenticateCommand, Result<AuthenticatedUser>>
{
    public Task<Result<AuthenticatedUser>> Handle(AuthenticateCommand request, CancellationToken cancellationToken)
    {
        var user = directory.Users.FirstOrDefault(u =>
            u.TenantId == request.TenantId
            && (u.Username == request.EmailOrUsername || u.Email == request.EmailOrUsername));
        var ok = user is not null
                 && request.Password == FakeDirectory.UserPassword
                 && user.Status is not (UserStatus.Disabled or UserStatus.SoftDeleted or UserStatus.Locked);
        return Task.FromResult(ok
            ? Result.Ok(new AuthenticatedUser(user!.UserId, user.Email, user.Username, user.Status, user.PendingActions.ToArray()))
            : Result.Fail<AuthenticatedUser>(new Error("Invalid credentials.")));
    }
}

public sealed class GetForSignInHandler(FakeDirectory directory) : IRequestHandler<GetForSignInQuery, Result<SignInUser>>
{
    public Task<Result<SignInUser>> Handle(GetForSignInQuery request, CancellationToken cancellationToken)
    {
        var user = directory.Users.FirstOrDefault(u => u.TenantId == request.TenantId && u.UserId == request.UserId);
        return Task.FromResult(user is not null && user.Status != UserStatus.SoftDeleted
            ? Result.Ok(new SignInUser(user.UserId, user.Email, user.Username, user.Status, user.PendingActions.ToArray()))
            : Result.Fail<SignInUser>(Fail.NotFound));
    }
}
