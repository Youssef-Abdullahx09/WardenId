using FluentResults;
using MediatR;
using WardenId.Protocol.Application.Auditing;

namespace WardenId.Protocol.Application.Features.Authorize.AcceptTerms;

/// <summary>The terms step of sign-in: the user ticked "I accept". Audited as the user's acceptance once Identity has recorded it.</summary>
public sealed record Command(Guid TenantId, Guid UserId) : IRequest<Result>, IActsAsUser;
