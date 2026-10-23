using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Features.Events;

/// <summary>Turns the primitive columns the Dapper query layer (Audit.Query) returns back into domain types.</summary>
internal static class AuditEventColumns
{
    // Npgsql materializes timestamptz as DateTime with Kind=Utc.
    public static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    // Channel is persisted by name and every row has one.
    public static AuditChannel ParseChannel(string value) => Enum.Parse<AuditChannel>(value);

    // Outcome is persisted by name; it is null on rows whose search columns couldn't be filled.
    public static AuditResult? ParseOutcome(string? value) =>
        Enum.TryParse<AuditResult>(value, out var outcome) ? outcome : null;
}
