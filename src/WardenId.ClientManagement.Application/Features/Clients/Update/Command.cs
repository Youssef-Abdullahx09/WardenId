using FluentResults;
using MediatR;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.Update;

/// <summary>
/// Partial update: a null field is left unchanged. <paramref name="Description"/> set to an empty or
/// whitespace string clears it. ClientType, RequirePkce and the authentication method are fixed at create.
/// </summary>
public sealed record Command(
    Guid TenantId,
    Guid ClientId,
    string? DisplayName,
    string? Description,
    bool? Enabled,
    bool? IsFirstParty,
    bool? ConsentRequired,
    GrantType? GrantType,
    int? AccessTokenSeconds,
    int? IdTokenSeconds,
    int? RefreshTokenSeconds,
    bool ClearTokenLifetimeOverrides,
    Guid ActorId) : IRequest<Result>;
