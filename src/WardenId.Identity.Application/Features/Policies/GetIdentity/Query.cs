using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Policies.GetIdentity;

/// <summary>
/// The identity policy in effect for a tenant. Policies are global today (IdentityPolicy config section),
/// so every tenant gets the same values; TenantId is accepted for the per-tenant policies to come.
/// </summary>
public sealed record Query(Guid TenantId) : IRequest<Result<IdentityPolicy>>;
