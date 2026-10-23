using WardenId.SharedKernel.Domain;

namespace WardenId.Audit.Infrastructure.Tests.Fakes;

internal sealed class TestAggregate : AggregateRoot, IAuditableAggregate, ICreationTracked, ISoftDeletable
{
    public string Name { get; private set; }
    public string Secret { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private TestAggregate(string name, string secret)
    {
        Name = name;
        Secret = secret;
    }

    public static TestAggregate Create(string name, string secret = "") => new(name, secret);

    public void Rename(string name) => Name = name;
    public void SetSecret(string secret) => Secret = secret;

    public void Delete()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }

    public string AuditTargetTypeKey => "tenant";
    public AuditableChannel AuditChannel => AuditableChannel.AdminDirectory;

    public IReadOnlySet<string> AuditSensitivePropertyNames { get; } = new HashSet<string> { nameof(Secret) };
}

/// <summary>Declares a target-type key that doesn't exist in AuditTargetType's catalog — proves the interceptor fails loudly rather than silently skipping the audit.</summary>
internal sealed class BadTargetTypeAggregate : AggregateRoot, IAuditableAggregate
{
    public string Name { get; private set; }

    private BadTargetTypeAggregate(string name) => Name = name;

    public static BadTargetTypeAggregate Create(string name) => new(name);

    public string AuditTargetTypeKey => "no-such-target";
    public AuditableChannel AuditChannel => AuditableChannel.AdminDirectory;
    public IReadOnlySet<string> AuditSensitivePropertyNames { get; } = new HashSet<string>();
}
