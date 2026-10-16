using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Application.Features.Clients.CorsOrigins.Replace;

public sealed record Command(
    Guid TenantId,
    Guid ClientId,
    IReadOnlyCollection<string> Origins,
    Guid ActorId) : IRequest<Result>;
