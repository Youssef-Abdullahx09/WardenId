using FluentResults;

namespace WardenId.Protocol.Application.Features.BackchannelLogout;

/// <summary>Creates the signed OIDC Back-Channel Logout 1.0 logout_token (spec D9).</summary>
public interface ILogoutTokenFactory
{
    Task<Result<string>> CreateAsync(Guid tenantId, string clientId, Guid subjectId, Guid sid, CancellationToken cancellationToken);
}
