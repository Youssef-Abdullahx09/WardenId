using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.ChangePassword;

public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        if (user.Credential is null || !passwordHasher.Verify(request.CurrentPassword, user.Credential.Hash))
            return Result.Fail(UserErrors.InvalidCredentials());

        var policyResult = PasswordPolicyFactory.Create(options.Value);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value;

        var evaluateResult = await PasswordPolicyEvaluator.EvaluateAsync(
            policy, request.NewPassword, user.Credential, user.PasswordHistory, passwordHasher, breachedPasswordChecker);
        if (evaluateResult.IsFailed) return evaluateResult;

        var credential = PasswordCredential.Create(
            passwordHasher.Hash(request.NewPassword), "Argon2id", options.Value.CurrentPasswordAlgorithmVersion, DateTimeOffset.UtcNow);

        var setPasswordResult = user.SetPassword(credential, policy);
        if (setPasswordResult.IsFailed) return setPasswordResult;

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok();
    }
}
