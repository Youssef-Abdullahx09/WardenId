using WardenId.SharedKernel.Domain;

namespace WardenId.Audit.Infrastructure.Tests.Fakes;

/// <summary>
/// An aggregate with every child shape a real one has: child entities (like a tenant's signing keys or a
/// client's secrets), an owned collection (like redirect URIs) and an owned reference (like token lifetime
/// overrides). Opts Keys and Tags into the audit diff; Notes stay out to prove the opt-in.
/// </summary>
internal sealed class ParentAggregate : AggregateRoot, IAuditableAggregate
{
    private static readonly IReadOnlySet<string> Sensitive = new HashSet<string> { "Keys.Material" };
    private static readonly IReadOnlySet<string> Navigations = new HashSet<string> { nameof(Keys), nameof(Tags), nameof(Limits) };

    private readonly List<ChildKey> _keys = [];
    private readonly List<Tag> _tags = [];
    private readonly List<ChildKey> _notes = [];

    private ParentAggregate(string name, string publicName)
    {
        Name = name;
        PublicName = publicName;
    }

    public string Name { get; private set; }
    public string PublicName { get; private set; }
    public List<Guid> ScopeIds { get; private set; } = [];
    public Limits? Limits { get; private set; }
    public IReadOnlyCollection<ChildKey> Keys => _keys.AsReadOnly();
    public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();
    public IReadOnlyCollection<ChildKey> Notes => _notes.AsReadOnly();

    public static ParentAggregate Create(string name, string publicName = "public-1") => new(name, publicName);

    public ChildKey AddKey(string name, string material)
    {
        var key = ChildKey.Create(name, material);
        _keys.Add(key);
        return key;
    }

    public void AddNote(string name) => _notes.Add(ChildKey.Create(name, "n/a"));
    public void AddTag(string value) => _tags.Add(new Tag(value));
    public void RemoveTag(string value) => _tags.RemoveAll(t => t.Value == value);
    public void SetLimits(int? max) => Limits = max is null ? null : new Limits(max.Value);
    public void SetScopeIds(List<Guid> ids) => ScopeIds = ids;

    public string AuditTargetTypeKey => "oauth_client";
    public AuditableChannel AuditChannel => AuditableChannel.AdminConfig;
    public IReadOnlySet<string> AuditSensitivePropertyNames => Sensitive;
    public IReadOnlySet<string> AuditedNavigationNames => Navigations;
    public string AuditTargetId => PublicName;
}

internal sealed class ChildKey : Entity
{
    private ChildKey(string name, string material)
    {
        Name = name;
        Material = material;
    }

    public string Name { get; private set; }
    public string Material { get; private set; }
    public bool Active { get; private set; } = true;

    public static ChildKey Create(string name, string material) => new(name, material);

    public void Deactivate() => Active = false;
}

internal sealed record Tag(string Value);

internal sealed record Limits(int Max);
