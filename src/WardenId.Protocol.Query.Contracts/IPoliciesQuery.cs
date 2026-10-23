using FluentResults;

namespace WardenId.Protocol.Query.Contracts;

/// <summary>Read-side (Dapper) queries over the Protocol schema's tenant token policies.</summary>
public interface IPoliciesQuery
{
    /// <summary>The tenant's lifetime overrides, or null when it has no policy row.</summary>
    Task<Result<Features.Policies.GetTokens.Item?>> GetTokens(
        Features.Policies.GetTokens.Query query,
        CancellationToken cancellationToken);
}
