using FluentResults;
using MediatR;

namespace WardenId.Identity.Integration.Command.Features.Users.ChangeOwnPassword;

/// <summary>
/// Changes the user's password after checking the current one. A wrong current password counts as a failed
/// sign-in. Policy failures come back as errors whose messages can be shown to the user.
/// </summary>
public sealed record Command(Guid TenantId, Guid UserId, string CurrentPassword, string NewPassword) : IRequest<Result>;
