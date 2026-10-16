using FluentResults;
using WardenId.SharedKernel.Domain;
using WardenId.Tenancy.Domain.Events;

namespace WardenId.Tenancy.Domain;

public sealed class Tenant : AggregateRoot
{
    private Tenant(
        Slug slug,
        string displayName)
    {
        Slug = slug;
        DisplayName = displayName;
        Status = Status.Provisioning;
    }

    public string DisplayName { get; private set; }
    public Slug Slug { get; private set; }
    public Status Status { get; private set; }
    public DateTimeOffset? SuspendedAtUtc { get; private set; }
    public string? SuspendedReason { get; private set; }
    public uint Version { get; private set; }

    private readonly List<SigningKey> _signingKeys = new();
    public IReadOnlyCollection<SigningKey> SigningKeys => _signingKeys.AsReadOnly();

    public static Result<Tenant> Create(Slug slug, string displayName)
    {
        var displayNameResult = ValidateDisplayName(displayName);
        if (displayNameResult.IsFailed) return Result.Fail(displayNameResult.Errors);

        var tenant = new Tenant(slug, displayNameResult.Value);
        tenant.RaiseDomainEvent(new TenantCreatedDomainEvent(tenant.Slug));
        return Result.Ok(tenant);
    }

    /// <summary>Changes the display name. The slug (and so the issuer) never changes.</summary>
    public Result Rename(string displayName)
    {
        if (Status == Status.Archived) return Result.Fail(TenantErrors.TenantInvalidTransition(Status));

        var displayNameResult = ValidateDisplayName(displayName);
        if (displayNameResult.IsFailed) return Result.Fail(displayNameResult.Errors);
        if (displayNameResult.Value == DisplayName) return Result.Ok();

        DisplayName = displayNameResult.Value;
        RaiseDomainEvent(new TenantRenamedDomainEvent(DisplayName));
        return Result.Ok();
    }

    private static Result<string> ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return Result.Fail(TenantErrors.TenantDisplayNameRequired());

        var trimmed = displayName.Trim();
        return trimmed.Length > 200 ? Result.Fail(TenantErrors.TenantDisplayNameTooLong()) : Result.Ok(trimmed);
    }

    /// <summary>
    /// Attaches the tenant's initial signing key. Call once, right after Create and before
    /// Activate. Key material generation and encryption at rest happen in the application/
    /// infrastructure layer before this call — the aggregate only records the result.
    /// </summary>
    public Result AssignInitialSigningKey(SigningKey signingKey)
    {
        if (_signingKeys.Count > 0) return Result.Fail(TenantErrors.TenantSigningKeyAlreadyAssigned());

        _signingKeys.Add(signingKey);
        RaiseDomainEvent(new TenantSigningKeyAssignedDomainEvent(signingKey.Id));
        return Result.Ok();
    }

    /// <summary>
    /// Provisioning -> Active. Requires an active signing key to already be assigned —
    /// enforces "signing keys mandatory" as an aggregate invariant, not caller discipline.
    /// </summary>
    public Result Activate()
    {
        if (Status != Status.Provisioning) return Result.Fail(TenantErrors.TenantInvalidTransition(Status));

        if (_signingKeys.All(k => k.IsDeleted)) return Result.Fail(TenantErrors.TenantNoActiveSigningKey());

        Status = Status.Active;
        RaiseDomainEvent(new TenantActivatedDomainEvent());
        return Result.Ok();
    }

    public Result Suspend(string reason)
    {
        if (Status != Status.Active) return Result.Fail(TenantErrors.TenantInvalidTransition(Status));

        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail(TenantErrors.TenantSuspendReasonRequired());

        Status = Status.Suspended;
        SuspendedAtUtc = DateTimeOffset.UtcNow;
        SuspendedReason = reason.Trim();
        RaiseDomainEvent(new TenantSuspendedDomainEvent(SuspendedReason));
        return Result.Ok();
    }

    public Result Reactivate()
    {
        if (Status != Status.Suspended) return Result.Fail(TenantErrors.TenantInvalidTransition(Status));

        Status = Status.Active;
        SuspendedAtUtc = null;
        SuspendedReason = null;
        RaiseDomainEvent(new TenantReactivatedDomainEvent());
        return Result.Ok();
    }

    /// <summary>Terminal state — an archived tenant can never be reactivated.</summary>
    public Result Archive()
    {
        if (Status == Status.Archived) return Result.Fail(TenantErrors.TenantAlreadyArchived());

        Status = Status.Archived;
        RaiseDomainEvent(new TenantArchivedDomainEvent());
        return Result.Ok();
    }
}