using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Policies.GetTokens;

/// <summary>
/// The token and session lifetimes in effect for a tenant: its own overrides where set, the global ProtocolToken /
/// ProtocolSession configuration otherwise. Per-client lifetime overrides (which win at issuance) are not reflected.
/// The tenant is not checked for existence.
/// </summary>
public sealed record Query(Guid TenantId) : IRequest<Result<TokenPolicy>>;
