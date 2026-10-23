using FluentResults;
using MediatR;
using WardenId.Identity.Application.Utilities;
using WardenId.Identity.Domain;
using WardenId.Identity.Integration.Command.Features.Users.GetAccount;
using WardenId.Identity.Query.Contracts;
using AccountQuery = WardenId.Identity.Query.Contracts.Features.Users.GetAccount.Query;
using GetAccountQuery = WardenId.Identity.Integration.Command.Features.Users.GetAccount.Query;

namespace WardenId.Identity.Application.Features.Users.GetAccount;

/// <summary>
/// The user's own view of their account, with the tenant policy bits the account page and sign-in steps need.
/// Read side: the user comes from the Dapper query layer (Identity.Query); the policy still goes through the resolver's cache.
/// </summary>
public sealed class Handler(
    IUsersQuery usersQuery,
    IdentityPolicyResolver policies) : IRequestHandler<GetAccountQuery, Result<AccountView>>
{
    public async Task<Result<AccountView>> Handle(GetAccountQuery request, CancellationToken cancellationToken)
    {
        var userResult = await usersQuery.GetAccount(new AccountQuery(request.TenantId, request.UserId), cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);
        var user = userResult.Value;
        if (user is null) return Result.Fail(UserErrors.NotFound);

        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value;

        var factors = user.EnabledFactors
            .OrderBy(f => f.EnrolledAtUtc)
            .Select(f => new AccountFactor(
                f.Id,
                ContractMapping.MapFactorType(QueryColumns.ParseEnum<MfaFactorType>(f.FactorType)),
                QueryColumns.AsUtc(f.EnrolledAtUtc),
                QueryColumns.AsUtc(f.LastUsedAtUtc)))
            .ToArray();

        return Result.Ok(new AccountView(
            user.Email,
            user.Username,
            QueryColumns.AsUtc(user.TermsAcceptedAtUtc),
            policy.TermsUrl,
            policy.Mfa.Mode != MfaPolicyMode.Off,
            MfaPolicyEvaluator.IsMfaRequired(policy.Mfa),
            factors,
            user.RemainingRecoveryCodeCount));
    }
}
