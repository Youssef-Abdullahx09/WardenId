using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Application.Features.Clients.Secrets.Issue;

public sealed record Command(
    Guid TenantId,
    Guid ClientId,
    string Label,
    int? OverlapDays,
    Guid ActorId) : IRequest<Result<ClientSecretIssued>>;
