namespace WardenId.Identity.Query.Contracts.Features.Users.Get;

/// <summary>
/// The user row of Features/Users/Get/Query.sql, with its child rows attached. Times are UTC <see cref="DateTime"/>:
/// Npgsql reads timestamptz as DateTime (Kind=Utc), which Dapper maps directly.
/// </summary>
public sealed class Item
{
    public Guid Id { get; init; }
    public string Email { get; init; } = "";
    public string Username { get; init; } = "";
    public string? FirstName { get; init; }
    public string? LastName { get; init; }

    /// <summary>The <c>UserStatus</c> member name, as persisted.</summary>
    public string Status { get; init; } = "";

    public int FailedAttemptCount { get; init; }
    public DateTime? LockedUntilUtc { get; init; }

    /// <summary>The <c>RequiredAction</c> member names, as persisted, in stored order.</summary>
    public string[] PendingActions { get; init; } = [];

    public DateTime? TermsAcceptedAtUtc { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>Recovery codes not used yet.</summary>
    public int RemainingRecoveryCodeCount { get; init; }

    public UserAttribute[] Attributes { get; set; } = [];

    /// <summary>Every factor, enabled or still pending, oldest enrollment first.</summary>
    public MfaFactor[] MfaFactors { get; set; } = [];
}

public sealed class UserAttribute
{
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";

    /// <summary>The <c>UserAttributeValueType</c> member name, as persisted.</summary>
    public string ValueType { get; init; } = "";
}

public sealed class MfaFactor
{
    public Guid Id { get; init; }

    /// <summary>The <c>MfaFactorType</c> member name, as persisted.</summary>
    public string FactorType { get; init; } = "";

    public bool Enabled { get; init; }
    public DateTime EnrolledAtUtc { get; init; }
    public DateTime? VerifiedAtUtc { get; init; }
    public DateTime? LastUsedAtUtc { get; init; }
}
