using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Account.ChangePassword;

/// <summary>
/// The account page's password change. Failures carry messages for the user (wrong current password, policy).
/// On success Identity ends the user's sessions.
/// </summary>
public sealed record Command(Guid TenantId, Guid UserId, string CurrentPassword, string NewPassword) : IRequest<Result>;
