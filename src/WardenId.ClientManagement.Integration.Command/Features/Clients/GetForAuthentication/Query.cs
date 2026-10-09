using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;

public sealed record Query(Guid TenantId, string ClientId) : IRequest<Result<ClientForAuthentication>>;
