using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Sessions.Start;

/// <param name="Amr">How the user signed in (<see cref="Authorize.AuthenticationMethods"/>); null ⇒ password only.</param>
public sealed record Command(Guid TenantId, Guid UserId, IReadOnlyList<string>? Amr = null) : IRequest<Result<StartedSession>>;
