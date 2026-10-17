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

    /// <summary>
    /// Verifies the current email. A new user (PendingVerification) becomes Active; a user who
    /// already got past that (Active or Locked) but has the VerifyEmail action — their email was
    /// changed, or an admin asked for it — keeps their status and loses the action.
    /// </summary>
    public Result VerifyEmail(string tokenHash)
    {
        if (Status is UserStatus.Disabled or UserStatus.SoftDeleted) return Result.Fail(UserErrors.InvalidStatusTransition());
        if (Status != UserStatus.PendingVerification && !_pendingActions.Contains(RequiredAction.VerifyEmail))
            return Result.Fail(UserErrors.EmailAlreadyVerified());

        var token = _emailVerificationTokens.FirstOrDefault(t => t.Matches(tokenHash));
        if (token is null) return Result.Fail(UserErrors.TokenInvalid());

        var consumeResult = token.Consume(DateTimeOffset.UtcNow);
        if (consumeResult.IsFailed) return consumeResult;

        if (Status == UserStatus.PendingVerification) Status = UserStatus.Active;
        _pendingActions.Remove(RequiredAction.VerifyEmail);
        RaiseDomainEvent(new UserEmailVerifiedDomainEvent());
        return Result.Ok();
    }

    /// <summary>
    /// Sets a new email, which has to be verified with <paramref name="verificationToken"/>
    /// (sent to the new address). The status is kept — an Active user stays Active — and the
    /// VerifyEmail required action blocks sign-in and token issuance until then. Every email
    /// verification and password reset token issued so far went to the old address, so they
    /// are all dropped: none can be used any more, and since a consumed verification token is
    /// what marks the email verified, the new one counts as unverified (see <see cref="Enable"/>).
    /// The same email (after normalization) changes nothing.
    /// </summary>
    public Result ChangeEmail(Email newEmail, EmailVerificationToken verificationToken)
    {
        var ensureMutable = EnsureMutable();
        if (ensureMutable.IsFailed) return ensureMutable;
        if (newEmail == Email) return Result.Ok();

        var previousEmail = Email;
        Email = newEmail;
        _emailVerificationTokens.Clear();
        _passwordResetTokens.Clear();
        _emailVerificationTokens.Add(verificationToken);
        AddRequiredAction(RequiredAction.VerifyEmail);
        RaiseDomainEvent(new UserEmailChangedDomainEvent(previousEmail, newEmail));
        return Result.Ok();
    }

    /// <summary>Sets a new username. Nothing to verify; the same username changes nothing.</summary>
    public Result ChangeUsername(Username newUsername)
    {
        var ensureMutable = EnsureMutable();
        if (ensureMutable.IsFailed) return ensureMutable;
        if (newUsername == Username) return Result.Ok();

        var previousUsername = Username;
        Username = newUsername;
        RaiseDomainEvent(new UserUsernameChangedDomainEvent(previousUsername, newUsername));
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

    /// <summary>Whether the user has an enabled MFA factor, so sign-in needs a code step.</summary>
    public bool HasEnabledMfaFactor => _mfaFactors.Any(f => f.Enabled);

    public int RemainingRecoveryCodeCount => _recoveryCodes.Count(c => !c.IsUsed);

    /// <summary>
    /// Adds an unverified factor, replacing any other unverified factor of the same type: only the latest
    /// enrollment can be completed. Enabled factors are kept.
    /// </summary>
    public Result BeginMfaEnrollment(MfaFactor factor)
    {
        var ensureMutable = EnsureMutable();
        if (ensureMutable.IsFailed) return ensureMutable;

        _mfaFactors.RemoveAll(f => f.IsPendingVerification && f.FactorType == factor.FactorType);
        _mfaFactors.Add(factor);
        return Result.Ok();
    }

    /// <summary>
    /// Enables a factor started by <see cref="BeginMfaEnrollment"/> with its first code (for
    /// <paramref name="timeStep"/>), clears the EnrollMfa required action and replaces every recovery code with
    /// <paramref name="recoveryCodes"/>: codes issued before stop working.
    /// </summary>
    public Result CompleteMfaEnrollment(
        Guid factorId, long timeStep, IEnumerable<RecoveryCode> recoveryCodes, DateTimeOffset nowUtc)
    {
        var ensureMutable = EnsureMutable();
        if (ensureMutable.IsFailed) return ensureMutable;

        var factor = _mfaFactors.FirstOrDefault(f => f.Id == factorId);
        if (factor is null) return Result.Fail(UserErrors.MfaFactorNotFound());

        var verifyResult = factor.Verify(timeStep, nowUtc);
        if (verifyResult.IsFailed) return verifyResult;

        _recoveryCodes.Clear();
        _recoveryCodes.AddRange(recoveryCodes);
        _pendingActions.Remove(RequiredAction.EnrollMfa);
        RaiseDomainEvent(new MfaFactorEnrolledDomainEvent(factor.Id, factor.FactorType));
        return Result.Ok();
    }

    /// <summary>Records a TOTP code of an enabled factor accepted at sign-in (see <see cref="MfaFactor.AcceptCode"/>).</summary>
    public Result AcceptMfaCode(Guid factorId, long timeStep, DateTimeOffset nowUtc)
    {
        var factor = _mfaFactors.FirstOrDefault(f => f.Id == factorId);
        if (factor is null) return Result.Fail(UserErrors.MfaFactorNotFound());

        return factor.AcceptCode(timeStep, nowUtc);
    }

    /// <summary>Uses up the unused recovery code with this hash.</summary>
    public Result ConsumeRecoveryCode(string codeHash, DateTimeOffset nowUtc)
    {
        var code = _recoveryCodes.FirstOrDefault(c => c.Matches(codeHash));
        if (code is null) return Result.Fail(UserErrors.RecoveryCodeInvalid());

        var consumeResult = code.Consume(nowUtc);
        if (consumeResult.IsFailed) return consumeResult;

        RaiseDomainEvent(new RecoveryCodeUsedDomainEvent(RemainingRecoveryCodeCount));
        return Result.Ok();
    }

    /// <summary>
    /// An admin's reset: removes every MFA factor (enabled or not) and every recovery code, and asks the user to
    /// enroll again at their next sign-in. Unlike <see cref="RevokeMfaFactor"/>, the MFA policy doesn't stop it:
    /// it's how a user who lost their authenticator gets back in.
    /// </summary>
    public Result ResetMfa()
    {
        var ensureMutable = EnsureMutable();
        if (ensureMutable.IsFailed) return ensureMutable;

        var removedFactorCount = _mfaFactors.Count;
        var removedRecoveryCodeCount = _recoveryCodes.Count;
        _mfaFactors.Clear();
        _recoveryCodes.Clear();
        AddRequiredAction(RequiredAction.EnrollMfa);
        RaiseDomainEvent(new MfaResetDomainEvent(removedFactorCount, removedRecoveryCodeCount));
        return Result.Ok();
    }

    public Result RehashCredential(string newHash, int newAlgorithmVersion)
    {
        if (Credential is null) return Result.Fail(UserErrors.InvalidStatusTransition());

        Credential.Rehash(newHash, newAlgorithmVersion, DateTimeOffset.UtcNow);
        return Result.Ok();
    }

    public Result RevokeMfaFactor(Guid factorId, MfaPolicy policy)
    {
        var factor = _mfaFactors.FirstOrDefault(f => f.Id == factorId);
        if (factor is null) return Result.Fail(UserErrors.MfaFactorNotFound());

        var canRevoke = MfaPolicyEvaluator.CanRevokeFactor(policy, this, factor);
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

    /// <summary>Removes the attribute with this key (trimmed, matched exactly like <see cref="SetAttribute"/>).</summary>
    public Result RemoveAttribute(string key)
    {
        var trimmedKey = key.Trim();
        var existing = _attributes.FirstOrDefault(a => a.Key == trimmedKey);
        if (existing is null) return Result.Fail(UserErrors.AttributeNotFound(trimmedKey));

        _attributes.Remove(existing);
        return Result.Ok();
    }

    public void AddRequiredAction(RequiredAction action)
    {
        if (!_pendingActions.Contains(action)) _pendingActions.Add(action);
    }

    public void ClearRequiredAction(RequiredAction action) => _pendingActions.Remove(action);
}
