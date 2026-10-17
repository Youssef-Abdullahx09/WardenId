using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;
using WardenId.Identity.Integration.Command.Features.Users.GetAccount;

namespace WardenId.Identity.Application.Features.Users.GetAccount;

/// <summary>The user's own view of their account, with the tenant policy bits the account page and sign-in steps need.</summary>
public sealed class Handler(
    IUserRepository repository,
    IdentityPolicyResolver policies) : IRequestHandler<Query, Result<AccountView>>
{
    public async Task<Result<AccountView>> Handle(Query request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);
        var user = userResult.Value;

        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value;

        var factors = user.MfaFactors
            .Where(f => f.Enabled)
            .OrderBy(f => f.EnrolledAtUtc)
            .Select(f => new AccountFactor(f.Id, f.FactorType.ToString(), f.EnrolledAtUtc, f.LastUsedAtUtc))
            .ToArray();

        return Result.Ok(new AccountView(
            user.Email.Value,
            user.Username.Value,
            user.TermsAcceptedAtUtc,
            policy.TermsUrl,
            policy.Mfa.Mode != MfaPolicyMode.Off,
            MfaPolicyEvaluator.IsMfaRequired(policy.Mfa),
            factors,
            user.RemainingRecoveryCodeCount));
    }
}
