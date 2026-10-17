using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.ChangePassword;

public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    IdentityPolicyResolver policies) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        if (user.Credential is null || !passwordHasher.Verify(request.CurrentPassword, user.Credential.Hash))
            return Result.Fail(UserErrors.InvalidCredentials());

        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var effective = policyResult.Value;
        var policy = effective.Password;

        var evaluateResult = await PasswordPolicyEvaluator.EvaluateAsync(
            policy, request.NewPassword, user.Credential, user.PasswordHistory, passwordHasher, breachedPasswordChecker);
        if (evaluateResult.IsFailed) return evaluateResult;

        var credential = PasswordCredential.Create(
            passwordHasher.Hash(request.NewPassword), passwordHasher.Algorithm, effective.CurrentPasswordAlgorithmVersion, DateTimeOffset.UtcNow);

        var setPasswordResult = user.SetPassword(credential, policy);
        if (setPasswordResult.IsFailed) return setPasswordResult;

        var saveResult = await repository.UpdateAsync(user, user.Id, cancellationToken);
        if (saveResult.IsFailed) return Result.Fail(saveResult.Errors);

        return Result.Ok();
    }
}
