using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Application.Features.Clients.BackchannelLogoutUri.Set;

/// <summary>Sets the client's back-channel logout URI; a null <paramref name="Uri"/> clears it.</summary>
public sealed record Command(
    Guid TenantId,
    Guid ClientId,
    string? Uri,
    Guid ActorId) : IRequest<Result>;
