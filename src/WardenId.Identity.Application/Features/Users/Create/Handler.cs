using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Create;

/// <summary>
/// An admin creates a user. Same rules as self-registration (the tenant's password policy); the email still
/// has to be verified. Allowed whether or not the tenant accepts self-registration.
/// </summary>
public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    IIdentityEmailSender emailSender,
    IdentityPolicyResolver policies) : IRequestHandler<Command, Result<UserCreated>>
{
    private readonly UserRegistration _registration =
        new(repository, passwordHasher, breachedPasswordChecker, emailSender);

    public async Task<Result<UserCreated>> Handle(Command request, CancellationToken cancellationToken)
    {
        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);

        var result = await _registration.RegisterAsync(
            request.TenantId,
            policyResult.Value,
            request.Email,
            request.Username,
            request.TemporaryPassword,
            request.ActorUserId,
            request.RequirePasswordChange ? [RequiredAction.ResetPassword] : [],
            cancellationToken);

        return result.IsSuccess ? Result.Ok(new UserCreated(result.Value.UserId)) : Result.Fail(result.Errors);
    }
}
