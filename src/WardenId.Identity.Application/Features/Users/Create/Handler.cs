using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Create;

/// <summary>An admin creates a user. Same rules as self-registration; the email still has to be verified.</summary>
public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    IIdentityEmailSender emailSender,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Command, Result<UserCreated>>
{
    private readonly UserRegistration _registration =
        new(repository, passwordHasher, breachedPasswordChecker, emailSender, options);

    public async Task<Result<UserCreated>> Handle(Command request, CancellationToken cancellationToken)
    {
        var result = await _registration.RegisterAsync(
            request.TenantId,
            request.Email,
            request.Username,
            request.TemporaryPassword,
            request.ActorUserId,
            request.RequirePasswordChange ? [RequiredAction.ResetPassword] : [],
            cancellationToken);

        return result.IsSuccess ? Result.Ok(new UserCreated(result.Value.UserId)) : Result.Fail(result.Errors);
    }
}
