using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Application.Features.Clients.RedirectUris.Remove;

public sealed record Command(
    Guid TenantId,
    Guid ClientId,
    string Uri) : IRequest<Result>;
