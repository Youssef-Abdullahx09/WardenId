namespace WardenId.Identity.Query.Contracts.Features.Users.GetAccount;

/// <summary>
/// The user row of Features/Users/GetAccount/Query.sql, with its enabled factors attached. Times are UTC
/// <see cref="DateTime"/>.
/// </summary>
public sealed class Item
{
    public string Email { get; init; } = "";
    public string Username { get; init; } = "";
    public DateTime? TermsAcceptedAtUtc { get; init; }

    /// <summary>Recovery codes not used yet.</summary>
    public int RemainingRecoveryCodeCount { get; init; }

    /// <summary>Only enabled factors (pending enrollments left out), oldest enrollment first.</summary>
    public Factor[] EnabledFactors { get; set; } = [];
}

public sealed class Factor
{
    public Guid Id { get; init; }

    /// <summary>The <c>MfaFactorType</c> member name, as persisted.</summary>
    public string FactorType { get; init; } = "";

    public DateTime EnrolledAtUtc { get; init; }
    public DateTime? LastUsedAtUtc { get; init; }
}
