using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.RequestPasswordReset;

public sealed record Command(Guid TenantId, string Email) : IRequest<Result<PasswordResetRequested?>>;
