using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Register;

public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    IIdentityEmailSender emailSender,
    IdentityPolicyResolver policies) : IRequestHandler<Command, Result<UserRegistered>>
{
    private readonly UserRegistration _registration =
        new(repository, passwordHasher, breachedPasswordChecker, emailSender);

    public async Task<Result<UserRegistered>> Handle(Command request, CancellationToken cancellationToken)
    {
        var policyResult = await policies.ResolveAsync(request.TenantId, cancellationToken);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);

        // Checked first, so a tenant that's closed to sign-ups doesn't reveal which emails are taken.
        if (!policyResult.Value.SelfRegistrationEnabled) return Result.Fail(UserErrors.SelfRegistrationDisabled());

        return await _registration.RegisterAsync(
            request.TenantId, policyResult.Value, request.Email, request.Username, request.Password,
            actorUserId: null, requiredActions: [], cancellationToken);
    }
}
