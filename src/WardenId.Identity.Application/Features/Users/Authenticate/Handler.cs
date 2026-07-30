using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Authenticate;

public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Command, Result<AuthenticatedUser>>
{
    public async Task<Result<AuthenticatedUser>> Handle(Command request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.EmailOrUsername);

        var userResult = emailResult.IsSuccess
            ? await repository.GetByEmailAsync(request.TenantId, emailResult.Value, cancellationToken)
            : await LookupByUsernameAsync(request, cancellationToken);

        if (userResult.IsFailed) return Result.Fail(UserErrors.InvalidCredentials());

        var user = userResult.Value;

        if (user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } lockedUntil && lockedUntil > DateTimeOffset.UtcNow)
            return Result.Fail(UserErrors.AccountLocked(lockedUntil));

        if (user.Credential is null || !passwordHasher.Verify(request.Password, user.Credential.Hash))
        {
            var failResult = user.RecordFailedLogin(options.Value.MaxFailedLoginAttempts, options.Value.LockoutDuration);
            if (failResult.IsFailed) return Result.Fail(failResult.Errors);

            await repository.UpdateAsync(user, user.Id, cancellationToken);

            return user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } newLockedUntil
                ? Result.Fail(UserErrors.AccountLocked(newLockedUntil))
                : Result.Fail(UserErrors.InvalidCredentials());
        }

        if (user.Status == UserStatus.Locked)
        {
            var unlockResult = user.Unlock();
            if (unlockResult.IsFailed) return Result.Fail(unlockResult.Errors);
        }
        else
        {
            user.RecordSuccessfulLogin();
        }

        if (user.Credential.NeedsRehash(options.Value.CurrentPasswordAlgorithmVersion))
            user.RehashCredential(passwordHasher.Hash(request.Password), options.Value.CurrentPasswordAlgorithmVersion);

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok(new AuthenticatedUser(user.Id, user.Status, user.PendingActions));
    }

    private async Task<Result<User>> LookupByUsernameAsync(Command request, CancellationToken cancellationToken)
    {
        var usernameResult = Username.Create(request.EmailOrUsername);
        if (usernameResult.IsFailed) return Result.Fail<User>(UserErrors.InvalidCredentials());

        return await repository.GetByUsernameAsync(request.TenantId, usernameResult.Value, cancellationToken);
    }
}
