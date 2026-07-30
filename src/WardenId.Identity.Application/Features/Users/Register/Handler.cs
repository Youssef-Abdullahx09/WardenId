using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Register;

public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Command, Result<UserRegistered>>
{
    public async Task<Result<UserRegistered>> Handle(Command request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailed) return Result.Fail(emailResult.Errors);

        var usernameResult = Username.Create(request.Username);
        if (usernameResult.IsFailed) return Result.Fail(usernameResult.Errors);

        var email = emailResult.Value;
        var username = usernameResult.Value;

        if (await repository.ExistsByEmailAsync(request.TenantId, email, cancellationToken))
            return Result.Fail(UserErrors.EmailAlreadyInUse());

        if (await repository.ExistsByUsernameAsync(request.TenantId, username, cancellationToken))
            return Result.Fail(UserErrors.UsernameAlreadyInUse());

        var policyResult = PasswordPolicyFactory.Create(options.Value);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value;

        var evaluateResult = await PasswordPolicyEvaluator.EvaluateAsync(
            policy, request.Password, currentCredential: null, history: [], passwordHasher, breachedPasswordChecker);
        if (evaluateResult.IsFailed) return evaluateResult;

        var registerResult = User.Register(request.TenantId, email, username);
        if (registerResult.IsFailed) return Result.Fail(registerResult.Errors);
        var user = registerResult.Value;

        var (rawToken, tokenHash) = SecureTokenGenerator.Generate();
        var verificationToken = EmailVerificationToken.Create(tokenHash, DateTimeOffset.UtcNow.AddHours(24));
        user.AddEmailVerificationToken(verificationToken);

        var credential = PasswordCredential.Create(
            passwordHasher.Hash(request.Password),
            passwordHasher.Algorithm,
            options.Value.CurrentPasswordAlgorithmVersion,
            DateTimeOffset.UtcNow);

        var setPasswordResult = user.SetPassword(credential, policy);
        if (setPasswordResult.IsFailed) return Result.Fail(setPasswordResult.Errors);

        await repository.CreateAsync(user, user.Id, cancellationToken);

        return Result.Ok(new UserRegistered(user.Id, rawToken));
    }
}
