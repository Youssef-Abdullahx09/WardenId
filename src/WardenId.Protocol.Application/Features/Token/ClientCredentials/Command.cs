using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Token.ClientCredentials;

public sealed record Command(Guid TenantId, string Slug, string ClientId) : IRequest<Result<ClientCredentialsGrant>>;
