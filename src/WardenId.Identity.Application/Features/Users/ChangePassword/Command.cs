using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.ChangePassword;

public sealed record Command(Guid TenantId, Guid UserId, string CurrentPassword, string NewPassword) : IRequest<Result>;
