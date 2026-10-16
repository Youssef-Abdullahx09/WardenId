using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Application.Features.Tenants.Get;

public sealed record TenantDetail(
    Guid Id,
    string Slug,
    string DisplayName,
    Status Status,
    DateTimeOffset? SuspendedAtUtc,
    string? SuspendedReason,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<SigningKeySummary> SigningKeys)
{
    public static TenantDetail From(Tenant tenant) => new(
        tenant.Id,
        tenant.Slug.Value,
        tenant.DisplayName,
        tenant.Status,
        tenant.SuspendedAtUtc,
        tenant.SuspendedReason,
        tenant.CreatedAt,
        tenant.SigningKeys
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new SigningKeySummary(k.SigningKeyId, k.Algorithm, k.IsActive, k.CreatedAt))
            .ToArray());
}

/// <summary>Public metadata only; key material stays in Tenancy.</summary>
public sealed record SigningKeySummary(Guid KeyId, string Algorithm, bool IsActive, DateTimeOffset CreatedAt);
