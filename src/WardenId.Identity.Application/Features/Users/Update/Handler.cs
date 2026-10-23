using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Utilities;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Update;

/// <summary>
/// An admin changes a user's email, username and/or names. Same validation and uniqueness rules as
/// creating a user (soft-deleted users' values stay taken). A new email is unverified: the user
/// gets the VerifyEmail required action and a verification email at the new address, and the
/// tokens sent to the old address stop working. A blank name removes it. Values equal to the current ones are no
/// change.
/// </summary>
public sealed class Handler(
    IUserRepository repository,
    IIdentityEmailSender emailSender) : IRequestHandler<Command, Result>
{
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);

    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        if (user.Status is UserStatus.Disabled or UserStatus.SoftDeleted)
            return Result.Fail(UserErrors.InvalidStatusTransition());

        Email? newEmail = null;
        if (request.Email is not null)
        {
            var emailResult = Email.Create(request.Email);
            if (emailResult.IsFailed) return Result.Fail(emailResult.Errors);
            if (emailResult.Value != user.Email) newEmail = emailResult.Value;
        }

        Username? newUsername = null;
        if (request.Username is not null)
        {
            var usernameResult = Username.Create(request.Username);
            if (usernameResult.IsFailed) return Result.Fail(usernameResult.Errors);
            if (usernameResult.Value != user.Username) newUsername = usernameResult.Value;
        }

        var firstName = user.FirstName;
        if (request.FirstName is not null)
        {
            var firstNameResult = PersonName.CreateOptional(request.FirstName, PersonNameFields.FirstName);
            if (firstNameResult.IsFailed) return Result.Fail(firstNameResult.Errors);
            firstName = firstNameResult.Value;
        }

        var lastName = user.LastName;
        if (request.LastName is not null)
        {
            var lastNameResult = PersonName.CreateOptional(request.LastName, PersonNameFields.LastName);
            if (lastNameResult.IsFailed) return Result.Fail(lastNameResult.Errors);
            lastName = lastNameResult.Value;
        }

        var nameChanged = firstName != user.FirstName || lastName != user.LastName;

        if (newEmail is null && newUsername is null && !nameChanged) return Result.Ok();

        if (newEmail is not null && await repository.ExistsByEmailAsync(request.TenantId, newEmail, cancellationToken))
            return Result.Fail(UserErrors.EmailAlreadyInUse());

        if (newUsername is not null && await repository.ExistsByUsernameAsync(request.TenantId, newUsername, cancellationToken))
            return Result.Fail(UserErrors.UsernameAlreadyInUse());

        if (newUsername is not null)
        {
            var changeUsernameResult = user.ChangeUsername(newUsername);
            if (changeUsernameResult.IsFailed) return changeUsernameResult;
        }

        if (nameChanged)
        {
            var changeNameResult = user.ChangePersonName(firstName, lastName);
            if (changeNameResult.IsFailed) return changeNameResult;
        }

        string? rawToken = null;
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(VerificationTokenLifetime);
        if (newEmail is not null)
        {
            (rawToken, var tokenHash) = SecureTokenGenerator.Generate();
            var changeEmailResult = user.ChangeEmail(newEmail, EmailVerificationToken.Create(tokenHash, expiresAtUtc));
            if (changeEmailResult.IsFailed) return changeEmailResult;
        }

        var saveResult = await repository.UpdateAsync(user, request.ActorUserId, cancellationToken);
        if (saveResult.IsFailed) return Result.Fail(saveResult.Errors);

        if (rawToken is not null)
            await emailSender.SendEmailVerificationAsync(user.Email.Value, user.Username.Value, rawToken, expiresAtUtc, cancellationToken);

        return Result.Ok();
    }
}
