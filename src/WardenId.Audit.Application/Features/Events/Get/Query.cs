using FluentResults;
using MediatR;

namespace WardenId.Audit.Application.Features.Events.Get;

public sealed record Query(Guid EventId) : IRequest<Result<AuditEventDetail>>;
