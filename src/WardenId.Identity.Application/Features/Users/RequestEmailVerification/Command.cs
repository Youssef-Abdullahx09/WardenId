using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.RequestEmailVerification;

public sealed record Command(Guid TenantId, Guid UserId) : IRequest<Result<EmailVerificationRequested>>;
