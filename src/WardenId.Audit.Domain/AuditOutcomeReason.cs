using System.Text.RegularExpressions;
using WardenId.SharedKernel.Domain;

namespace WardenId.Audit.Domain;

/// <summary>
/// Single documented catalog of machine-readable outcome reason codes (OAuth-style, e.g.
/// "invalid_grant", "unknown_username"). Fastest-growing of the audit catalogs — unlike
/// AuditTargetType/AuditCreationSource this isn't tied to a small closed concept, it's
/// roughly one code per failure mode across every channel, so expect the most additions
/// here over time. Same enforcement rationale as AuditEventType: it's the type
/// AuditOutcome.Reason carries, so a new failure mode cannot be recorded until its entry
/// exists here. Key is the persisted taxonomy string; Name (inherited from Enumeration) is
/// the C# identifier. Grows alongside real capture call sites — do not pre-populate
/// speculative reasons.
/// </summary>
public sealed class AuditOutcomeReason : Enumeration<AuditOutcomeReason>
{
    private static readonly Regex KeyFormat = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    public string Key { get; }

    private AuditOutcomeReason(int value, string name, string key) : base(value, name)
    {
        if (!KeyFormat.IsMatch(key))
            throw new ArgumentException($"Audit outcome reason key must be lowercase snake_case, got '{key}'.", nameof(key));

        Key = key;
    }

    public static readonly AuditOutcomeReason InvalidGrant = new(1, nameof(InvalidGrant), "invalid_grant");

    public static readonly AuditOutcomeReason UnknownUsername = new(2, nameof(UnknownUsername), "unknown_username");

    public static readonly AuditOutcomeReason PolicyDenied = new(3, nameof(PolicyDenied), "policy_denied");

    public static readonly IReadOnlyList<AuditOutcomeReason> All =
    [
        InvalidGrant,
        UnknownUsername,
        PolicyDenied
    ];

    /// <summary>Resolves a catalog entry from its persisted taxonomy string — canonical deserialization only.</summary>
    public static AuditOutcomeReason? FromKey(string key) => All.FirstOrDefault(r => r.Key == key);
}
