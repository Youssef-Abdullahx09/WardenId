using FluentResults;
using MediatR;

namespace WardenId.Identity.Integration.Command.Features.Users.GetAccount;

/// <summary>What the user's own account page and the sign-in steps show. Never contains secrets.</summary>
public sealed record Query(Guid TenantId, Guid UserId) : IRequest<Result<AccountView>>;

/// <param name="TermsUrl">The tenant's terms of service link (tenant policy, else global), or null if none is set.</param>
/// <param name="MfaAvailable">The tenant's MFA policy isn't Off.</param>
/// <param name="MfaRequired">The tenant's MFA policy requires MFA, so the last factor can't be removed.</param>
/// <param name="Factors">Enabled factors only.</param>
public sealed record AccountView(
    string Email,
    string Username,
    DateTimeOffset? TermsAcceptedAtUtc,
    string? TermsUrl,
    bool MfaAvailable,
    bool MfaRequired,
    IReadOnlyList<AccountFactor> Factors,
    int RemainingRecoveryCodes);

public sealed record AccountFactor(Guid Id, AccountFactorType Type, DateTimeOffset EnrolledAtUtc, DateTimeOffset? LastUsedAtUtc);
