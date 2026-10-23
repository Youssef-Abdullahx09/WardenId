using FluentResults;
using MediatR;
using WardenId.Identity.Integration.Command.Features.Users.GetAccount;

namespace WardenId.Protocol.Application.Features.Account.Get;

/// <summary>The signed-in user's own account, for the account pages and the terms step. Never contains secrets.</summary>
public sealed record Query(Guid TenantId, Guid UserId) : IRequest<Result<AccountSummary>>;

/// <param name="TermsUrl">An absolute http(s) link to the tenant's terms, or null (none set, or not a web link).</param>
/// <param name="MfaAvailable">The tenant's MFA policy isn't Off: an authenticator app may be set up.</param>
/// <param name="MfaRequired">MFA can't be turned off.</param>
/// <param name="Factors">Enabled factors only.</param>
public sealed record AccountSummary(
    string Email,
    string Username,
    string? TermsUrl,
    bool MfaAvailable,
    bool MfaRequired,
    IReadOnlyList<AccountFactorSummary> Factors,
    int RemainingRecoveryCodes)
{
    public bool HasMfa => Factors.Count > 0;
}

public sealed record AccountFactorSummary(AccountFactorType Type, DateTimeOffset EnrolledAtUtc, DateTimeOffset? LastUsedAtUtc);
