using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Add;

public sealed record Command(
    Guid TenantId,
    Guid ClientId,
    string Uri,
    Guid ActorId) : IRequest<Result>;
