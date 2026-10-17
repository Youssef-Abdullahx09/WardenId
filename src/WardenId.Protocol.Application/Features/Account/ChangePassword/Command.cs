using FluentResults;
using MediatR;
using WardenId.Protocol.Application.Auditing;

namespace WardenId.Protocol.Application.Features.Account.ChangePassword;

/// <summary>
/// The account page's password change. Failures carry messages for the user (wrong current password, policy).
/// On success Identity ends the user's sessions.
/// </summary>
/// <param name="Sid">The login session the user is on, for the audit trail.</param>
public sealed record Command(Guid TenantId, Guid UserId, Guid Sid, string CurrentPassword, string NewPassword)
    : IRequest<Result>, IActsAsUser;
