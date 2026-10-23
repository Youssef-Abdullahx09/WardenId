using FluentResults;

namespace WardenId.Identity.Query.Contracts;

/// <summary>Read-side (Dapper) queries over the Identity schema's tenant policies.</summary>
public interface IPoliciesQuery
{
    /// <summary>The tenant's stored identity policy overrides; null when the tenant has no row.</summary>
    Task<Result<Features.Policies.GetIdentity.Item?>> GetIdentity(
        Features.Policies.GetIdentity.Query query,
        CancellationToken cancellationToken);
}
