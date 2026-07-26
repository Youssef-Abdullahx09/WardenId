using FluentResults;
using WardenId.Identity.Domain.Events;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class User : AggregateRoot
{
    private User(Guid tenantId, Email email, Username username)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        TenantId = tenantId;
        Email = email;
        Username = username;
        Status = UserStatus.PendingVerification;
        Lockout = LoginLockoutState.None;
        _pendingActions.Add(RequiredAction.VerifyEmail);
    }

    public Guid TenantId { get; private set; }
    public Email Email { get; private set; }
    public Username Username { get; private set; }
    public UserStatus Status { get; private set; }
    public PasswordCredential? Credential { get; private set; }
    public LoginLockoutState Lockout { get; private set; }

    private readonly List<MfaFactor> _mfaFactors = new();
    public IReadOnlyCollection<MfaFactor> MfaFactors => _mfaFactors.AsReadOnly();

    private readonly List<RecoveryCode> _recoveryCodes = new();
    public IReadOnlyCollection<RecoveryCode> RecoveryCodes => _recoveryCodes.AsReadOnly();

    private readonly List<UserAttribute> _attributes = new();
    public IReadOnlyCollection<UserAttribute> Attributes => _attributes.AsReadOnly();

    private readonly List<RequiredAction> _pendingActions = new();
    public IReadOnlyCollection<RequiredAction> PendingActions => _pendingActions.AsReadOnly();

    private readonly List<PasswordHistoryEntry> _passwordHistory = new();
    public IReadOnlyCollection<PasswordHistoryEntry> PasswordHistory => _passwordHistory.AsReadOnly();

    private readonly List<EmailVerificationToken> _emailVerificationTokens = new();
    public IReadOnlyCollection<EmailVerificationToken> EmailVerificationTokens => _emailVerificationTokens.AsReadOnly();

    private readonly List<PasswordResetToken> _passwordResetTokens = new();
    public IReadOnlyCollection<PasswordResetToken> PasswordResetTokens => _passwordResetTokens.AsReadOnly();

    public static Result<User> Register(Guid tenantId, Email email, Username username)
    {
        var user = new User(tenantId, email, username);
        user.RaiseDomainEvent(new UserRegisteredDomainEvent(email, username));
        return Result.Ok(user);
    }

    public void AddEmailVerificationToken(EmailVerificationToken token) => _emailVerificationTokens.Add(token);

    public Result VerifyEmail(string tokenHash)
    {
        if (Status != UserStatus.PendingVerification) return Result.Fail(UserErrors.EmailAlreadyVerified());

        var token = _emailVerificationTokens.FirstOrDefault(t => t.Matches(tokenHash));
        if (token is null) return Result.Fail(UserErrors.TokenInvalid());

        var consumeResult = token.Consume(DateTimeOffset.UtcNow);
        if (consumeResult.IsFailed) return consumeResult;

        Status = UserStatus.Active;
        _pendingActions.Remove(RequiredAction.VerifyEmail);
        RaiseDomainEvent(new UserEmailVerifiedDomainEvent());
        return Result.Ok();
    }

    public Result SetPassword(PasswordCredential newCredential, PasswordPolicy policy)
    {
        if (Credential is not null)
        {
            _passwordHistory.Add(PasswordHistoryEntry.Create(Credential.Hash, Credential.Algorithm, Credential.SetAtUtc));

            while (_passwordHistory.Count > policy.HistoryCount)
            {
                _passwordHistory.RemoveAt(0);
            }
        }

        Credential = newCredential;
        RaiseDomainEvent(new PasswordChangedDomainEvent());
        return Result.Ok();
    }

    public Result RecordFailedLogin(int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        var failedCount = Lockout.FailedAttemptCount + 1;

        if (failedCount >= maxFailedAttempts)
        {
            var lockedUntil = DateTimeOffset.UtcNow.Add(lockoutDuration);
            Lockout = LoginLockoutState.FromTrustedSource(failedCount, lockedUntil);
            Status = UserStatus.Locked;
            RaiseDomainEvent(new LoginFailedDomainEvent(failedCount));
            RaiseDomainEvent(new AccountLockedOutDomainEvent(lockedUntil));
            return Result.Ok();
        }

        Lockout = LoginLockoutState.FromTrustedSource(failedCount, null);
        RaiseDomainEvent(new LoginFailedDomainEvent(failedCount));
        return Result.Ok();
    }

    public void RecordSuccessfulLogin() => Lockout = LoginLockoutState.None;

    public Result Unlock()
    {
        if (Status != UserStatus.Locked) return Result.Fail(UserErrors.InvalidStatusTransition());

        Lockout = LoginLockoutState.None;
        Status = UserStatus.Active;
        RaiseDomainEvent(new UserUnlockedDomainEvent());
        return Result.Ok();
    }

    public Result Disable(string reason)
    {
        if (Status == UserStatus.Disabled) return Result.Fail(UserErrors.InvalidStatusTransition());
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail(UserErrors.DisableReasonRequired());

        Status = UserStatus.Disabled;
        RaiseDomainEvent(new UserDisabledDomainEvent(reason.Trim()));
        return Result.Ok();
    }

    public Result SoftDelete()
    {
        if (Status == UserStatus.SoftDeleted) return Result.Fail(UserErrors.InvalidStatusTransition());

        Status = UserStatus.SoftDeleted;
        RaiseDomainEvent(new UserSoftDeletedDomainEvent());
        return Result.Ok();
    }

    public Result EnrollMfaFactor(MfaFactor factor, IEnumerable<RecoveryCode> recoveryCodes)
    {
        _mfaFactors.Add(factor);
        _recoveryCodes.AddRange(recoveryCodes);
        RaiseDomainEvent(new MfaFactorEnrolledDomainEvent(factor.Id, factor.FactorType));
        return Result.Ok();
    }

    public Result RevokeMfaFactor(Guid factorId, MfaPolicy policy, bool userIsAdmin)
    {
        var factor = _mfaFactors.FirstOrDefault(f => f.Id == factorId);
        if (factor is null) return Result.Fail(UserErrors.MfaFactorNotFound());

        var canRevoke = MfaPolicyEvaluator.CanRevokeFactor(policy, this, factor, userIsAdmin);
        if (canRevoke.IsFailed) return canRevoke;

        _mfaFactors.Remove(factor);
        RaiseDomainEvent(new MfaFactorRevokedDomainEvent(factorId));
        return Result.Ok();
    }
}
