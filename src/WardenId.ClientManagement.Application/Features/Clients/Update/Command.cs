using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Application.Features.Clients.Update;

public sealed record Command(
    Guid TenantId,
    Guid ClientId,
    string? DisplayName,
    bool? Enabled,
    bool? ConsentRequired,
    int? AccessTokenSeconds,
    int? IdTokenSeconds,
    int? RefreshTokenSeconds,
    bool ClearTokenLifetimeOverrides) : IRequest<Result>;
