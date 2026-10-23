using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Create;

/// <param name="TemporaryPassword">Set by the admin; checked against the tenant's password policy.</param>
/// <param name="FirstName">Optional; null or blank for none.</param>
/// <param name="LastName">Optional; null or blank for none.</param>
/// <param name="RequirePasswordChange">Adds the ResetPassword required action, so the user picks their own.</param>
public sealed record Command(
    Guid TenantId,
    string Email,
    string Username,
    string? FirstName,
    string? LastName,
    string TemporaryPassword,
    bool RequirePasswordChange,
    Guid ActorUserId) : IRequest<Result<UserCreated>>;
