using FluentResults;

namespace WardenId.Protocol.Domain.TokenPolicies;

public static class TokenPolicyErrors
{
    // Messages start with the request field name (camelCase, as on the wire) so a client can point at it.
    public static Error OutOfBounds(string field, string bounds, string why)
        => new Error($"{field} must be {bounds}: {why}")
            .WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.Validation);

    public static Error ShorterThan(string field, TimeSpan value, string otherField, TimeSpan other, string why)
        => new Error($"{field} ({Format(value)}) must be at least {otherField} ({Format(other)}): {why}")
            .WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.Validation);

    public static Error LongerThan(string field, TimeSpan value, string limitName, TimeSpan limit, string why)
        => new Error($"{field} ({Format(value)}) must not exceed {limitName} ({Format(limit)}): {why}")
            .WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.Validation);

    private static string Format(TimeSpan value) => System.Xml.XmlConvert.ToString(value);
}
