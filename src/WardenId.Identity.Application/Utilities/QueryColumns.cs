namespace WardenId.Identity.Application.Utilities;

/// <summary>Maps the Dapper query layer's raw columns (Identity.Query) onto the domain's types.</summary>
internal static class QueryColumns
{
    // Npgsql materializes timestamptz as DateTime with Kind=Utc.
    public static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    public static DateTimeOffset? AsUtc(DateTime? value) => value is { } v ? AsUtc(v) : null;

    // Enums are persisted by member name (EF's HasConversion<string>); parsed the way EF's converter did.
    public static TEnum ParseEnum<TEnum>(string value) where TEnum : struct, Enum => Enum.Parse<TEnum>(value);

    public static TEnum? ParseOptionalEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        value is null ? null : Enum.Parse<TEnum>(value);
}
