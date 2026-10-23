using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Identity.Query.Contracts.Features.Users.List;

/// <summary>
/// One page of a tenant's users. Soft-deleted users are left out unless <paramref name="IncludeDeleted"/>.
/// </summary>
/// <param name="Status">The <c>UserStatus</c> member name to match; null for any.</param>
/// <param name="SearchPrefix">
/// Already lowercased prefix of the email or (lowercased) username, matched literally (% and _ are not wildcards);
/// null for no filter.
/// </param>
public sealed record Query(
    Guid TenantId,
    bool IncludeDeleted,
    string? Status,
    string? SearchPrefix,
    SortField SortField,
    SortDirection SortDirection,
    int PageNumber,
    int PageSize);
