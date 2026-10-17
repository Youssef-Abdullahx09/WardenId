using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.ChangePassword;

/// <summary>See <see cref="PasswordChange"/>: a wrong current password counts as a failed login.</summary>
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
