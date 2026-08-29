using FluentResults;

namespace WardenId.Protocol.Application.Abstractions;

public interface IAuthenticateUsers
{
    Task<Result<AuthenticatedUserInfo>> AuthenticateAsync(
        Guid tenantId, string emailOrUsername, string password, CancellationToken cancellationToken);
}

public sealed record AuthenticatedUserInfo(
    Guid UserId,
    string Email,
    bool EmailVerified,
    string Username,
    bool IsActive,
    bool HasPendingActions);
