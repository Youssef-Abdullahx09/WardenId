using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Application.Features.Clients.Secrets.Expire;

public sealed record Command(
    Guid TenantId,
    Guid ClientId,
    Guid SecretId,
    Guid ActorId) : IRequest<Result>;
