using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Integration.Command.Features.Clients.VerifySecret;

public sealed record Query(Guid TenantId, string ClientId, string PresentedSecret) : IRequest<Result>;
