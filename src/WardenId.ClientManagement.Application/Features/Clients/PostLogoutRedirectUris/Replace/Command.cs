using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Application.Features.Clients.PostLogoutRedirectUris.Replace;

public sealed record Command(
    Guid TenantId,
    Guid ClientId,
    IReadOnlyCollection<string> Uris,
    Guid ActorId) : IRequest<Result>;
