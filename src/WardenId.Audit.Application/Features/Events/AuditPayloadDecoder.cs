using WardenId.Audit.Domain;

namespace WardenId.Audit.Application.Features.Events;

internal static class AuditPayloadDecoder
{
    /// <summary>The event in <paramref name="payload"/>, or null when it was purged or can't be read.</summary>
    public static AuditEvent? TryDecode(byte[]? payload)
    {
        if (payload is null) return null;
        try
        {
            return CanonicalAuditEventSerializer.Deserialize(payload);
        }
        catch (Exception)
        {
            // Unreadable: an event type this build doesn't know, or bytes that aren't a canonical event
            // (the serializer throws more than one exception type for those). Reading is best effort, so
            // one bad row must not fail the request; it's still listed and has its inclusion proof.
            return null;
        }
    }
}
