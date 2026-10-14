using FluentResults;
using MediatR;
using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Application.Features.Sessions.End;

/// <param name="RequireExisting">
/// Admin callers pass true (missing/ended ⇒ NotFound); user logout passes false so a repeated logout is a no-op.
/// </param>
public sealed record Command(Guid TenantId, Guid Sid, EndReason Reason, bool RequireExisting) : IRequest<Result>;
