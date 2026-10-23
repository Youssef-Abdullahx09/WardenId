using System.Text;
using WardenId.Audit.Domain.Utilities;

namespace WardenId.Audit.Domain;

/// <summary>
/// The statement the outgoing checkpoint key signs when it hands over to a new key (spec §6: "the first
/// checkpoint under a new key must be verifiable from the old one"). It names both kids and the new key's
/// public point, so a verifier who trusts the old key can trust the new one, and a key that appears without
/// such a statement is a substitution, not a rotation. Signed-note style, like checkpoints, so the same
/// witnesses can keep it. The point's coordinates are signed rather than the JWK text, because the JWK is
/// stored as jsonb and Postgres does not keep its formatting.
/// </summary>
public static class CheckpointKeyRotation
{
    /// <summary>The bytes the outgoing key signs.</summary>
    public static byte[] BuildUnsignedBody(string previousKid, string newKid, string newKeyX, string newKeyY)
        => Encoding.UTF8.GetBytes(NoteHeader(previousKid, newKid, newKeyX, newKeyY));

    /// <summary>The full signed note, as handed to checkpoint witnesses.</summary>
    public static string ToSignedNoteText(string previousKid, string newKid, string newKeyX, string newKeyY, byte[] signature)
        => NoteHeader(previousKid, newKid, newKeyX, newKeyY) + $"\n— {previousKid} {Convert.ToBase64String(signature)}\n";

    private static string NoteHeader(string previousKid, string newKid, string newKeyX, string newKeyY)
        => $"{AuditFormats.SignedNotes.KeyRotationOrigin}\n{previousKid}\n{newKid}\n{newKeyX}\n{newKeyY}\n";
}
