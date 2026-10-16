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
    IIdentityEmailSender emailSender,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Command, Result<UserRegistered>>
{
    private readonly UserRegistration _registration =
        new(repository, passwordHasher, breachedPasswordChecker, emailSender, options);

    public Task<Result<UserRegistered>> Handle(Command request, CancellationToken cancellationToken) =>
        _registration.RegisterAsync(
            request.TenantId, request.Email, request.Username, request.Password,
            actorUserId: null, requiredActions: [], cancellationToken);
}
