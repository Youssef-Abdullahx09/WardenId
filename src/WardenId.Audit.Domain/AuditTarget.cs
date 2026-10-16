using FluentResults;

namespace WardenId.Audit.Domain;

/// <summary>One object affected by an audited action. An event carries these as an array — one action can affect several.</summary>
public sealed record AuditTarget
{
    public AuditTargetType Type { get; }
    public string Id { get; }
    public AuditCreationSource? CreatedVia { get; }

    private AuditTarget(AuditTargetType type, string id, AuditCreationSource? createdVia)
    {
        Type = type;
        Id = id;
        CreatedVia = createdVia;
    }

    public static Result<AuditTarget> Create(AuditTargetType type, string id, AuditCreationSource? createdVia = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return Result.Fail(AuditErrors.TargetIdRequired());
        return Result.Ok(new AuditTarget(type, id, createdVia));
    }

    /// <summary>"{type key}:{id}", e.g. "user:0198…" — how a target is listed and searched.</summary>
    public string ToSerializedForm() => $"{Type.Key}:{Id}";
}
