using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Sessions.RecordAuthorization;

public sealed record Command(
    Guid TenantId,
    Guid Sid,
    string AuthorizationId,
    string ClientId,
    bool OfflineAccess) : IRequest<Result>;
