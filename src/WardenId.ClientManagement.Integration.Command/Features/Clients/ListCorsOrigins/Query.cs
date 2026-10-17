using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Integration.Command.Features.Clients.ListCorsOrigins;

/// <summary>
/// The distinct CORS origins of every enabled, non-deleted client of the tenant, ordinal order. The Gateway allows
/// these origins on the tenant's browser-facing protocol endpoints.
/// </summary>
public sealed record Query(Guid TenantId) : IRequest<Result<IReadOnlyList<string>>>;
