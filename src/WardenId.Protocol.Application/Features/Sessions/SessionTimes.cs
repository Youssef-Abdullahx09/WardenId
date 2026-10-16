using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Application.Features.Sessions;

/// <summary>Maps the Dapper query layer's raw columns onto the API's types.</summary>
internal static class SessionTimes
{
    // Npgsql materializes timestamptz as DateTime with Kind=Utc.
    public static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    public static DateTimeOffset? AsUtc(DateTime? value) => value is { } v ? AsUtc(v) : null;

    // EndReason is persisted by name; an unknown name (a newer build's reason) reads as null, not a failure.
    public static EndReason? ParseEndReason(string? value) =>
        Enum.TryParse<EndReason>(value, ignoreCase: false, out var reason) ? reason : null;
}
