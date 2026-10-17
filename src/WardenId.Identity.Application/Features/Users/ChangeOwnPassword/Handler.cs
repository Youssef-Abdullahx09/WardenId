using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;
using Command = WardenId.Identity.Integration.Command.Features.Users.ChangeOwnPassword.Command;

namespace WardenId.Identity.Application.Features.Users.ChangeOwnPassword;

/// <summary>
/// The account page's password change, the same as the admin API's (see <see cref="PasswordChange"/>). Like every
/// password change it raises PasswordChangedDomainEvent, which ends the user's sessions.
/// </summary>
public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    IdentityPolicyResolver policies) : IRequestHandler<Command, Result>
{
    public Task<Result> Handle(Command request, CancellationToken cancellationToken) =>
        new PasswordChange(repository, passwordHasher, breachedPasswordChecker, policies)
            .ChangeAsync(request.TenantId, request.UserId, request.CurrentPassword, request.NewPassword, cancellationToken);
}
