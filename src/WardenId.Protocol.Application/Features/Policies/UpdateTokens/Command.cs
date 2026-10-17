using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Policies.UpdateTokens;

/// <summary>
/// Replaces a tenant's lifetime overrides (every field; null clears it back to the global default). Applies to
/// tokens and sessions issued afterwards only.
/// </summary>
public sealed record Command(
    Guid TenantId,
    TimeSpan? AccessTokenLifetime,
    TimeSpan? IdTokenLifetime,
    TimeSpan? RefreshTokenLifetime,
    TimeSpan? RefreshTokenAbsoluteLifetime,
    TimeSpan? AuthorizationCodeLifetime,
    TimeSpan? SessionCookieLifetime,
    TimeSpan? SessionAbsoluteLifetime,
    Guid ActorId) : IRequest<Result>;
