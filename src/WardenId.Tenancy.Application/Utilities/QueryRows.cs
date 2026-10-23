using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Application.Utilities;

/// <summary>Maps the Dapper query layer's raw columns (Tenancy.Query) onto the API's types.</summary>
internal static class QueryRows
{
    // Npgsql materializes timestamptz as DateTime with Kind=Utc.
    public static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    public static DateTimeOffset? AsUtc(DateTime? value) => value is { } v ? AsUtc(v) : null;

    // Status columns hold the enum member name (EF HasConversion<string>). An unknown name means the row was written by
    // a newer build: parsing fails loudly for detail/list reads, and Try* treats the row as ineligible for protocol reads.
    public static Status ParseStatus(string value) => Enum.Parse<Status>(value);

    public static Status? TryParseStatus(string value) =>
        Enum.TryParse<Status>(value, ignoreCase: false, out var status) ? status : null;

    public static SigningKeyStatus ParseSigningKeyStatus(string value) => Enum.Parse<SigningKeyStatus>(value);

    public static SigningKeyStatus? TryParseSigningKeyStatus(string value) =>
        Enum.TryParse<SigningKeyStatus>(value, ignoreCase: false, out var status) ? status : null;
}
