using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Authorize.AcceptTerms;

/// <summary>The terms step of sign-in: the user ticked "I accept". Not audited here; Identity records the change.</summary>
public sealed record Command(Guid TenantId, Guid UserId) : IRequest<Result>;
