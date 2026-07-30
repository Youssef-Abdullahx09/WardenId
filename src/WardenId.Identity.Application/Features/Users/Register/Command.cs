using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Register;

public sealed record Command(
    Guid TenantId,
    string Email,
    string Username,
    string Password) : IRequest<Result<UserRegistered>>;
