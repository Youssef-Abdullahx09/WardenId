using WardenId.Identity.Application.Features.Users.SetAttribute;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Presentation.Features.Users.SetAttribute;

public sealed record Request(
    string Key,
    string Value,
    UserAttributeValueType ValueType)
{
    public Command ToCommand(Guid tenantId, Guid userId, Guid actorUserId) =>
        new(tenantId, userId, Key, Value, ValueType, actorUserId);
}
