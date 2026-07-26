using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Application.Features.Clients.Get;

public sealed record Query(
    Guid TenantId,
    Guid ClientId) : IRequest<Result<ClientDetail>>;
