using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Register;

/// <param name="FirstName">Optional; null or blank for none.</param>
/// <param name="LastName">Optional; null or blank for none.</param>
public sealed record Command(
    Guid TenantId,
    string Email,
    string Username,
    string? FirstName,
    string? LastName,
    string Password) : IRequest<Result<UserRegistered>>;
