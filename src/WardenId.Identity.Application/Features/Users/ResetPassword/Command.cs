using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.ResetPassword;

public sealed record Command(Guid TenantId, string Email, string RawToken, string NewPassword) : IRequest<Result>;
