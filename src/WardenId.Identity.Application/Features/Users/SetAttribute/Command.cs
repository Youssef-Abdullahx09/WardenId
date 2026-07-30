using FluentResults;
using MediatR;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.SetAttribute;

public sealed record Command(
    Guid TenantId,
    Guid UserId,
    string Key,
    string Value,
    UserAttributeValueType ValueType,
    Guid ActorUserId) : IRequest<Result>;
