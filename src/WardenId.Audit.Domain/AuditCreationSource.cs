using System.Text.RegularExpressions;
using WardenId.Audit.Domain.Utilities;
using WardenId.SharedKernel.Domain;

namespace WardenId.Audit.Domain;

/// <summary>
/// Single documented catalog of how a target came to exist — a small, cross-cutting
/// vocabulary (unlike AuditTargetType, it isn't tied to any one aggregate), used to record
/// honestly that a target has no human behind its creation, e.g. Dynamic Client Registration
/// (spec §3.1) — never fabricate a synthetic admin actor instead. Key is the persisted
/// taxonomy string; Name (inherited from Enumeration) is the C# identifier. Grows alongside
/// real capture call sites — do not pre-populate speculative sources.
/// </summary>
public sealed class AuditCreationSource : Enumeration<AuditCreationSource>
{
    private static readonly Regex KeyFormat = new(AuditFormats.KeyPatterns.SnakeCase, RegexOptions.Compiled);

    public string Key { get; }

    private AuditCreationSource(int value, string name, string key) : base(value, name)
    {
        if (!KeyFormat.IsMatch(key))
            throw new ArgumentException($"Audit creation source key must be lowercase snake_case, got '{key}'.", nameof(key));

        Key = key;
    }

    public static readonly AuditCreationSource Dcr = new(1, nameof(Dcr), "dcr");

    public static readonly IReadOnlyList<AuditCreationSource> All =
    [
        Dcr
    ];

    /// <summary>Resolves a catalog entry from its persisted taxonomy string — canonical deserialization only.</summary>
    public static AuditCreationSource? FromKey(string key) => All.FirstOrDefault(s => s.Key == key);
}
