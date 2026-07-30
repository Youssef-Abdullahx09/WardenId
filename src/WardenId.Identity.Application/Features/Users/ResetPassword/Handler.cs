using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.ResetPassword;

public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailed) return Result.Fail(UserErrors.TokenInvalid());

        var userResult = await repository.GetByEmailAsync(request.TenantId, emailResult.Value, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(UserErrors.TokenInvalid());

        var user = userResult.Value;
        var tokenHash = SecureTokenGenerator.Hash(request.RawToken);
        var nowUtc = DateTimeOffset.UtcNow;

        // Token validity is checked BEFORE the password is evaluated against policy: evaluating first
        // turned this endpoint into an unauthenticated password oracle (a `PasswordReused` failure told
        // an anonymous caller their guess matched the account's real password), with no lockout on the path.
        var tokenIsValid = user.PasswordResetTokens
            .Any(t => t.Matches(tokenHash) && t.ConsumedAtUtc is null && t.ExpiresAtUtc > nowUtc);
        if (!tokenIsValid) return Result.Fail(UserErrors.TokenInvalid());

        var policyResult = PasswordPolicyFactory.Create(options.Value);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value;

        var evaluateResult = await PasswordPolicyEvaluator.EvaluateAsync(
            policy, request.NewPassword, user.Credential, user.PasswordHistory, passwordHasher, breachedPasswordChecker);
        if (evaluateResult.IsFailed) return evaluateResult;

        var credential = PasswordCredential.Create(
            passwordHasher.Hash(request.NewPassword), "Argon2id", options.Value.CurrentPasswordAlgorithmVersion, DateTimeOffset.UtcNow);

        var resetResult = user.ResetPassword(tokenHash, credential, policy);
        if (resetResult.IsFailed) return resetResult;

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok();
    }
}
