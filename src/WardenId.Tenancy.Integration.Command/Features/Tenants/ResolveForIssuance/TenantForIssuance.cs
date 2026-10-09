namespace WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;

/// <summary>A tenant that may appear at a protocol endpoint. Issuers must refuse Suspended tenants.</summary>
public sealed record TenantForIssuance(Guid TenantId, string Slug, TenantIssuanceStatus Status);
