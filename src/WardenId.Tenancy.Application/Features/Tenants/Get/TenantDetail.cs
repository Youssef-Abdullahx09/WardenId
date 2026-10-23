using WardenId.Tenancy.Application.Utilities;
using WardenId.Tenancy.Domain;
using QueryItem = WardenId.Tenancy.Query.Contracts.Features.Tenants.Get.Item;

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
    /// <summary>Maps a query row; its keys are already newest first.</summary>
    public static TenantDetail From(QueryItem item) => new(
        item.Id,
        item.Slug,
        item.DisplayName,
        QueryRows.ParseStatus(item.Status),
        QueryRows.AsUtc(item.SuspendedAtUtc),
        item.SuspendedReason,
        QueryRows.AsUtc(item.CreatedAt),
        item.SigningKeys
            .Select(k =>
            {
                var status = QueryRows.ParseSigningKeyStatus(k.Status);
                return new SigningKeySummary(
                    k.SigningKeyId,
                    k.Algorithm,
                    status,
                    status == SigningKeyStatus.Active,
                    QueryRows.AsUtc(k.CreatedAt),
                    QueryRows.AsUtc(k.ActivatedAt),
                    QueryRows.AsUtc(k.RetiringSince),
                    QueryRows.AsUtc(k.RetiredAt));
            })
            .ToArray());
}

/// <summary>
/// Public metadata only; key material stays in Tenancy. IsActive is Status == Active, kept for
/// existing clients. RetiringSince is when the key stopped signing — retire it only once the
/// longest token lifetime has passed since then.
/// </summary>
public sealed record SigningKeySummary(
    Guid KeyId,
    string Algorithm,
    SigningKeyStatus Status,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset ActivatedAt,
    DateTimeOffset? RetiringSince,
    DateTimeOffset? RetiredAt);
