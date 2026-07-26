using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class UserAttribute : Entity
{
    // EF Core materialization only
    private UserAttribute() { }

    private UserAttribute(string key, string value, UserAttributeValueType valueType)
    {
        Key = key;
        Value = value;
        ValueType = valueType;
    }

    public string Key { get; private set; }
    public string Value { get; private set; }
    public UserAttributeValueType ValueType { get; private set; }

    public static Result<UserAttribute> Create(string key, string value, UserAttributeValueType valueType)
    {
        if (string.IsNullOrWhiteSpace(key)) return Result.Fail(UserErrors.AttributeKeyRequired());

        return Result.Ok(new UserAttribute(key.Trim(), value, valueType));
    }

    public void UpdateValue(string value, UserAttributeValueType valueType)
    {
        Value = value;
        ValueType = valueType;
    }
}
