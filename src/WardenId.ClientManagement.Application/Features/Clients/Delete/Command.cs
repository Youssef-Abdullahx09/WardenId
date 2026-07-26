using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Application.Features.Clients.Delete;

public sealed record Command(
    Guid TenantId,
    Guid ClientId) : IRequest<Result>;
