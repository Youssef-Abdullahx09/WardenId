using MediatR;

namespace WardenId.Protocol.Application.Features.Cors.ListAllowedOrigins;

/// <summary>
/// The origins allowed to call the tenant's browser-facing protocol endpoints cross-origin: the union of the CORS
/// origins of the tenant's enabled clients. Empty (nothing allowed) for an unknown, malformed or suspended tenant.
/// </summary>
public sealed record Query(string Slug) : IRequest<IReadOnlyList<string>>;
