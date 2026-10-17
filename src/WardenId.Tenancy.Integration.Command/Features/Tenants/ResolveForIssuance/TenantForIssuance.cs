namespace WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;

/// <summary>A tenant that may appear at a protocol endpoint. Issuers must refuse Suspended tenants.</summary>
/// <param name="DisplayName">The tenant's human-readable name, e.g. what an authenticator app shows.</param>
public sealed record TenantForIssuance(Guid TenantId, string Slug, TenantIssuanceStatus Status, string DisplayName);
