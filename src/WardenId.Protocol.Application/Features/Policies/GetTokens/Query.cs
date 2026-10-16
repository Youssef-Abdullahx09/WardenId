using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Policies.GetTokens;

/// <summary>
/// The token and session lifetimes in effect for a tenant. They are global today (ProtocolToken and
/// ProtocolSession config sections), so every tenant gets the same values; TenantId is accepted for the
/// per-tenant defaults to come. Per-client overrides are not reflected here.
/// </summary>
public sealed record Query(Guid TenantId) : IRequest<Result<TokenPolicy>>;
