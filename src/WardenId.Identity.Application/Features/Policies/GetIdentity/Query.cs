using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Policies.GetIdentity;

/// <summary>
/// The identity policy in effect for a tenant: its stored overrides over the global IdentityPolicy
/// config section. The tenant id isn't checked for existence (an unknown tenant gets the global values).
/// </summary>
public sealed record Query(Guid TenantId) : IRequest<Result<IdentityPolicy>>;
