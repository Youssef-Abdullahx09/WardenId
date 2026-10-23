namespace WardenId.ClientManagement.Application.Features.Clients;

/// <summary>Maps the Dapper query layer's raw columns onto the API's types.</summary>
internal static class ClientRows
{
    // Npgsql materializes timestamptz as DateTime with Kind=Utc.
    public static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    public static DateTimeOffset? AsUtc(DateTime? value) => value is { } v ? AsUtc(v) : null;

    // Enums are persisted by member name (flags enums as "A, B"), exactly what Enum.Parse reads back; EF's string
    // converter would fail on an unknown name too.
    public static TEnum ParseEnum<TEnum>(string value) where TEnum : struct, Enum => Enum.Parse<TEnum>(value);
}
