using FluentResults;
using WardenId.Identity.Domain.Events;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class User : AggregateRoot, IAuditableAggregate
{
    public string AuditTargetTypeKey => "user";
    public AuditableChannel AuditChannel => AuditableChannel.AdminDirectory;

    // Credential/MFA secrets live on child entities the generic interceptor never diffs
    // (see AuditCaptureInterceptor's docstring) — none of User's own scalar properties carry
    // secret material, so nothing needs redaction here.
    public IReadOnlySet<string> AuditSensitivePropertyNames { get; } = new HashSet<string>();

    private User(Guid tenantId, Email email, Username username)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        TenantId = tenantId;
        Email = email;
        Username = username;
        Status = UserStatus.PendingVerification;
        Lockout = LoginLockoutState.None;
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
        user._pendingActions.Add(RequiredAction.VerifyEmail);
        user.RaiseDomainEvent(new UserRegisteredDomainEvent(email, username));
        return Result.Ok(user);
    }

    private Result EnsureMutable()
    {
        return Status is UserStatus.Disabled or UserStatus.SoftDeleted
            ? Result.Fail(UserErrors.InvalidStatusTransition())
            : Result.Ok();
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
        var ensureMutable = EnsureMutable();
        if (ensureMutable.IsFailed) return ensureMutable;

        if (Credential is not null)
        {
            _passwordHistory.Add(PasswordHistoryEntry.Create(Credential.Hash, Credential.Algorithm, Credential.SetAtUtc));

            while (_passwordHistory.Count > policy.HistoryCount)
            {
                _passwordHistory.RemoveAt(0);
            }
        }

        Credential = newCredential;
        // A new password is exactly what the ResetPassword action asks for, so it is done.
        _pendingActions.Remove(RequiredAction.ResetPassword);
        RaiseDomainEvent(new PasswordChangedDomainEvent());
        return Result.Ok();
    }

    public Result RecordFailedLogin(int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        var ensureMutable = EnsureMutable();
        if (ensureMutable.IsFailed) return ensureMutable;

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
        if (Status is UserStatus.Disabled or UserStatus.SoftDeleted) return Result.Fail(UserErrors.InvalidStatusTransition());
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail(UserErrors.DisableReasonRequired());

        Status = UserStatus.Disabled;
        RaiseDomainEvent(new UserDisabledDomainEvent(reason.Trim()));
        return Result.Ok();
    }

    /// <summary>
    /// Disabled → the status the user would have without the disable: PendingVerification while
    /// the email has never been verified (no verification token was ever consumed), Active
    /// otherwise. A lockout from before the disable is cleared, since an admin enabling the
    /// account means it should be usable now. Pending required actions are kept.
    /// </summary>
    public Result Enable()
    {
        if (Status != UserStatus.Disabled) return Result.Fail(UserErrors.InvalidStatusTransition());

        Lockout = LoginLockoutState.None;
        Status = IsEmailVerified ? UserStatus.Active : UserStatus.PendingVerification;
        RaiseDomainEvent(new UserEnabledDomainEvent());
        return Result.Ok();
    }

    public Result SoftDelete()
    {
        if (Status == UserStatus.SoftDeleted) return Result.Fail(UserErrors.InvalidStatusTransition());

        Status = UserStatus.SoftDeleted;
        RaiseDomainEvent(new UserSoftDeletedDomainEvent());
        return Result.Ok();
    }

    /// <summary>
    /// SoftDeleted → Disabled. The status before the delete isn't kept, and a restored account
    /// should not be able to sign in again until an admin decides so: they call <see cref="Enable"/>.
    /// </summary>
    public Result Restore()
    {
        if (Status != UserStatus.SoftDeleted) return Result.Fail(UserErrors.InvalidStatusTransition());

        Status = UserStatus.Disabled;
        RaiseDomainEvent(new UserRestoredDomainEvent());
        return Result.Ok();
    }

    private bool IsEmailVerified => _emailVerificationTokens.Any(t => t.ConsumedAtUtc is not null);

    public Result EnrollMfaFactor(MfaFactor factor, IEnumerable<RecoveryCode> recoveryCodes)
    {
        var ensureMutable = EnsureMutable();
        if (ensureMutable.IsFailed) return ensureMutable;

        _mfaFactors.Add(factor);
        _recoveryCodes.AddRange(recoveryCodes);
        RaiseDomainEvent(new MfaFactorEnrolledDomainEvent(factor.Id, factor.FactorType));
        return Result.Ok();
    }

    public Result VerifyMfaFactor(Guid factorId)
    {
        var factor = _mfaFactors.FirstOrDefault(f => f.Id == factorId);
        if (factor is null) return Result.Fail(UserErrors.MfaFactorNotFound());

        return factor.Verify();
    }

    public Result ConsumeRecoveryCode(string codeHash)
    {
        var code = _recoveryCodes.FirstOrDefault(c => c.Matches(codeHash));
        if (code is null) return Result.Fail(UserErrors.RecoveryCodeInvalid());

        return code.Consume();
    }

    public Result RehashCredential(string newHash, int newAlgorithmVersion)
    {
        if (Credential is null) return Result.Fail(UserErrors.InvalidStatusTransition());

        Credential.Rehash(newHash, newAlgorithmVersion, DateTimeOffset.UtcNow);
        return Result.Ok();
    }

    public Result RevokeMfaFactor(Guid factorId, MfaPolicy policy, bool userIsAdmin)
    {
        var factor = _mfaFactors.FirstOrDefault(f => f.Id == factorId);
        if (factor is null) return Result.Fail(UserErrors.MfaFactorNotFound());

        var canRevoke = MfaPolicyEvaluator.CanRevokeFactor(policy, this, factor, userIsAdmin);
        if (canRevoke.IsFailed) return canRevoke;

        _mfaFactors.Remove(factor);
        if (_mfaFactors.All(f => !f.Enabled)) _recoveryCodes.Clear();
        RaiseDomainEvent(new MfaFactorRevokedDomainEvent(factorId));
        return Result.Ok();
    }

    public void RequestPasswordReset(PasswordResetToken token)
    {
        _passwordResetTokens.Add(token);
        RaiseDomainEvent(new PasswordResetRequestedDomainEvent());
    }

    public Result ResetPassword(string tokenHash, PasswordCredential newCredential, PasswordPolicy policy)
    {
        var ensureMutable = EnsureMutable();
        if (ensureMutable.IsFailed) return ensureMutable;

        var token = _passwordResetTokens.FirstOrDefault(t => t.Matches(tokenHash));
        if (token is null) return Result.Fail(UserErrors.TokenInvalid());

        var consumeResult = token.Consume(DateTimeOffset.UtcNow);
        if (consumeResult.IsFailed) return consumeResult;

        SetPassword(newCredential, policy);
        Lockout = LoginLockoutState.None;
        if (Status == UserStatus.Locked) Status = UserStatus.Active;

        RaiseDomainEvent(new PasswordResetDomainEvent());
        return Result.Ok();
    }

    public Result SetAttribute(string key, string value, UserAttributeValueType valueType)
    {
        var trimmedKey = key.Trim();
        var existing = _attributes.FirstOrDefault(a => a.Key == trimmedKey);
        if (existing is not null)
        {
            existing.UpdateValue(value, valueType);
            return Result.Ok();
        }

        var createResult = UserAttribute.Create(trimmedKey, value, valueType);
        if (createResult.IsFailed) return createResult.ToResult();

        _attributes.Add(createResult.Value);
        return Result.Ok();
    }

    public void AddRequiredAction(RequiredAction action)
    {
        if (!_pendingActions.Contains(action)) _pendingActions.Add(action);
    }

    public void ClearRequiredAction(RequiredAction action) => _pendingActions.Remove(action);
}
