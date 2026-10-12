using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Authorize.ValidateSessionUser;

/// <summary>Succeeds when the session's user may still sign in; fails with invalid_grant otherwise.</summary>
public sealed record Query(Guid TenantId, Guid UserId) : IRequest<Result>;
