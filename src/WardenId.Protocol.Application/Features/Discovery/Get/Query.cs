using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Discovery.Get;

public sealed record Query(string Slug) : IRequest<Result<DiscoveryDocument>>;
