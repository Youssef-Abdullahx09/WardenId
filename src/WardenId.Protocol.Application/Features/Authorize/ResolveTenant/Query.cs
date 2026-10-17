using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Authorize.ResolveTenant;

/// <summary>Slug → tenant for interactive pages outside the OpenIddict pipeline. Suspended counts as not found.</summary>
public sealed record Query(string Slug) : IRequest<Result<ResolvedTenant>>;

/// <param name="DisplayName">What people see for the tenant (authenticator apps, for one); the slug when Tenancy has no name.</param>
public sealed record ResolvedTenant(Guid TenantId, string DisplayName);
