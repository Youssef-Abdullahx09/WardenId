using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Jwks.Get;

public sealed record Query(string Slug) : IRequest<Result<JsonWebKeySetDocument>>;
