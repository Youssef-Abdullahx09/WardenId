using FluentResults;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Features.Users.Register;
using WardenId.Identity.Application.Utilities;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users;

/// <summary>
/// Creates a user with a password and sends the email verification code: shared by
/// self-registration and admin creation, so both apply the same uniqueness and password rules.
/// </summary>
internal sealed class UserRegistration(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    IIdentityEmailSender emailSender)
{
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);

    /// <param name="effectivePolicy">The tenant's policy, resolved by the caller.</param>
    /// <param name="actorUserId">Who is creating the user; null for self-registration (the user themselves).</param>
    /// <param name="requiredActions">Extra steps for the first sign-in, beyond verifying the email.</param>
    public async Task<Result<UserRegistered>> RegisterAsync(
        Guid tenantId,
        EffectiveIdentityPolicy effectivePolicy,
        string rawEmail,
        string rawUsername,
        string password,
        Guid? actorUserId,
        IReadOnlyCollection<RequiredAction> requiredActions,
        CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(rawEmail);
        if (emailResult.IsFailed) return Result.Fail(emailResult.Errors);

        var usernameResult = Username.Create(rawUsername);
        if (usernameResult.IsFailed) return Result.Fail(usernameResult.Errors);

        var email = emailResult.Value;
        var username = usernameResult.Value;

        if (await repository.ExistsByEmailAsync(tenantId, email, cancellationToken))
            return Result.Fail(UserErrors.EmailAlreadyInUse());

        if (await repository.ExistsByUsernameAsync(tenantId, username, cancellationToken))
            return Result.Fail(UserErrors.UsernameAlreadyInUse());

        var policy = effectivePolicy.Password;
        var evaluateResult = await PasswordPolicyEvaluator.EvaluateAsync(
            policy, password, currentCredential: null, history: [], passwordHasher, breachedPasswordChecker);
        if (evaluateResult.IsFailed) return evaluateResult;

        var registerResult = User.Register(tenantId, email, username);
        if (registerResult.IsFailed) return Result.Fail(registerResult.Errors);

        var user = registerResult.Value;
        var (rawToken, tokenHash) = SecureTokenGenerator.Generate();
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(VerificationTokenLifetime);
        user.AddEmailVerificationToken(EmailVerificationToken.Create(tokenHash, expiresAtUtc));

        var credential = PasswordCredential.Create(
            passwordHasher.Hash(password),
            passwordHasher.Algorithm,
            effectivePolicy.CurrentPasswordAlgorithmVersion,
            DateTimeOffset.UtcNow);
        var setPasswordResult = user.SetPassword(credential, policy);
        if (setPasswordResult.IsFailed) return Result.Fail(setPasswordResult.Errors);

        foreach (var action in requiredActions)
            user.AddRequiredAction(action);

        await repository.CreateAsync(user, actorUserId ?? user.Id, cancellationToken);

        await emailSender.SendEmailVerificationAsync(email.Value, username.Value, rawToken, expiresAtUtc, cancellationToken);

        return Result.Ok(new UserRegistered(user.Id, rawToken));
    }
}
