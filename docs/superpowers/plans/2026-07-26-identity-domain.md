# Identity Domain (User Aggregate) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build out the `User` aggregate in a new `WardenId.Identity.Domain` project per `docs/superpowers/specs/2026-07-26-identity-domain-design.md` — enums, value objects, child entities, aggregate behaviors, domain events, domain services, errors — with a new xunit test project (`WardenId.Identity.Domain.Tests`).

**Architecture:** Follow `WardenId.Tenancy.Domain` and `WardenId.ClientManagement.Domain` conventions exactly: `Result<T>`-returning static factories, `Create`/`FromTrustedSource` split on value objects, error factories in a static `UserErrors` class tagged with `UserErrorCodes` metadata, domain events raised via `RaiseDomainEvent` in an `Events/` subfolder, `DateTimeOffset.UtcNow` called directly inside aggregate/entity methods (no injected clock — matches `Tenant.Suspend`/`Reactivate`).

**Tech Stack:** .NET 10, FluentResults 4.0.0, xunit 2.9.3 + xunit.runner.visualstudio 3.1.5 + Microsoft.NET.Test.Sdk 18.8.1 (all already present in `Directory.Packages.props` from the `WardenId.ClientManagement.Domain.Tests` pass — no edits needed there).

## Global Constraints

- Target framework `net10.0`, nullable + implicit usings enabled (inherited from root `Directory.Build.props` — never redeclare in a new `.csproj`).
- `RestorePackagesWithLockFile=true` repo-wide — every new project needs `dotnet restore` run once to generate its `packages.lock.json`.
- Package versions go in `Directory.Packages.props` only; project `.csproj` files reference packages by name with no `Version=` attribute. All packages needed here (`FluentResults`, `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`) already exist in `Directory.Packages.props` — no edits to that file in this plan.
- Domain layer has zero framework dependencies — no EF Core, no MediatR, no ASP.NET in `WardenId.Identity.Domain`. Only project reference: `WardenId.SharedKernel.Domain`. Only package reference: `FluentResults`.
- Value objects: `Create(raw)` validates for external input, `FromTrustedSource(value)` skips validation for EF materialization — never skip this split, except where a VO can never be invalid (`MfaPolicy`, backed by a plain enum with no invalid state).
- Aggregate/entity `Id` comes from `SharedKernel.Domain.Entity` (`Guid.CreateVersion7()`), never manually assigned.
- Every state-changing aggregate behavior that matters for eventing raises a `DomainEvent` via `RaiseDomainEvent` — pure bookkeeping mutators (`SetAttribute`, `AddRequiredAction`/`ClearRequiredAction`, `RecordSuccessfulLogin`) do not, per the spec's behavior table.
- Secrets/tokens/codes are stored as hashes only (`TokenHash`, `CodeHash`, `Hash`) — Domain never generates or compares raw secret material itself; hashing/comparison happens through `IPasswordHasher`, and raw token/code generation is an Infrastructure concern entirely outside this pass.
- **Deviation from the committed spec, corrected here to match actual repo convention:** the spec's "Repository" section describes `IUserRepository` living in `Identity.Domain`. Checking `WardenId.Tenancy.Application/Abstractions/ITenantRepository.cs` and `WardenId.ClientManagement.Application/Abstractions/IClientRepository.cs` shows repository interfaces actually live in `<Module>.Application/Abstractions/`, not Domain, in this repo — and the `ClientManagement` precedent deferred `IClientRepository` to the later Application/Infrastructure/Presentation pass (`2026-07-25-client-management-crud`), not the Domain-only pass. This plan follows that precedent: **no `IUserRepository` in this pass** — it's deferred to the future Identity Application/CRUD pass, consistent with "Domain layer only" scope already stated in the spec.
- **Error code note:** `WardenId.SharedKernel.Presentation/ErrorCodes.cs` already defines a shared `AccountLocked = "AccountLocked"` constant, and `IResultExtensions.HandleApiFailure` already maps it to HTTP 423. `UserErrorCodes.AccountLocked` in this plan uses the identical string literal `"AccountLocked"` so it routes correctly with **zero changes needed** to `SharedKernel.Presentation` — Domain stays decoupled from Presentation, matched only by string value.
- This pass is Domain-layer only — no Application/Infrastructure/Presentation projects, no `IUserRepository`, no WebAuthn/passkey implementation (only the `MfaFactorType.WebAuthnPasskey` stub), no impersonation (FR-ID-14), no notification port (lives in `SharedKernel.Application`, out of scope here). See spec's "Explicitly Out of Scope" section.

---

## File Structure

**New — `src/WardenId.Identity.Domain/`:**
- `WardenId.Identity.Domain.csproj`
- `UserStatus.cs`, `MfaFactorType.cs`, `RequiredAction.cs`, `UserAttributeValueType.cs`, `MfaPolicyMode.cs`
- `UserErrorCodes.cs`, `UserErrors.cs`
- `Email.cs`, `Username.cs`, `PasswordPolicy.cs`, `MfaPolicy.cs`, `LoginLockoutState.cs`
- `PasswordCredential.cs`, `PasswordHistoryEntry.cs`
- `EmailVerificationToken.cs`, `PasswordResetToken.cs`
- `MfaFactor.cs`, `RecoveryCode.cs`
- `UserAttribute.cs`
- `IPasswordHasher.cs`, `IBreachedPasswordChecker.cs`
- `PasswordPolicyEvaluator.cs`, `MfaPolicyEvaluator.cs`
- `User.cs`
- `Events/UserRegisteredDomainEvent.cs`, `Events/UserEmailVerifiedDomainEvent.cs`, `Events/PasswordChangedDomainEvent.cs`, `Events/LoginFailedDomainEvent.cs`, `Events/AccountLockedOutDomainEvent.cs`, `Events/UserUnlockedDomainEvent.cs`, `Events/UserDisabledDomainEvent.cs`, `Events/UserSoftDeletedDomainEvent.cs`, `Events/MfaFactorEnrolledDomainEvent.cs`, `Events/MfaFactorRevokedDomainEvent.cs`, `Events/PasswordResetRequestedDomainEvent.cs`, `Events/PasswordResetDomainEvent.cs`
- `README.md`

**New — `tests/WardenId.Identity.Domain.Tests/`:**
- `WardenId.Identity.Domain.Tests.csproj`
- `UserErrorsTests.cs`, `EmailTests.cs`, `UsernameTests.cs`, `PasswordPolicyTests.cs`, `MfaPolicyTests.cs`, `LoginLockoutStateTests.cs`
- `PasswordCredentialTests.cs`, `EmailVerificationTokenTests.cs`, `PasswordResetTokenTests.cs`, `MfaFactorTests.cs`, `RecoveryCodeTests.cs`, `UserAttributeTests.cs`
- `PasswordPolicyEvaluatorTests.cs`, `MfaPolicyEvaluatorTests.cs`
- `UserRegisterTests.cs`, `UserVerifyEmailTests.cs`, `UserSetPasswordTests.cs`, `UserLoginLockoutTests.cs`, `UserLifecycleTests.cs`, `UserMfaTests.cs`, `UserPasswordResetTests.cs`, `UserAttributesAndActionsTests.cs`

**Modify:**
- `WardenId.slnx` — register both new projects.

---

### Task 1: Project scaffold — enums, `UserErrorCodes`, `UserErrors`

**Files:**
- Create: `src/WardenId.Identity.Domain/WardenId.Identity.Domain.csproj`
- Create: `src/WardenId.Identity.Domain/UserStatus.cs`
- Create: `src/WardenId.Identity.Domain/MfaFactorType.cs`
- Create: `src/WardenId.Identity.Domain/RequiredAction.cs`
- Create: `src/WardenId.Identity.Domain/UserAttributeValueType.cs`
- Create: `src/WardenId.Identity.Domain/MfaPolicyMode.cs`
- Create: `src/WardenId.Identity.Domain/UserErrorCodes.cs`
- Create: `src/WardenId.Identity.Domain/UserErrors.cs`
- Create: `tests/WardenId.Identity.Domain.Tests/WardenId.Identity.Domain.Tests.csproj`
- Create: `tests/WardenId.Identity.Domain.Tests/UserErrorsTests.cs`
- Modify: `WardenId.slnx`

**Interfaces:**
- Produces: `UserStatus` (`PendingVerification=1, Active=2, Disabled=3, Locked=4, SoftDeleted=5`), `MfaFactorType` (`Totp=1, WebAuthnPasskey=2`), `RequiredAction` (`ResetPassword=1, AcceptTos=2, EnrollMfa=3, VerifyEmail=4`), `UserAttributeValueType` (`String=1, Number=2, Bool=3, Date=4`), `MfaPolicyMode` (`Off=1, Optional=2, Required=3, RequiredForAdmins=4`) — every later task uses these exact names/values. `UserErrorCodes.MetadataKey`/`NotFound`/`Validation`/`AccountLocked` and the full `UserErrors` factory set — every later task's `Result.Fail(...)` calls use these exact method names.

- [ ] **Step 1: Create the Domain project file**

`src/WardenId.Identity.Domain/WardenId.Identity.Domain.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
      <PackageReference Include="FluentResults" />
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include="..\WardenId.SharedKernel.Domain\WardenId.SharedKernel.Domain.csproj" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: Create the enums**

`src/WardenId.Identity.Domain/UserStatus.cs`:

```csharp
namespace WardenId.Identity.Domain;

public enum UserStatus
{
    PendingVerification = 1,
    Active = 2,
    Disabled = 3,
    Locked = 4,
    SoftDeleted = 5
}
```

`src/WardenId.Identity.Domain/MfaFactorType.cs`:

```csharp
namespace WardenId.Identity.Domain;

// WebAuthnPasskey is the FR-ID-08 extension point — reserved, not implemented yet.
public enum MfaFactorType
{
    Totp = 1,
    WebAuthnPasskey = 2
}
```

`src/WardenId.Identity.Domain/RequiredAction.cs`:

```csharp
namespace WardenId.Identity.Domain;

public enum RequiredAction
{
    ResetPassword = 1,
    AcceptTos = 2,
    EnrollMfa = 3,
    VerifyEmail = 4
}
```

`src/WardenId.Identity.Domain/UserAttributeValueType.cs`:

```csharp
namespace WardenId.Identity.Domain;

public enum UserAttributeValueType
{
    String = 1,
    Number = 2,
    Bool = 3,
    Date = 4
}
```

`src/WardenId.Identity.Domain/MfaPolicyMode.cs`:

```csharp
namespace WardenId.Identity.Domain;

public enum MfaPolicyMode
{
    Off = 1,
    Optional = 2,
    Required = 3,
    RequiredForAdmins = 4
}
```

- [ ] **Step 3: Create `UserErrorCodes`**

`src/WardenId.Identity.Domain/UserErrorCodes.cs`:

```csharp
namespace WardenId.Identity.Domain;

public static class UserErrorCodes
{
    public const string MetadataKey = "ErrorCode";

    public const string NotFound = "NotFound";
    public const string Validation = "Validation";

    // Matches WardenId.SharedKernel.Presentation.ErrorCodes.AccountLocked verbatim so
    // HandleApiFailure already maps it to 423 with no Presentation-layer changes.
    public const string AccountLocked = "AccountLocked";
}
```

- [ ] **Step 4: Create `UserErrors`**

`src/WardenId.Identity.Domain/UserErrors.cs`:

```csharp
using FluentResults;

namespace WardenId.Identity.Domain;

public static class UserErrors
{
    public static Error NotFound =>
        new Error("User not found.")
            .WithMetadata(UserErrorCodes.MetadataKey, UserErrorCodes.NotFound);

    public static Error Validation(string reason)
        => new Error(reason)
            .WithMetadata(UserErrorCodes.MetadataKey, UserErrorCodes.Validation);

    public static Error AccountLocked(DateTimeOffset lockedUntilUtc)
        => new Error($"Account is locked until {lockedUntilUtc:O}.")
            .WithMetadata(UserErrorCodes.MetadataKey, UserErrorCodes.AccountLocked);

    public static Error EmailRequired() => Validation("Email is required.");
    public static Error EmailInvalidFormat() => Validation("Email is not a valid email address.");
    // Literal 254, not Email.MaxLength — Email doesn't exist until Task 2; keep this value in sync with Email.MaxLength.
    public static Error EmailTooLong() => Validation("Email cannot exceed 254 characters.");

    public static Error UsernameRequired() => Validation("Username is required.");
    // Literals 3/64, not Username.MinLength/MaxLength — Username doesn't exist until Task 3; keep in sync with those constants.
    public static Error UsernameInvalidLength() => Validation("Username must be between 3 and 64 characters.");
    public static Error UsernameInvalidFormat() => Validation("Username may only contain letters, digits, dots, underscores and hyphens.");

    public static Error PasswordPolicyMinLengthInvalid() => Validation("Password policy minimum length must be at least 1.");
    public static Error PasswordPolicyHistoryCountInvalid() => Validation("Password policy history count cannot be negative.");

    public static Error PasswordTooShort(int minLength) => Validation($"Password must be at least {minLength} characters.");
    public static Error PasswordMissingUppercase() => Validation("Password must contain an uppercase letter.");
    public static Error PasswordMissingDigit() => Validation("Password must contain a digit.");
    public static Error PasswordMissingSymbol() => Validation("Password must contain a symbol.");
    public static Error PasswordBreached() => Validation("Password appears in a known data breach and cannot be used.");
    public static Error PasswordReused() => Validation("Password matches a previously used password.");

    public static Error TokenInvalid() => Validation("Token is invalid.");
    public static Error TokenExpired() => Validation("Token has expired.");
    public static Error TokenAlreadyConsumed() => Validation("Token has already been used.");

    public static Error EmailAlreadyVerified() => Validation("Email is already verified.");
    public static Error InvalidStatusTransition() => Validation("User is not in a valid state for this operation.");
    public static Error DisableReasonRequired() => Validation("A reason is required to disable a user.");

    public static Error RecoveryCodeAlreadyUsed() => Validation("Recovery code has already been used.");

    public static Error MfaFactorNotFound() => Validation("MFA factor not found.");
    public static Error MfaFactorAlreadyVerified() => Validation("MFA factor is already verified.");
    public static Error CannotRevokeLastRequiredFactor() => Validation("Cannot revoke the last MFA factor while MFA is required for this account.");

    public static Error AttributeKeyRequired() => Validation("Attribute key is required.");
}
```

- [ ] **Step 5: Create the test project file**

`tests/WardenId.Identity.Domain.Tests/WardenId.Identity.Domain.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\WardenId.Identity.Domain\WardenId.Identity.Domain.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="WardenId.Identity.Domain" />
  </ItemGroup>

</Project>
```

Note: no `<Using Include="WardenId.Identity.Domain.Events" />` yet — a global using targeting a namespace with zero types in it fails to compile (`CS0246`), and `Events/` is empty until Task 12 creates the first domain event. Task 12 adds this line once the namespace is populated.

- [ ] **Step 6: Register both projects in `WardenId.slnx`**

Add a new folder (alongside the existing `/src/WardenId.Tenancy/` etc. folders) and a project entry under `/tests/`:

```xml
  <Folder Name="/src/WardenId.Identity/">
    <Project Path="src/WardenId.Identity.Domain/WardenId.Identity.Domain.csproj" />
  </Folder>
```

And change:

```xml
  <Folder Name="/tests/">
    <Project Path="tests/WardenId.ClientManagement.Domain.Tests/WardenId.ClientManagement.Domain.Tests.csproj" />
  </Folder>
```

to:

```xml
  <Folder Name="/tests/">
    <Project Path="tests/WardenId.ClientManagement.Domain.Tests/WardenId.ClientManagement.Domain.Tests.csproj" />
    <Project Path="tests/WardenId.Identity.Domain.Tests/WardenId.Identity.Domain.Tests.csproj" />
  </Folder>
```

- [ ] **Step 7: Write the failing test**

`tests/WardenId.Identity.Domain.Tests/UserErrorsTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class UserErrorsTests
{
    [Fact]
    public void Validation_SetsValidationErrorCodeMetadata()
    {
        var error = UserErrors.Validation("some reason");

        Assert.Equal("some reason", error.Message);
        Assert.Equal(UserErrorCodes.Validation, error.Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public void NotFound_SetsNotFoundErrorCodeMetadata()
    {
        var error = UserErrors.NotFound;

        Assert.Equal(UserErrorCodes.NotFound, error.Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public void AccountLocked_SetsAccountLockedErrorCodeMetadata()
    {
        var error = UserErrors.AccountLocked(DateTimeOffset.UtcNow.AddMinutes(15));

        Assert.Equal(UserErrorCodes.AccountLocked, error.Metadata[UserErrorCodes.MetadataKey]);
        Assert.Equal("AccountLocked", UserErrorCodes.AccountLocked);
    }
}
```

- [ ] **Step 8: Restore and run to verify it fails**

Run: `dotnet restore tests/WardenId.Identity.Domain.Tests/WardenId.Identity.Domain.Tests.csproj`
Then: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserErrorsTests -v n`
Expected: FAIL (project/files don't fully exist yet until steps above are all applied — if you're following steps in order, this instead PASSES once steps 1-7 are all in place; if it fails, check that every file above was created before running).

- [ ] **Step 9: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserErrorsTests -v n`
Expected: PASS (3 tests)

- [ ] **Step 10: Commit**

```bash
git add src/WardenId.Identity.Domain tests/WardenId.Identity.Domain.Tests WardenId.slnx
git commit -m "feat: scaffold Identity domain project with enums and error factories"
```

---

### Task 2: `Email` value object

**Files:**
- Create: `src/WardenId.Identity.Domain/Email.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/EmailTests.cs`

**Interfaces:**
- Consumes: `UserErrors.EmailRequired()`, `.EmailTooLong()`, `.EmailInvalidFormat()` (Task 1).
- Produces: `Email` — `Value` (string, lowercased), `Create(string?) -> Result<Email>`, `FromTrustedSource(string) -> Email`. Every later task referencing a user's email uses this type.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/EmailTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class EmailTests
{
    [Fact]
    public void Create_FailsOnEmpty()
    {
        var result = Email.Create("");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsOnInvalidFormat()
    {
        var result = Email.Create("not-an-email");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenTooLong()
    {
        var result = Email.Create(new string('a', 250) + "@example.com");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_LowercasesAndTrimsValidEmail()
    {
        var result = Email.Create("  Someone@Example.COM  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("someone@example.com", result.Value.Value);
    }

    [Fact]
    public void FromTrustedSource_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => Email.FromTrustedSource(""));
    }

    [Fact]
    public void TwoEmailsWithSameValue_AreEqual()
    {
        var a = Email.Create("someone@example.com").Value;
        var b = Email.Create("someone@example.com").Value;

        Assert.Equal(a, b);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter EmailTests -v n`
Expected: FAIL (`Email` doesn't exist)

- [ ] **Step 3: Implement `Email`**

`src/WardenId.Identity.Domain/Email.cs`:

```csharp
using System.Text.RegularExpressions;
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class Email : ValueObject
{
    public const int MaxLength = 254;

    private static readonly Regex FormatRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Result.Fail(UserErrors.EmailRequired());

        var candidate = raw.Trim().ToLowerInvariant();

        if (candidate.Length > MaxLength) return Result.Fail(UserErrors.EmailTooLong());

        if (!FormatRegex.IsMatch(candidate)) return Result.Fail(UserErrors.EmailInvalidFormat());

        return Result.Ok(new Email(candidate));
    }

    public static Email FromTrustedSource(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Email cannot be null or empty.", nameof(value))
            : new Email(value);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter EmailTests -v n`
Expected: PASS (6 tests)

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.Identity.Domain/Email.cs tests/WardenId.Identity.Domain.Tests/EmailTests.cs
git commit -m "feat: add Email value object"
```

---

### Task 3: `Username` value object

**Files:**
- Create: `src/WardenId.Identity.Domain/Username.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/UsernameTests.cs`

**Interfaces:**
- Consumes: `UserErrors.UsernameRequired()`, `.UsernameInvalidLength()`, `.UsernameInvalidFormat()` (Task 1).
- Produces: `Username` — `Value` (string), `Create(string?) -> Result<Username>`, `FromTrustedSource(string) -> Username`, `MinLength = 3`, `MaxLength = 64`.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/UsernameTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class UsernameTests
{
    [Fact]
    public void Create_FailsOnEmpty()
    {
        var result = Username.Create("");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenTooShort()
    {
        var result = Username.Create("ab");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenTooLong()
    {
        var result = Username.Create(new string('a', 65));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsOnInvalidCharacters()
    {
        var result = Username.Create("bad username!");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_SucceedsOnValidUsername()
    {
        var result = Username.Create("john.doe-99");

        Assert.True(result.IsSuccess);
        Assert.Equal("john.doe-99", result.Value.Value);
    }

    [Fact]
    public void FromTrustedSource_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => Username.FromTrustedSource(""));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UsernameTests -v n`
Expected: FAIL (`Username` doesn't exist)

- [ ] **Step 3: Implement `Username`**

`src/WardenId.Identity.Domain/Username.cs`:

```csharp
using System.Text.RegularExpressions;
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class Username : ValueObject
{
    public const int MinLength = 3;
    public const int MaxLength = 64;

    private static readonly Regex FormatRegex = new(@"^[a-zA-Z0-9._-]+$", RegexOptions.Compiled);

    public string Value { get; }

    private Username(string value) => Value = value;

    public static Result<Username> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Result.Fail(UserErrors.UsernameRequired());

        var candidate = raw.Trim();

        if (candidate.Length is < MinLength or > MaxLength) return Result.Fail(UserErrors.UsernameInvalidLength());

        if (!FormatRegex.IsMatch(candidate)) return Result.Fail(UserErrors.UsernameInvalidFormat());

        return Result.Ok(new Username(candidate));
    }

    public static Username FromTrustedSource(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Username cannot be null or empty.", nameof(value))
            : new Username(value);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UsernameTests -v n`
Expected: PASS (6 tests)

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.Identity.Domain/Username.cs tests/WardenId.Identity.Domain.Tests/UsernameTests.cs
git commit -m "feat: add Username value object"
```

---

### Task 4: `PasswordPolicy` value object

**Files:**
- Create: `src/WardenId.Identity.Domain/PasswordPolicy.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/PasswordPolicyTests.cs`

**Interfaces:**
- Consumes: `UserErrors.PasswordPolicyMinLengthInvalid()`, `.PasswordPolicyHistoryCountInvalid()` (Task 1).
- Produces: `PasswordPolicy` — `MinLength` (int), `RequireUppercase`/`RequireDigit`/`RequireSymbol`/`BreachCheckEnabled` (bool), `HistoryCount` (int), `Create(minLength, requireUppercase, requireDigit, requireSymbol, breachCheckEnabled, historyCount) -> Result<PasswordPolicy>`, `FromTrustedSource(...)` (same params, no validation). Used by Task 11 (`PasswordPolicyEvaluator`) and Task 13 (`User.SetPassword`).

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/PasswordPolicyTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class PasswordPolicyTests
{
    [Fact]
    public void Create_FailsWhenMinLengthLessThanOne()
    {
        var result = PasswordPolicy.Create(0, true, true, true, true, 5);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenHistoryCountNegative()
    {
        var result = PasswordPolicy.Create(8, true, true, true, true, -1);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_SucceedsWithValidValues()
    {
        var result = PasswordPolicy.Create(12, true, true, false, true, 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value.MinLength);
        Assert.Equal(5, result.Value.HistoryCount);
        Assert.False(result.Value.RequireSymbol);
    }

    [Fact]
    public void FromTrustedSource_SkipsValidation()
    {
        var policy = PasswordPolicy.FromTrustedSource(8, false, false, false, false, 0);

        Assert.Equal(8, policy.MinLength);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter PasswordPolicyTests -v n`
Expected: FAIL (`PasswordPolicy` doesn't exist)

- [ ] **Step 3: Implement `PasswordPolicy`**

`src/WardenId.Identity.Domain/PasswordPolicy.cs`:

```csharp
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class PasswordPolicy : ValueObject
{
    public int MinLength { get; }
    public bool RequireUppercase { get; }
    public bool RequireDigit { get; }
    public bool RequireSymbol { get; }
    public bool BreachCheckEnabled { get; }
    public int HistoryCount { get; }

    private PasswordPolicy(
        int minLength, bool requireUppercase, bool requireDigit, bool requireSymbol,
        bool breachCheckEnabled, int historyCount)
    {
        MinLength = minLength;
        RequireUppercase = requireUppercase;
        RequireDigit = requireDigit;
        RequireSymbol = requireSymbol;
        BreachCheckEnabled = breachCheckEnabled;
        HistoryCount = historyCount;
    }

    public static Result<PasswordPolicy> Create(
        int minLength, bool requireUppercase, bool requireDigit, bool requireSymbol,
        bool breachCheckEnabled, int historyCount)
    {
        if (minLength < 1) return Result.Fail(UserErrors.PasswordPolicyMinLengthInvalid());
        if (historyCount < 0) return Result.Fail(UserErrors.PasswordPolicyHistoryCountInvalid());

        return Result.Ok(new PasswordPolicy(minLength, requireUppercase, requireDigit, requireSymbol, breachCheckEnabled, historyCount));
    }

    public static PasswordPolicy FromTrustedSource(
        int minLength, bool requireUppercase, bool requireDigit, bool requireSymbol,
        bool breachCheckEnabled, int historyCount)
        => new(minLength, requireUppercase, requireDigit, requireSymbol, breachCheckEnabled, historyCount);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MinLength;
        yield return RequireUppercase;
        yield return RequireDigit;
        yield return RequireSymbol;
        yield return BreachCheckEnabled;
        yield return HistoryCount;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter PasswordPolicyTests -v n`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.Identity.Domain/PasswordPolicy.cs tests/WardenId.Identity.Domain.Tests/PasswordPolicyTests.cs
git commit -m "feat: add PasswordPolicy value object"
```

---

### Task 5: `MfaPolicy` value object

**Files:**
- Create: `src/WardenId.Identity.Domain/MfaPolicy.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/MfaPolicyTests.cs`

**Interfaces:**
- Consumes: `MfaPolicyMode` (Task 1).
- Produces: `MfaPolicy` — `Mode` (`MfaPolicyMode`), `Create(MfaPolicyMode) -> MfaPolicy` (cannot fail — enum-backed), `FromTrustedSource(MfaPolicyMode) -> MfaPolicy`. Used by Task 16 (`MfaPolicyEvaluator` and `User.RevokeMfaFactor`).

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/MfaPolicyTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class MfaPolicyTests
{
    [Fact]
    public void Create_SetsMode()
    {
        var policy = MfaPolicy.Create(MfaPolicyMode.Required);

        Assert.Equal(MfaPolicyMode.Required, policy.Mode);
    }

    [Fact]
    public void TwoPoliciesWithSameMode_AreEqual()
    {
        var a = MfaPolicy.Create(MfaPolicyMode.Optional);
        var b = MfaPolicy.Create(MfaPolicyMode.Optional);

        Assert.Equal(a, b);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter MfaPolicyTests -v n`
Expected: FAIL (`MfaPolicy` doesn't exist)

- [ ] **Step 3: Implement `MfaPolicy`**

`src/WardenId.Identity.Domain/MfaPolicy.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class MfaPolicy : ValueObject
{
    public MfaPolicyMode Mode { get; }

    private MfaPolicy(MfaPolicyMode mode) => Mode = mode;

    public static MfaPolicy Create(MfaPolicyMode mode) => new(mode);

    public static MfaPolicy FromTrustedSource(MfaPolicyMode mode) => new(mode);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Mode;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter MfaPolicyTests -v n`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.Identity.Domain/MfaPolicy.cs tests/WardenId.Identity.Domain.Tests/MfaPolicyTests.cs
git commit -m "feat: add MfaPolicy value object"
```

---

### Task 6: `LoginLockoutState` value object

**Files:**
- Create: `src/WardenId.Identity.Domain/LoginLockoutState.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/LoginLockoutStateTests.cs`

**Interfaces:**
- Produces: `LoginLockoutState` — `FailedAttemptCount` (int), `LockedUntilUtc` (`DateTimeOffset?`), static `None` (zero state), `FromTrustedSource(int, DateTimeOffset?) -> LoginLockoutState`. Used by `User.Lockout` (Task 12) and `User.RecordFailedLogin`/`RecordSuccessfulLogin`/`Unlock` (Task 14).

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/LoginLockoutStateTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class LoginLockoutStateTests
{
    [Fact]
    public void None_HasZeroFailedAttemptsAndNoLock()
    {
        Assert.Equal(0, LoginLockoutState.None.FailedAttemptCount);
        Assert.Null(LoginLockoutState.None.LockedUntilUtc);
    }

    [Fact]
    public void FromTrustedSource_SetsBothFields()
    {
        var lockedUntil = DateTimeOffset.UtcNow.AddMinutes(15);

        var state = LoginLockoutState.FromTrustedSource(3, lockedUntil);

        Assert.Equal(3, state.FailedAttemptCount);
        Assert.Equal(lockedUntil, state.LockedUntilUtc);
    }

    [Fact]
    public void TwoStatesWithSameValues_AreEqual()
    {
        var a = LoginLockoutState.FromTrustedSource(2, null);
        var b = LoginLockoutState.FromTrustedSource(2, null);

        Assert.Equal(a, b);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter LoginLockoutStateTests -v n`
Expected: FAIL (`LoginLockoutState` doesn't exist)

- [ ] **Step 3: Implement `LoginLockoutState`**

`src/WardenId.Identity.Domain/LoginLockoutState.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class LoginLockoutState : ValueObject
{
    public static readonly LoginLockoutState None = new(0, null);

    public int FailedAttemptCount { get; }
    public DateTimeOffset? LockedUntilUtc { get; }

    private LoginLockoutState(int failedAttemptCount, DateTimeOffset? lockedUntilUtc)
    {
        FailedAttemptCount = failedAttemptCount;
        LockedUntilUtc = lockedUntilUtc;
    }

    public static LoginLockoutState FromTrustedSource(int failedAttemptCount, DateTimeOffset? lockedUntilUtc)
        => new(failedAttemptCount, lockedUntilUtc);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FailedAttemptCount;
        yield return LockedUntilUtc;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter LoginLockoutStateTests -v n`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.Identity.Domain/LoginLockoutState.cs tests/WardenId.Identity.Domain.Tests/LoginLockoutStateTests.cs
git commit -m "feat: add LoginLockoutState value object"
```

---

### Task 7: `PasswordCredential` and `PasswordHistoryEntry` entities

**Files:**
- Create: `src/WardenId.Identity.Domain/PasswordCredential.cs`
- Create: `src/WardenId.Identity.Domain/PasswordHistoryEntry.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/PasswordCredentialTests.cs`

**Interfaces:**
- Consumes: `SharedKernel.Domain.Entity` (base `Id`/audit fields).
- Produces: `PasswordCredential` — `Hash`/`Algorithm` (string), `AlgorithmVersion` (int), `SetAtUtc` (`DateTimeOffset`), `Create(hash, algorithm, algorithmVersion, setAtUtc) -> PasswordCredential`, `NeedsRehash(int currentAlgorithmVersion) -> bool`, `Rehash(newHash, newAlgorithmVersion, rehashedAtUtc)`. `PasswordHistoryEntry` — `Hash`/`Algorithm` (string), `SetAtUtc` (`DateTimeOffset`), `Create(hash, algorithm, setAtUtc) -> PasswordHistoryEntry`. Used by `User.Credential`/`User.PasswordHistory` (Task 12) and `User.SetPassword` (Task 13).

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/PasswordCredentialTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class PasswordCredentialTests
{
    [Fact]
    public void Create_SetsAllFields()
    {
        var setAt = DateTimeOffset.UtcNow;

        var credential = PasswordCredential.Create("hash-v1", "Argon2id", 1, setAt);

        Assert.Equal("hash-v1", credential.Hash);
        Assert.Equal("Argon2id", credential.Algorithm);
        Assert.Equal(1, credential.AlgorithmVersion);
        Assert.Equal(setAt, credential.SetAtUtc);
    }

    [Fact]
    public void NeedsRehash_TrueWhenStoredVersionIsOlder()
    {
        var credential = PasswordCredential.Create("hash-v1", "Argon2id", 1, DateTimeOffset.UtcNow);

        Assert.True(credential.NeedsRehash(2));
    }

    [Fact]
    public void NeedsRehash_FalseWhenStoredVersionIsCurrent()
    {
        var credential = PasswordCredential.Create("hash-v1", "Argon2id", 2, DateTimeOffset.UtcNow);

        Assert.False(credential.NeedsRehash(2));
    }

    [Fact]
    public void Rehash_ReplacesHashAndVersionAndTimestamp()
    {
        var credential = PasswordCredential.Create("hash-v1", "Argon2id", 1, DateTimeOffset.UtcNow.AddDays(-1));
        var rehashedAt = DateTimeOffset.UtcNow;

        credential.Rehash("hash-v2", 2, rehashedAt);

        Assert.Equal("hash-v2", credential.Hash);
        Assert.Equal(2, credential.AlgorithmVersion);
        Assert.Equal(rehashedAt, credential.SetAtUtc);
    }

    [Fact]
    public void PasswordHistoryEntry_Create_SetsAllFields()
    {
        var setAt = DateTimeOffset.UtcNow;

        var entry = PasswordHistoryEntry.Create("hash-old", "Argon2id", setAt);

        Assert.Equal("hash-old", entry.Hash);
        Assert.Equal("Argon2id", entry.Algorithm);
        Assert.Equal(setAt, entry.SetAtUtc);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter PasswordCredentialTests -v n`
Expected: FAIL (types don't exist)

- [ ] **Step 3: Implement `PasswordCredential`**

`src/WardenId.Identity.Domain/PasswordCredential.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class PasswordCredential : Entity
{
    // EF Core materialization only
    private PasswordCredential() { }

    private PasswordCredential(string hash, string algorithm, int algorithmVersion, DateTimeOffset setAtUtc)
    {
        Hash = hash;
        Algorithm = algorithm;
        AlgorithmVersion = algorithmVersion;
        SetAtUtc = setAtUtc;
    }

    public string Hash { get; private set; }
    public string Algorithm { get; private set; }
    public int AlgorithmVersion { get; private set; }
    public DateTimeOffset SetAtUtc { get; private set; }

    public static PasswordCredential Create(string hash, string algorithm, int algorithmVersion, DateTimeOffset setAtUtc) =>
        new(hash, algorithm, algorithmVersion, setAtUtc);

    public bool NeedsRehash(int currentAlgorithmVersion) => AlgorithmVersion < currentAlgorithmVersion;

    public void Rehash(string newHash, int newAlgorithmVersion, DateTimeOffset rehashedAtUtc)
    {
        Hash = newHash;
        AlgorithmVersion = newAlgorithmVersion;
        SetAtUtc = rehashedAtUtc;
    }
}
```

- [ ] **Step 4: Implement `PasswordHistoryEntry`**

`src/WardenId.Identity.Domain/PasswordHistoryEntry.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class PasswordHistoryEntry : Entity
{
    // EF Core materialization only
    private PasswordHistoryEntry() { }

    private PasswordHistoryEntry(string hash, string algorithm, DateTimeOffset setAtUtc)
    {
        Hash = hash;
        Algorithm = algorithm;
        SetAtUtc = setAtUtc;
    }

    public string Hash { get; private set; }
    public string Algorithm { get; private set; }
    public DateTimeOffset SetAtUtc { get; private set; }

    public static PasswordHistoryEntry Create(string hash, string algorithm, DateTimeOffset setAtUtc) =>
        new(hash, algorithm, setAtUtc);
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter PasswordCredentialTests -v n`
Expected: PASS (5 tests)

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Domain/PasswordCredential.cs src/WardenId.Identity.Domain/PasswordHistoryEntry.cs tests/WardenId.Identity.Domain.Tests/PasswordCredentialTests.cs
git commit -m "feat: add PasswordCredential and PasswordHistoryEntry entities"
```

---

### Task 8: `EmailVerificationToken` and `PasswordResetToken` entities

**Files:**
- Create: `src/WardenId.Identity.Domain/EmailVerificationToken.cs`
- Create: `src/WardenId.Identity.Domain/PasswordResetToken.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/EmailVerificationTokenTests.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/PasswordResetTokenTests.cs`

**Interfaces:**
- Consumes: `UserErrors.TokenAlreadyConsumed()`, `.TokenExpired()` (Task 1).
- Produces: `EmailVerificationToken` — `TokenHash` (string), `ExpiresAtUtc` (`DateTimeOffset`), `ConsumedAtUtc` (`DateTimeOffset?`), `Create(tokenHash, expiresAtUtc) -> EmailVerificationToken`, `Matches(string tokenHash) -> bool`, `Consume(DateTimeOffset nowUtc) -> Result`. `PasswordResetToken` — identical shape, separate class, own `Matches`/`Consume`. Used by `User.EmailVerificationTokens`/`VerifyEmail` (Task 12) and `User.PasswordResetTokens`/`RequestPasswordReset`/`ResetPassword` (Task 17).

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/EmailVerificationTokenTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class EmailVerificationTokenTests
{
    [Fact]
    public void Matches_TrueForSameHash()
    {
        var token = EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1));

        Assert.True(token.Matches("hash-1"));
        Assert.False(token.Matches("hash-2"));
    }

    [Fact]
    public void Consume_SucceedsWhenNotExpiredOrConsumed()
    {
        var token = EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1));

        var result = token.Consume(DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.NotNull(token.ConsumedAtUtc);
    }

    [Fact]
    public void Consume_FailsWhenExpired()
    {
        var token = EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(-1));

        var result = token.Consume(DateTimeOffset.UtcNow);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Consume_FailsWhenAlreadyConsumed()
    {
        var token = EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1));
        token.Consume(DateTimeOffset.UtcNow);

        var result = token.Consume(DateTimeOffset.UtcNow);

        Assert.True(result.IsFailed);
    }
}
```

`tests/WardenId.Identity.Domain.Tests/PasswordResetTokenTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class PasswordResetTokenTests
{
    [Fact]
    public void Matches_TrueForSameHash()
    {
        var token = PasswordResetToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1));

        Assert.True(token.Matches("hash-1"));
    }

    [Fact]
    public void Consume_FailsWhenExpired()
    {
        var token = PasswordResetToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(-1));

        var result = token.Consume(DateTimeOffset.UtcNow);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Consume_SucceedsWhenValid()
    {
        var token = PasswordResetToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1));

        var result = token.Consume(DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter "EmailVerificationTokenTests|PasswordResetTokenTests" -v n`
Expected: FAIL (types don't exist)

- [ ] **Step 3: Implement `EmailVerificationToken`**

`src/WardenId.Identity.Domain/EmailVerificationToken.cs`:

```csharp
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class EmailVerificationToken : Entity
{
    // EF Core materialization only
    private EmailVerificationToken() { }

    private EmailVerificationToken(string tokenHash, DateTimeOffset expiresAtUtc)
    {
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string TokenHash { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public static EmailVerificationToken Create(string tokenHash, DateTimeOffset expiresAtUtc) =>
        new(tokenHash, expiresAtUtc);

    public bool Matches(string tokenHash) => TokenHash == tokenHash;

    public Result Consume(DateTimeOffset nowUtc)
    {
        if (ConsumedAtUtc is not null) return Result.Fail(UserErrors.TokenAlreadyConsumed());
        if (ExpiresAtUtc <= nowUtc) return Result.Fail(UserErrors.TokenExpired());

        ConsumedAtUtc = nowUtc;
        return Result.Ok();
    }
}
```

- [ ] **Step 4: Implement `PasswordResetToken`**

`src/WardenId.Identity.Domain/PasswordResetToken.cs`:

```csharp
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

// Kept separate from EmailVerificationToken (not a shared base) — same shape today,
// but consumption side effects differ (reset also clears account lockout).
public sealed class PasswordResetToken : Entity
{
    // EF Core materialization only
    private PasswordResetToken() { }

    private PasswordResetToken(string tokenHash, DateTimeOffset expiresAtUtc)
    {
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string TokenHash { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }

    public static PasswordResetToken Create(string tokenHash, DateTimeOffset expiresAtUtc) =>
        new(tokenHash, expiresAtUtc);

    public bool Matches(string tokenHash) => TokenHash == tokenHash;

    public Result Consume(DateTimeOffset nowUtc)
    {
        if (ConsumedAtUtc is not null) return Result.Fail(UserErrors.TokenAlreadyConsumed());
        if (ExpiresAtUtc <= nowUtc) return Result.Fail(UserErrors.TokenExpired());

        ConsumedAtUtc = nowUtc;
        return Result.Ok();
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter "EmailVerificationTokenTests|PasswordResetTokenTests" -v n`
Expected: PASS (7 tests)

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Domain/EmailVerificationToken.cs src/WardenId.Identity.Domain/PasswordResetToken.cs tests/WardenId.Identity.Domain.Tests/EmailVerificationTokenTests.cs tests/WardenId.Identity.Domain.Tests/PasswordResetTokenTests.cs
git commit -m "feat: add EmailVerificationToken and PasswordResetToken entities"
```

---

### Task 9: `MfaFactor` and `RecoveryCode` entities

**Files:**
- Create: `src/WardenId.Identity.Domain/MfaFactor.cs`
- Create: `src/WardenId.Identity.Domain/RecoveryCode.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/MfaFactorTests.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/RecoveryCodeTests.cs`

**Interfaces:**
- Consumes: `MfaFactorType` (Task 1), `UserErrors.MfaFactorAlreadyVerified()`, `.RecoveryCodeAlreadyUsed()` (Task 1).
- Produces: `MfaFactor` — `FactorType` (`MfaFactorType`), `CredentialData` (string, opaque ciphertext), `Enabled` (bool, starts `false`), `EnrolledAtUtc` (`DateTimeOffset`), `VerifiedAtUtc` (`DateTimeOffset?`), `Create(factorType, credentialData) -> MfaFactor`, `Verify() -> Result` (sets `Enabled = true`). `RecoveryCode` — `CodeHash` (string), `UsedAtUtc` (`DateTimeOffset?`), `Create(codeHash) -> RecoveryCode`, `Matches(string codeHash) -> bool`, `Consume() -> Result`. Used by `User.MfaFactors`/`RecoveryCodes` (Task 12) and `User.EnrollMfaFactor`/`RevokeMfaFactor` (Task 16).

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/MfaFactorTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class MfaFactorTests
{
    [Fact]
    public void Create_StartsDisabledAndUnverified()
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");

        Assert.False(factor.Enabled);
        Assert.Null(factor.VerifiedAtUtc);
        Assert.Equal(MfaFactorType.Totp, factor.FactorType);
    }

    [Fact]
    public void Verify_EnablesTheFactorAndSetsVerifiedAt()
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");

        var result = factor.Verify();

        Assert.True(result.IsSuccess);
        Assert.True(factor.Enabled);
        Assert.NotNull(factor.VerifiedAtUtc);
    }

    [Fact]
    public void Verify_FailsWhenAlreadyVerified()
    {
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        factor.Verify();

        var result = factor.Verify();

        Assert.True(result.IsFailed);
    }
}
```

`tests/WardenId.Identity.Domain.Tests/RecoveryCodeTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class RecoveryCodeTests
{
    [Fact]
    public void Matches_TrueForSameHash()
    {
        var code = RecoveryCode.Create("hash-1");

        Assert.True(code.Matches("hash-1"));
        Assert.False(code.Matches("hash-2"));
    }

    [Fact]
    public void Consume_SucceedsFirstTime()
    {
        var code = RecoveryCode.Create("hash-1");

        var result = code.Consume();

        Assert.True(result.IsSuccess);
        Assert.NotNull(code.UsedAtUtc);
    }

    [Fact]
    public void Consume_FailsWhenAlreadyUsed()
    {
        var code = RecoveryCode.Create("hash-1");
        code.Consume();

        var result = code.Consume();

        Assert.True(result.IsFailed);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter "MfaFactorTests|RecoveryCodeTests" -v n`
Expected: FAIL (types don't exist)

- [ ] **Step 3: Implement `MfaFactor`**

`src/WardenId.Identity.Domain/MfaFactor.cs`:

```csharp
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class MfaFactor : Entity
{
    // EF Core materialization only
    private MfaFactor() { }

    private MfaFactor(MfaFactorType factorType, string credentialData)
    {
        FactorType = factorType;
        CredentialData = credentialData;
        Enabled = false;
        EnrolledAtUtc = DateTimeOffset.UtcNow;
    }

    public MfaFactorType FactorType { get; private set; }
    public string CredentialData { get; private set; }
    public bool Enabled { get; private set; }
    public DateTimeOffset EnrolledAtUtc { get; private set; }
    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    public static MfaFactor Create(MfaFactorType factorType, string credentialData) =>
        new(factorType, credentialData);

    public Result Verify()
    {
        if (VerifiedAtUtc is not null) return Result.Fail(UserErrors.MfaFactorAlreadyVerified());

        VerifiedAtUtc = DateTimeOffset.UtcNow;
        Enabled = true;
        return Result.Ok();
    }
}
```

- [ ] **Step 4: Implement `RecoveryCode`**

`src/WardenId.Identity.Domain/RecoveryCode.cs`:

```csharp
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class RecoveryCode : Entity
{
    // EF Core materialization only
    private RecoveryCode() { }

    private RecoveryCode(string codeHash) => CodeHash = codeHash;

    public string CodeHash { get; private set; }
    public DateTimeOffset? UsedAtUtc { get; private set; }

    public static RecoveryCode Create(string codeHash) => new(codeHash);

    public bool Matches(string codeHash) => CodeHash == codeHash;

    public Result Consume()
    {
        if (UsedAtUtc is not null) return Result.Fail(UserErrors.RecoveryCodeAlreadyUsed());

        UsedAtUtc = DateTimeOffset.UtcNow;
        return Result.Ok();
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter "MfaFactorTests|RecoveryCodeTests" -v n`
Expected: PASS (6 tests)

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Domain/MfaFactor.cs src/WardenId.Identity.Domain/RecoveryCode.cs tests/WardenId.Identity.Domain.Tests/MfaFactorTests.cs tests/WardenId.Identity.Domain.Tests/RecoveryCodeTests.cs
git commit -m "feat: add MfaFactor and RecoveryCode entities"
```

---

### Task 10: `UserAttribute` entity

**Files:**
- Create: `src/WardenId.Identity.Domain/UserAttribute.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/UserAttributeTests.cs`

**Interfaces:**
- Consumes: `UserAttributeValueType` (Task 1), `UserErrors.AttributeKeyRequired()` (Task 1).
- Produces: `UserAttribute` — `Key`/`Value` (string), `ValueType` (`UserAttributeValueType`), `Create(key, value, valueType) -> Result<UserAttribute>`, `UpdateValue(value, valueType)`. Used by `User.Attributes`/`SetAttribute` (Task 18).

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/UserAttributeTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class UserAttributeTests
{
    [Fact]
    public void Create_FailsOnEmptyKey()
    {
        var result = UserAttribute.Create("", "value", UserAttributeValueType.String);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_SucceedsWithTrimmedKey()
    {
        var result = UserAttribute.Create("  department  ", "engineering", UserAttributeValueType.String);

        Assert.True(result.IsSuccess);
        Assert.Equal("department", result.Value.Key);
        Assert.Equal("engineering", result.Value.Value);
    }

    [Fact]
    public void UpdateValue_ReplacesValueAndType()
    {
        var attribute = UserAttribute.Create("age", "30", UserAttributeValueType.Number).Value;

        attribute.UpdateValue("31", UserAttributeValueType.Number);

        Assert.Equal("31", attribute.Value);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserAttributeTests -v n`
Expected: FAIL (`UserAttribute` doesn't exist)

- [ ] **Step 3: Implement `UserAttribute`**

`src/WardenId.Identity.Domain/UserAttribute.cs`:

```csharp
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class UserAttribute : Entity
{
    // EF Core materialization only
    private UserAttribute() { }

    private UserAttribute(string key, string value, UserAttributeValueType valueType)
    {
        Key = key;
        Value = value;
        ValueType = valueType;
    }

    public string Key { get; private set; }
    public string Value { get; private set; }
    public UserAttributeValueType ValueType { get; private set; }

    public static Result<UserAttribute> Create(string key, string value, UserAttributeValueType valueType)
    {
        if (string.IsNullOrWhiteSpace(key)) return Result.Fail(UserErrors.AttributeKeyRequired());

        return Result.Ok(new UserAttribute(key.Trim(), value, valueType));
    }

    public void UpdateValue(string value, UserAttributeValueType valueType)
    {
        Value = value;
        ValueType = valueType;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserAttributeTests -v n`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.Identity.Domain/UserAttribute.cs tests/WardenId.Identity.Domain.Tests/UserAttributeTests.cs
git commit -m "feat: add UserAttribute entity"
```

---

### Task 11: Ports (`IPasswordHasher`, `IBreachedPasswordChecker`) and `PasswordPolicyEvaluator`

**Files:**
- Create: `src/WardenId.Identity.Domain/IPasswordHasher.cs`
- Create: `src/WardenId.Identity.Domain/IBreachedPasswordChecker.cs`
- Create: `src/WardenId.Identity.Domain/PasswordPolicyEvaluator.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/PasswordPolicyEvaluatorTests.cs`

**Interfaces:**
- Consumes: `PasswordPolicy` (Task 4), `PasswordHistoryEntry` (Task 7), `UserErrors.PasswordTooShort(int)`, `.PasswordMissingUppercase()`, `.PasswordMissingDigit()`, `.PasswordMissingSymbol()`, `.PasswordBreached()`, `.PasswordReused()` (Task 1).
- Produces: `IPasswordHasher` — `string Hash(string plaintext)`, `bool Verify(string plaintext, string hash)`, `bool NeedsRehash(string hash)`. `IBreachedPasswordChecker` — `Task<bool> IsBreachedAsync(string plaintext)`. `PasswordPolicyEvaluator.EvaluateAsync(PasswordPolicy policy, string candidatePassword, IReadOnlyCollection<PasswordHistoryEntry> history, IPasswordHasher passwordHasher, IBreachedPasswordChecker breachChecker) -> Task<Result>`. These ports are implemented in Infrastructure in a future pass — this task only defines the interfaces and the evaluator that consumes them via test fakes.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/PasswordPolicyEvaluatorTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class PasswordPolicyEvaluatorTests
{
    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string plaintext) => "hashed:" + plaintext;
        public bool Verify(string plaintext, string hash) => hash == "hashed:" + plaintext;
        public bool NeedsRehash(string hash) => false;
    }

    private sealed class FakeBreachedPasswordChecker(bool isBreached) : IBreachedPasswordChecker
    {
        public Task<bool> IsBreachedAsync(string plaintext) => Task.FromResult(isBreached);
    }

    private static readonly PasswordPolicy Policy =
        PasswordPolicy.Create(8, requireUppercase: true, requireDigit: true, requireSymbol: false, breachCheckEnabled: true, historyCount: 2).Value;

    [Fact]
    public async Task EvaluateAsync_FailsWhenTooShort()
    {
        var result = await PasswordPolicyEvaluator.EvaluateAsync(
            Policy, "Ab1", [], new FakePasswordHasher(), new FakeBreachedPasswordChecker(false));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task EvaluateAsync_FailsWhenMissingUppercase()
    {
        var result = await PasswordPolicyEvaluator.EvaluateAsync(
            Policy, "lowercase1", [], new FakePasswordHasher(), new FakeBreachedPasswordChecker(false));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task EvaluateAsync_FailsWhenMissingDigit()
    {
        var result = await PasswordPolicyEvaluator.EvaluateAsync(
            Policy, "NoDigitsHere", [], new FakePasswordHasher(), new FakeBreachedPasswordChecker(false));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task EvaluateAsync_FailsWhenBreached()
    {
        var result = await PasswordPolicyEvaluator.EvaluateAsync(
            Policy, "Valid1Password", [], new FakePasswordHasher(), new FakeBreachedPasswordChecker(true));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task EvaluateAsync_FailsWhenReusedFromHistory()
    {
        var hasher = new FakePasswordHasher();
        var history = new[] { PasswordHistoryEntry.Create(hasher.Hash("Valid1Password"), "Argon2id", DateTimeOffset.UtcNow) };

        var result = await PasswordPolicyEvaluator.EvaluateAsync(
            Policy, "Valid1Password", history, hasher, new FakeBreachedPasswordChecker(false));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task EvaluateAsync_SucceedsWhenAllChecksPass()
    {
        var result = await PasswordPolicyEvaluator.EvaluateAsync(
            Policy, "Valid1Password", [], new FakePasswordHasher(), new FakeBreachedPasswordChecker(false));

        Assert.True(result.IsSuccess);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter PasswordPolicyEvaluatorTests -v n`
Expected: FAIL (`IPasswordHasher`, `IBreachedPasswordChecker`, `PasswordPolicyEvaluator` don't exist)

- [ ] **Step 3: Implement the ports**

`src/WardenId.Identity.Domain/IPasswordHasher.cs`:

```csharp
namespace WardenId.Identity.Domain;

public interface IPasswordHasher
{
    string Hash(string plaintext);
    bool Verify(string plaintext, string hash);
    bool NeedsRehash(string hash);
}
```

`src/WardenId.Identity.Domain/IBreachedPasswordChecker.cs`:

```csharp
namespace WardenId.Identity.Domain;

public interface IBreachedPasswordChecker
{
    Task<bool> IsBreachedAsync(string plaintext);
}
```

- [ ] **Step 4: Implement `PasswordPolicyEvaluator`**

`src/WardenId.Identity.Domain/PasswordPolicyEvaluator.cs`:

```csharp
using FluentResults;

namespace WardenId.Identity.Domain;

public static class PasswordPolicyEvaluator
{
    public static async Task<Result> EvaluateAsync(
        PasswordPolicy policy,
        string candidatePassword,
        IReadOnlyCollection<PasswordHistoryEntry> history,
        IPasswordHasher passwordHasher,
        IBreachedPasswordChecker breachChecker)
    {
        if (candidatePassword.Length < policy.MinLength) return Result.Fail(UserErrors.PasswordTooShort(policy.MinLength));
        if (policy.RequireUppercase && !candidatePassword.Any(char.IsUpper)) return Result.Fail(UserErrors.PasswordMissingUppercase());
        if (policy.RequireDigit && !candidatePassword.Any(char.IsDigit)) return Result.Fail(UserErrors.PasswordMissingDigit());
        if (policy.RequireSymbol && !candidatePassword.Any(c => !char.IsLetterOrDigit(c))) return Result.Fail(UserErrors.PasswordMissingSymbol());

        if (policy.BreachCheckEnabled && await breachChecker.IsBreachedAsync(candidatePassword))
            return Result.Fail(UserErrors.PasswordBreached());

        if (history.Any(entry => passwordHasher.Verify(candidatePassword, entry.Hash)))
            return Result.Fail(UserErrors.PasswordReused());

        return Result.Ok();
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter PasswordPolicyEvaluatorTests -v n`
Expected: PASS (6 tests)

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Domain/IPasswordHasher.cs src/WardenId.Identity.Domain/IBreachedPasswordChecker.cs src/WardenId.Identity.Domain/PasswordPolicyEvaluator.cs tests/WardenId.Identity.Domain.Tests/PasswordPolicyEvaluatorTests.cs
git commit -m "feat: add password hashing/breach-check ports and PasswordPolicyEvaluator"
```

---

### Task 12: `User` aggregate skeleton — `Register` and `VerifyEmail`

**Files:**
- Create: `src/WardenId.Identity.Domain/User.cs`
- Create: `src/WardenId.Identity.Domain/Events/UserRegisteredDomainEvent.cs`
- Create: `src/WardenId.Identity.Domain/Events/UserEmailVerifiedDomainEvent.cs`
- Modify: `tests/WardenId.Identity.Domain.Tests/WardenId.Identity.Domain.Tests.csproj`
- Test: `tests/WardenId.Identity.Domain.Tests/UserRegisterTests.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/UserVerifyEmailTests.cs`

**Interfaces:**
- Consumes: `Email` (Task 2), `Username` (Task 3), `LoginLockoutState` (Task 6), `PasswordCredential`/`PasswordHistoryEntry` (Task 7), `EmailVerificationToken`/`PasswordResetToken` (Task 8), `MfaFactor`/`RecoveryCode` (Task 9), `UserAttribute` (Task 10), `UserErrors.EmailAlreadyVerified()`, `.TokenInvalid()` (Task 1).
- Produces: `User : AggregateRoot` with full state shape (`TenantId`, `Email`, `Username`, `Status`, `Credential`, `Lockout`, and all owned collections) — **every other `User` task (13-18) adds behaviors to this same class**, so this task's field/collection names are load-bearing for all of them: `MfaFactors`, `RecoveryCodes`, `Attributes`, `PendingActions`, `PasswordHistory`, `EmailVerificationTokens`, `PasswordResetTokens`. `User.Register(Guid tenantId, Email email, Username username) -> Result<User>`. `User.AddEmailVerificationToken(EmailVerificationToken token)`. `User.VerifyEmail(string tokenHash) -> Result`. This task also adds the `Events` global using to the test `.csproj` (Task 1 deliberately omitted it — see Task 1's note) now that `Events/` has real members.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/UserRegisterTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class UserRegisterTests
{
    private static (Email Email, Username Username) ValidInput() =>
        (Email.Create("someone@example.com").Value, Username.Create("someone").Value);

    [Fact]
    public void Register_ThrowsWhenTenantIdIsEmpty()
    {
        var (email, username) = ValidInput();

        Assert.Throws<ArgumentException>(() => User.Register(Guid.Empty, email, username));
    }

    [Fact]
    public void Register_SetsStatusToPendingVerification()
    {
        var (email, username) = ValidInput();

        var result = User.Register(Guid.NewGuid(), email, username);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.PendingVerification, result.Value.Status);
    }

    [Fact]
    public void Register_AddsVerifyEmailRequiredAction()
    {
        var (email, username) = ValidInput();

        var result = User.Register(Guid.NewGuid(), email, username);

        Assert.Contains(RequiredAction.VerifyEmail, result.Value.PendingActions);
    }

    [Fact]
    public void Register_RaisesUserRegisteredDomainEvent()
    {
        var (email, username) = ValidInput();

        var result = User.Register(Guid.NewGuid(), email, username);

        var domainEvent = Assert.Single(result.Value.DomainEvents);
        Assert.IsType<UserRegisteredDomainEvent>(domainEvent);
    }

    [Fact]
    public void Register_HasNoCredentialYet()
    {
        var (email, username) = ValidInput();

        var result = User.Register(Guid.NewGuid(), email, username);

        Assert.Null(result.Value.Credential);
    }
}
```

`tests/WardenId.Identity.Domain.Tests/UserVerifyEmailTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class UserVerifyEmailTests
{
    private static User CreateRegisteredUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    [Fact]
    public void VerifyEmail_FailsWhenTokenDoesNotMatch()
    {
        var user = CreateRegisteredUser();
        user.AddEmailVerificationToken(EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1)));

        var result = user.VerifyEmail("wrong-hash");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void VerifyEmail_FailsWhenTokenExpired()
    {
        var user = CreateRegisteredUser();
        user.AddEmailVerificationToken(EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(-1)));

        var result = user.VerifyEmail("hash-1");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void VerifyEmail_SucceedsAndActivatesUser()
    {
        var user = CreateRegisteredUser();
        user.AddEmailVerificationToken(EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1)));

        var result = user.VerifyEmail("hash-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.DoesNotContain(RequiredAction.VerifyEmail, user.PendingActions);
    }

    [Fact]
    public void VerifyEmail_RaisesUserEmailVerifiedDomainEvent()
    {
        var user = CreateRegisteredUser();
        user.AddEmailVerificationToken(EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1)));

        user.VerifyEmail("hash-1");

        Assert.Contains(user.DomainEvents, e => e is UserEmailVerifiedDomainEvent);
    }

    [Fact]
    public void VerifyEmail_FailsWhenUserAlreadyActive()
    {
        var user = CreateRegisteredUser();
        user.AddEmailVerificationToken(EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1)));
        user.VerifyEmail("hash-1");

        var result = user.VerifyEmail("hash-1");

        Assert.True(result.IsFailed);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter "UserRegisterTests|UserVerifyEmailTests" -v n`
Expected: FAIL (`User` doesn't exist as a concrete implementation yet)

- [ ] **Step 3: Implement the two domain events**

`src/WardenId.Identity.Domain/Events/UserRegisteredDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record UserRegisteredDomainEvent(Email Email, Username Username) : DomainEvent;
```

`src/WardenId.Identity.Domain/Events/UserEmailVerifiedDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record UserEmailVerifiedDomainEvent : DomainEvent;
```

- [ ] **Step 3b: Add the `Events` global using to the test project**

`Events/` now has real members, so the global using Task 1 deliberately omitted can be added back. In `tests/WardenId.Identity.Domain.Tests/WardenId.Identity.Domain.Tests.csproj`, change:

```xml
  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="WardenId.Identity.Domain" />
  </ItemGroup>
```

to:

```xml
  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="WardenId.Identity.Domain" />
    <Using Include="WardenId.Identity.Domain.Events" />
  </ItemGroup>
```

- [ ] **Step 4: Implement the `User` aggregate skeleton with `Register` and `VerifyEmail`**

`src/WardenId.Identity.Domain/User.cs`:

```csharp
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
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter "UserRegisterTests|UserVerifyEmailTests" -v n`
Expected: PASS (10 tests)

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Domain/User.cs src/WardenId.Identity.Domain/Events/UserRegisteredDomainEvent.cs src/WardenId.Identity.Domain/Events/UserEmailVerifiedDomainEvent.cs tests/WardenId.Identity.Domain.Tests/WardenId.Identity.Domain.Tests.csproj tests/WardenId.Identity.Domain.Tests/UserRegisterTests.cs tests/WardenId.Identity.Domain.Tests/UserVerifyEmailTests.cs
git commit -m "feat: add User aggregate with Register and VerifyEmail"
```

---

### Task 13: `User.SetPassword`

**Files:**
- Create: `src/WardenId.Identity.Domain/Events/PasswordChangedDomainEvent.cs`
- Modify: `src/WardenId.Identity.Domain/User.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/UserSetPasswordTests.cs`

**Interfaces:**
- Consumes: `PasswordCredential`/`PasswordHistoryEntry` (Task 7), `PasswordPolicy` (Task 4).
- Produces: `User.SetPassword(PasswordCredential newCredential, PasswordPolicy policy) -> Result`. Used by `User.ResetPassword` (Task 17).

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/UserSetPasswordTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class UserSetPasswordTests
{
    private static User CreateUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    private static PasswordPolicy PolicyWithHistory(int historyCount) =>
        PasswordPolicy.Create(8, true, true, false, false, historyCount).Value;

    [Fact]
    public void SetPassword_SetsCredentialOnFirstCall()
    {
        var user = CreateUser();
        var credential = PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow);

        var result = user.SetPassword(credential, PolicyWithHistory(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(credential, user.Credential);
        Assert.Empty(user.PasswordHistory);
    }

    [Fact]
    public void SetPassword_MovesPreviousCredentialToHistory()
    {
        var user = CreateUser();
        var first = PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow);
        user.SetPassword(first, PolicyWithHistory(2));
        var second = PasswordCredential.Create("hash-2", "Argon2id", 1, DateTimeOffset.UtcNow);

        user.SetPassword(second, PolicyWithHistory(2));

        Assert.Equal(second, user.Credential);
        Assert.Single(user.PasswordHistory);
        Assert.Equal("hash-1", user.PasswordHistory.Single().Hash);
    }

    [Fact]
    public void SetPassword_TrimsHistoryToPolicyCount()
    {
        var user = CreateUser();
        var policy = PolicyWithHistory(1);
        user.SetPassword(PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow), policy);
        user.SetPassword(PasswordCredential.Create("hash-2", "Argon2id", 1, DateTimeOffset.UtcNow), policy);

        user.SetPassword(PasswordCredential.Create("hash-3", "Argon2id", 1, DateTimeOffset.UtcNow), policy);

        Assert.Single(user.PasswordHistory);
        Assert.Equal("hash-2", user.PasswordHistory.Single().Hash);
    }

    [Fact]
    public void SetPassword_RaisesPasswordChangedDomainEvent()
    {
        var user = CreateUser();

        user.SetPassword(PasswordCredential.Create("hash-1", "Argon2id", 1, DateTimeOffset.UtcNow), PolicyWithHistory(2));

        Assert.Contains(user.DomainEvents, e => e is PasswordChangedDomainEvent);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserSetPasswordTests -v n`
Expected: FAIL (`SetPassword` doesn't exist)

- [ ] **Step 3: Implement the domain event**

`src/WardenId.Identity.Domain/Events/PasswordChangedDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record PasswordChangedDomainEvent : DomainEvent;
```

- [ ] **Step 4: Add `SetPassword` to `User`**

In `src/WardenId.Identity.Domain/User.cs`, add after `VerifyEmail`:

```csharp
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
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserSetPasswordTests -v n`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Domain/User.cs src/WardenId.Identity.Domain/Events/PasswordChangedDomainEvent.cs tests/WardenId.Identity.Domain.Tests/UserSetPasswordTests.cs
git commit -m "feat: add User.SetPassword with history trimming"
```

---

### Task 14: `User` login lockout — `RecordFailedLogin`, `RecordSuccessfulLogin`, `Unlock`

**Files:**
- Create: `src/WardenId.Identity.Domain/Events/LoginFailedDomainEvent.cs`
- Create: `src/WardenId.Identity.Domain/Events/AccountLockedOutDomainEvent.cs`
- Create: `src/WardenId.Identity.Domain/Events/UserUnlockedDomainEvent.cs`
- Modify: `src/WardenId.Identity.Domain/User.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/UserLoginLockoutTests.cs`

**Interfaces:**
- Consumes: `LoginLockoutState` (Task 6), `UserErrors.InvalidStatusTransition()` (Task 1).
- Produces: `User.RecordFailedLogin(int maxFailedAttempts, TimeSpan lockoutDuration) -> Result`, `User.RecordSuccessfulLogin()`, `User.Unlock() -> Result`. `maxFailedAttempts`/`lockoutDuration` are plain parameters here (no dedicated lockout-policy VO in the spec) — a future Application-layer caller sources these from tenant config.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/UserLoginLockoutTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class UserLoginLockoutTests
{
    private static User CreateActiveUser()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        user.AddEmailVerificationToken(EmailVerificationToken.Create("hash-1", DateTimeOffset.UtcNow.AddHours(1)));
        user.VerifyEmail("hash-1");
        return user;
    }

    [Fact]
    public void RecordFailedLogin_IncrementsFailedAttemptCount()
    {
        var user = CreateActiveUser();

        user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));

        Assert.Equal(1, user.Lockout.FailedAttemptCount);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void RecordFailedLogin_LocksAccountAtThreshold()
    {
        var user = CreateActiveUser();

        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));
        }

        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.NotNull(user.Lockout.LockedUntilUtc);
    }

    [Fact]
    public void RecordFailedLogin_RaisesAccountLockedOutDomainEventOnlyAtThreshold()
    {
        var user = CreateActiveUser();

        for (var i = 0; i < 4; i++)
        {
            user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));
        }
        Assert.DoesNotContain(user.DomainEvents, e => e is AccountLockedOutDomainEvent);

        user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));

        Assert.Contains(user.DomainEvents, e => e is AccountLockedOutDomainEvent);
    }

    [Fact]
    public void RecordSuccessfulLogin_ResetsLockoutState()
    {
        var user = CreateActiveUser();
        user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));

        user.RecordSuccessfulLogin();

        Assert.Equal(0, user.Lockout.FailedAttemptCount);
    }

    [Fact]
    public void Unlock_FailsWhenUserIsNotLocked()
    {
        var user = CreateActiveUser();

        var result = user.Unlock();

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Unlock_ReactivatesLockedUser()
    {
        var user = CreateActiveUser();
        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));
        }

        var result = user.Unlock();

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserLoginLockoutTests -v n`
Expected: FAIL (methods don't exist)

- [ ] **Step 3: Implement the three domain events**

`src/WardenId.Identity.Domain/Events/LoginFailedDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record LoginFailedDomainEvent(int FailedAttemptCount) : DomainEvent;
```

`src/WardenId.Identity.Domain/Events/AccountLockedOutDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record AccountLockedOutDomainEvent(DateTimeOffset LockedUntilUtc) : DomainEvent;
```

`src/WardenId.Identity.Domain/Events/UserUnlockedDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record UserUnlockedDomainEvent : DomainEvent;
```

- [ ] **Step 4: Add the three behaviors to `User`**

In `src/WardenId.Identity.Domain/User.cs`, add after `SetPassword`:

```csharp
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
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserLoginLockoutTests -v n`
Expected: PASS (6 tests)

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Domain/User.cs src/WardenId.Identity.Domain/Events/LoginFailedDomainEvent.cs src/WardenId.Identity.Domain/Events/AccountLockedOutDomainEvent.cs src/WardenId.Identity.Domain/Events/UserUnlockedDomainEvent.cs tests/WardenId.Identity.Domain.Tests/UserLoginLockoutTests.cs
git commit -m "feat: add User login lockout behaviors"
```

---

### Task 15: `User` lifecycle — `Disable`, `SoftDelete`

**Files:**
- Create: `src/WardenId.Identity.Domain/Events/UserDisabledDomainEvent.cs`
- Create: `src/WardenId.Identity.Domain/Events/UserSoftDeletedDomainEvent.cs`
- Modify: `src/WardenId.Identity.Domain/User.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/UserLifecycleTests.cs`

**Interfaces:**
- Consumes: `UserErrors.InvalidStatusTransition()`, `.DisableReasonRequired()` (Task 1).
- Produces: `User.Disable(string reason) -> Result`, `User.SoftDelete() -> Result`.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/UserLifecycleTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class UserLifecycleTests
{
    private static User CreateUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    [Fact]
    public void Disable_FailsWithoutReason()
    {
        var user = CreateUser();

        var result = user.Disable("");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Disable_SetsStatusAndRaisesEvent()
    {
        var user = CreateUser();

        var result = user.Disable("policy violation");

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.Contains(user.DomainEvents, e => e is UserDisabledDomainEvent d && d.Reason == "policy violation");
    }

    [Fact]
    public void Disable_FailsWhenAlreadyDisabled()
    {
        var user = CreateUser();
        user.Disable("first reason");

        var result = user.Disable("second reason");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void SoftDelete_SetsStatusAndRaisesEvent()
    {
        var user = CreateUser();

        var result = user.SoftDelete();

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.SoftDeleted, user.Status);
        Assert.Contains(user.DomainEvents, e => e is UserSoftDeletedDomainEvent);
    }

    [Fact]
    public void SoftDelete_FailsWhenAlreadySoftDeleted()
    {
        var user = CreateUser();
        user.SoftDelete();

        var result = user.SoftDelete();

        Assert.True(result.IsFailed);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserLifecycleTests -v n`
Expected: FAIL (methods don't exist)

- [ ] **Step 3: Implement the two domain events**

`src/WardenId.Identity.Domain/Events/UserDisabledDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record UserDisabledDomainEvent(string Reason) : DomainEvent;
```

`src/WardenId.Identity.Domain/Events/UserSoftDeletedDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record UserSoftDeletedDomainEvent : DomainEvent;
```

- [ ] **Step 4: Add `Disable` and `SoftDelete` to `User`**

In `src/WardenId.Identity.Domain/User.cs`, add after `Unlock`:

```csharp
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
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserLifecycleTests -v n`
Expected: PASS (5 tests)

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Domain/User.cs src/WardenId.Identity.Domain/Events/UserDisabledDomainEvent.cs src/WardenId.Identity.Domain/Events/UserSoftDeletedDomainEvent.cs tests/WardenId.Identity.Domain.Tests/UserLifecycleTests.cs
git commit -m "feat: add User.Disable and User.SoftDelete"
```

---

### Task 16: `MfaPolicyEvaluator` domain service + `User` MFA — `EnrollMfaFactor`, `RevokeMfaFactor`

**Files:**
- Create: `src/WardenId.Identity.Domain/MfaPolicyEvaluator.cs`
- Create: `src/WardenId.Identity.Domain/Events/MfaFactorEnrolledDomainEvent.cs`
- Create: `src/WardenId.Identity.Domain/Events/MfaFactorRevokedDomainEvent.cs`
- Modify: `src/WardenId.Identity.Domain/User.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/MfaPolicyEvaluatorTests.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/UserMfaTests.cs`

**Interfaces:**
- Consumes: `MfaPolicy`/`MfaPolicyMode` (Task 5, Task 1), `MfaFactor`/`RecoveryCode` (Task 9), `UserErrors.CannotRevokeLastRequiredFactor()`, `.MfaFactorNotFound()` (Task 1), `User` (Task 12).
- Produces: `MfaPolicyEvaluator.IsMfaRequired(MfaPolicy policy, bool userIsAdmin) -> bool`, `MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy policy, User user, MfaFactor factor, bool userIsAdmin) -> Result`, `User.EnrollMfaFactor(MfaFactor factor, IEnumerable<RecoveryCode> recoveryCodes) -> Result`, `User.RevokeMfaFactor(Guid factorId, MfaPolicy policy, bool userIsAdmin) -> Result`.

`MfaPolicyEvaluator` needs a `User` instance with enrolled factors to test `CanRevokeFactor`, so it's built in the same task as the `User` behaviors that use it, rather than earlier alongside the other domain service (`PasswordPolicyEvaluator`, Task 11) which has no such dependency.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/MfaPolicyEvaluatorTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class MfaPolicyEvaluatorTests
{
    [Theory]
    [InlineData(MfaPolicyMode.Off, false, false)]
    [InlineData(MfaPolicyMode.Optional, false, false)]
    [InlineData(MfaPolicyMode.Required, false, true)]
    [InlineData(MfaPolicyMode.Required, true, true)]
    [InlineData(MfaPolicyMode.RequiredForAdmins, false, false)]
    [InlineData(MfaPolicyMode.RequiredForAdmins, true, true)]
    public void IsMfaRequired_MatchesPolicyModeAndAdminStatus(MfaPolicyMode mode, bool userIsAdmin, bool expected)
    {
        var policy = MfaPolicy.Create(mode);

        Assert.Equal(expected, MfaPolicyEvaluator.IsMfaRequired(policy, userIsAdmin));
    }

    [Fact]
    public void CanRevokeFactor_SucceedsWhenPolicyDoesNotRequireMfa()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        user.EnrollMfaFactor(factor, []);

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.Optional), user, factor, userIsAdmin: false);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CanRevokeFactor_FailsWhenItIsTheOnlyEnabledFactorAndPolicyRequiresMfa()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        factor.Verify();
        user.EnrollMfaFactor(factor, []);

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.Required), user, factor, userIsAdmin: false);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void CanRevokeFactor_SucceedsWhenAnotherEnabledFactorRemains()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        var factorA = MfaFactor.Create(MfaFactorType.Totp, "secret-a");
        factorA.Verify();
        var factorB = MfaFactor.Create(MfaFactorType.Totp, "secret-b");
        factorB.Verify();
        user.EnrollMfaFactor(factorA, []);
        user.EnrollMfaFactor(factorB, []);

        var result = MfaPolicyEvaluator.CanRevokeFactor(MfaPolicy.Create(MfaPolicyMode.Required), user, factorA, userIsAdmin: false);

        Assert.True(result.IsSuccess);
    }
}
```

`tests/WardenId.Identity.Domain.Tests/UserMfaTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class UserMfaTests
{
    private static User CreateUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    [Fact]
    public void EnrollMfaFactor_AddsFactorAndRecoveryCodes()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        var codes = new[] { RecoveryCode.Create("hash-1"), RecoveryCode.Create("hash-2") };

        var result = user.EnrollMfaFactor(factor, codes);

        Assert.True(result.IsSuccess);
        Assert.Single(user.MfaFactors);
        Assert.Equal(2, user.RecoveryCodes.Count);
    }

    [Fact]
    public void EnrollMfaFactor_RaisesMfaFactorEnrolledDomainEvent()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");

        user.EnrollMfaFactor(factor, []);

        Assert.Contains(user.DomainEvents, e => e is MfaFactorEnrolledDomainEvent d && d.MfaFactorId == factor.Id);
    }

    [Fact]
    public void RevokeMfaFactor_FailsWhenFactorNotFound()
    {
        var user = CreateUser();

        var result = user.RevokeMfaFactor(Guid.NewGuid(), MfaPolicy.Create(MfaPolicyMode.Off), userIsAdmin: false);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void RevokeMfaFactor_SucceedsWhenPolicyDoesNotRequireMfa()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        user.EnrollMfaFactor(factor, []);

        var result = user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Optional), userIsAdmin: false);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.MfaFactors);
    }

    [Fact]
    public void RevokeMfaFactor_FailsWhenItIsTheLastFactorAndPolicyRequiresMfa()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        factor.Verify();
        user.EnrollMfaFactor(factor, []);

        var result = user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Required), userIsAdmin: false);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void RevokeMfaFactor_RaisesMfaFactorRevokedDomainEventOnSuccess()
    {
        var user = CreateUser();
        var factor = MfaFactor.Create(MfaFactorType.Totp, "encrypted-secret");
        user.EnrollMfaFactor(factor, []);

        user.RevokeMfaFactor(factor.Id, MfaPolicy.Create(MfaPolicyMode.Off), userIsAdmin: false);

        Assert.Contains(user.DomainEvents, e => e is MfaFactorRevokedDomainEvent d && d.MfaFactorId == factor.Id);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter "UserMfaTests|MfaPolicyEvaluatorTests" -v n`
Expected: FAIL (`MfaPolicyEvaluator`, `EnrollMfaFactor`/`RevokeMfaFactor` don't exist)

- [ ] **Step 3: Implement `MfaPolicyEvaluator`**

`src/WardenId.Identity.Domain/MfaPolicyEvaluator.cs`:

```csharp
using FluentResults;

namespace WardenId.Identity.Domain;

public static class MfaPolicyEvaluator
{
    public static bool IsMfaRequired(MfaPolicy policy, bool userIsAdmin) =>
        policy.Mode switch
        {
            MfaPolicyMode.Required => true,
            MfaPolicyMode.RequiredForAdmins => userIsAdmin,
            _ => false
        };

    public static Result CanRevokeFactor(MfaPolicy policy, User user, MfaFactor factor, bool userIsAdmin)
    {
        if (!IsMfaRequired(policy, userIsAdmin)) return Result.Ok();

        var remainingEnabledFactors = user.MfaFactors.Count(f => f.Enabled && f.Id != factor.Id);

        return remainingEnabledFactors == 0
            ? Result.Fail(UserErrors.CannotRevokeLastRequiredFactor())
            : Result.Ok();
    }
}
```

- [ ] **Step 4: Implement the two domain events**

`src/WardenId.Identity.Domain/Events/MfaFactorEnrolledDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record MfaFactorEnrolledDomainEvent(Guid MfaFactorId, MfaFactorType FactorType) : DomainEvent;
```

`src/WardenId.Identity.Domain/Events/MfaFactorRevokedDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record MfaFactorRevokedDomainEvent(Guid MfaFactorId) : DomainEvent;
```

- [ ] **Step 5: Add `EnrollMfaFactor` and `RevokeMfaFactor` to `User`**

In `src/WardenId.Identity.Domain/User.cs`, add after `SoftDelete`:

```csharp
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
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter "UserMfaTests|MfaPolicyEvaluatorTests" -v n`
Expected: PASS (9 tests in `MfaPolicyEvaluatorTests`: 6 `IsMfaRequired` cases + 3 `CanRevokeFactor` facts; 6 tests in `UserMfaTests`)

- [ ] **Step 7: Commit**

```bash
git add src/WardenId.Identity.Domain/User.cs src/WardenId.Identity.Domain/MfaPolicyEvaluator.cs src/WardenId.Identity.Domain/Events/MfaFactorEnrolledDomainEvent.cs src/WardenId.Identity.Domain/Events/MfaFactorRevokedDomainEvent.cs tests/WardenId.Identity.Domain.Tests/UserMfaTests.cs tests/WardenId.Identity.Domain.Tests/MfaPolicyEvaluatorTests.cs
git commit -m "feat: add MfaPolicyEvaluator domain service and User MFA enrollment/revocation"
```

---

### Task 17: `User` password reset — `RequestPasswordReset`, `ResetPassword`

**Files:**
- Create: `src/WardenId.Identity.Domain/Events/PasswordResetRequestedDomainEvent.cs`
- Create: `src/WardenId.Identity.Domain/Events/PasswordResetDomainEvent.cs`
- Modify: `src/WardenId.Identity.Domain/User.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/UserPasswordResetTests.cs`

**Interfaces:**
- Consumes: `PasswordResetToken` (Task 8), `PasswordCredential`/`PasswordPolicy` (Task 7, Task 4), `User.SetPassword` (Task 13).
- Produces: `User.RequestPasswordReset(PasswordResetToken token)`, `User.ResetPassword(string tokenHash, PasswordCredential newCredential, PasswordPolicy policy) -> Result`.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/UserPasswordResetTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class UserPasswordResetTests
{
    private static User CreateActiveUserWithCredential()
    {
        var user = User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;
        user.AddEmailVerificationToken(EmailVerificationToken.Create("verify-hash", DateTimeOffset.UtcNow.AddHours(1)));
        user.VerifyEmail("verify-hash");
        user.SetPassword(PasswordCredential.Create("hash-old", "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.Create(8, true, true, false, false, 2).Value);
        return user;
    }

    [Fact]
    public void RequestPasswordReset_AddsTokenAndRaisesEvent()
    {
        var user = CreateActiveUserWithCredential();

        user.RequestPasswordReset(PasswordResetToken.Create("reset-hash", DateTimeOffset.UtcNow.AddHours(1)));

        Assert.Single(user.PasswordResetTokens);
        Assert.Contains(user.DomainEvents, e => e is PasswordResetRequestedDomainEvent);
    }

    [Fact]
    public void ResetPassword_FailsWhenTokenDoesNotMatch()
    {
        var user = CreateActiveUserWithCredential();
        user.RequestPasswordReset(PasswordResetToken.Create("reset-hash", DateTimeOffset.UtcNow.AddHours(1)));

        var result = user.ResetPassword("wrong-hash", PasswordCredential.Create("hash-new", "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.Create(8, true, true, false, false, 2).Value);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void ResetPassword_SucceedsAndSetsNewCredential()
    {
        var user = CreateActiveUserWithCredential();
        user.RequestPasswordReset(PasswordResetToken.Create("reset-hash", DateTimeOffset.UtcNow.AddHours(1)));

        var result = user.ResetPassword("reset-hash", PasswordCredential.Create("hash-new", "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.Create(8, true, true, false, false, 2).Value);

        Assert.True(result.IsSuccess);
        Assert.Equal("hash-new", user.Credential!.Hash);
        Assert.Contains(user.DomainEvents, e => e is PasswordResetDomainEvent);
    }

    [Fact]
    public void ResetPassword_ClearsLockoutAndReactivatesLockedUser()
    {
        var user = CreateActiveUserWithCredential();
        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));
        }
        user.RequestPasswordReset(PasswordResetToken.Create("reset-hash", DateTimeOffset.UtcNow.AddHours(1)));

        var result = user.ResetPassword("reset-hash", PasswordCredential.Create("hash-new", "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.Create(8, true, true, false, false, 2).Value);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserPasswordResetTests -v n`
Expected: FAIL (methods don't exist)

- [ ] **Step 3: Implement the two domain events**

`src/WardenId.Identity.Domain/Events/PasswordResetRequestedDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record PasswordResetRequestedDomainEvent : DomainEvent;
```

`src/WardenId.Identity.Domain/Events/PasswordResetDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain.Events;

public sealed record PasswordResetDomainEvent : DomainEvent;
```

- [ ] **Step 4: Add `RequestPasswordReset` and `ResetPassword` to `User`**

In `src/WardenId.Identity.Domain/User.cs`, add after `RevokeMfaFactor`:

```csharp
    public void RequestPasswordReset(PasswordResetToken token)
    {
        _passwordResetTokens.Add(token);
        RaiseDomainEvent(new PasswordResetRequestedDomainEvent());
    }

    public Result ResetPassword(string tokenHash, PasswordCredential newCredential, PasswordPolicy policy)
    {
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
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserPasswordResetTests -v n`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Domain/User.cs src/WardenId.Identity.Domain/Events/PasswordResetRequestedDomainEvent.cs src/WardenId.Identity.Domain/Events/PasswordResetDomainEvent.cs tests/WardenId.Identity.Domain.Tests/UserPasswordResetTests.cs
git commit -m "feat: add User password reset flow"
```

---

### Task 18: `User` attributes and required actions

**Files:**
- Modify: `src/WardenId.Identity.Domain/User.cs`
- Test: `tests/WardenId.Identity.Domain.Tests/UserAttributesAndActionsTests.cs`

**Interfaces:**
- Consumes: `UserAttribute` (Task 10), `RequiredAction` (Task 1).
- Produces: `User.SetAttribute(string key, string value, UserAttributeValueType valueType) -> Result`, `User.AddRequiredAction(RequiredAction action)`, `User.ClearRequiredAction(RequiredAction action)`. No new domain events — pure bookkeeping per the spec.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Domain.Tests/UserAttributesAndActionsTests.cs`:

```csharp
namespace WardenId.Identity.Domain.Tests;

public class UserAttributesAndActionsTests
{
    private static User CreateUser() =>
        User.Register(Guid.NewGuid(), Email.Create("someone@example.com").Value, Username.Create("someone").Value).Value;

    [Fact]
    public void SetAttribute_AddsNewAttribute()
    {
        var user = CreateUser();

        var result = user.SetAttribute("department", "engineering", UserAttributeValueType.String);

        Assert.True(result.IsSuccess);
        Assert.Single(user.Attributes);
    }

    [Fact]
    public void SetAttribute_UpdatesExistingAttributeByKey()
    {
        var user = CreateUser();
        user.SetAttribute("department", "engineering", UserAttributeValueType.String);

        var result = user.SetAttribute("department", "sales", UserAttributeValueType.String);

        Assert.True(result.IsSuccess);
        Assert.Single(user.Attributes);
        Assert.Equal("sales", user.Attributes.Single().Value);
    }

    [Fact]
    public void SetAttribute_FailsOnEmptyKey()
    {
        var user = CreateUser();

        var result = user.SetAttribute("", "value", UserAttributeValueType.String);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void AddRequiredAction_DedupesOnRepeatedAdd()
    {
        var user = CreateUser();

        user.AddRequiredAction(RequiredAction.AcceptTos);
        user.AddRequiredAction(RequiredAction.AcceptTos);

        Assert.Single(user.PendingActions, a => a == RequiredAction.AcceptTos);
    }

    [Fact]
    public void ClearRequiredAction_RemovesActionIfPresent()
    {
        var user = CreateUser();
        user.AddRequiredAction(RequiredAction.AcceptTos);

        user.ClearRequiredAction(RequiredAction.AcceptTos);

        Assert.DoesNotContain(RequiredAction.AcceptTos, user.PendingActions);
    }

    [Fact]
    public void ClearRequiredAction_NoOpWhenActionNotPresent()
    {
        var user = CreateUser();

        user.ClearRequiredAction(RequiredAction.AcceptTos);

        Assert.DoesNotContain(RequiredAction.AcceptTos, user.PendingActions);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserAttributesAndActionsTests -v n`
Expected: FAIL (methods don't exist)

- [ ] **Step 3: Add the three behaviors to `User`**

In `src/WardenId.Identity.Domain/User.cs`, add after `ResetPassword`:

```csharp
    public Result SetAttribute(string key, string value, UserAttributeValueType valueType)
    {
        var existing = _attributes.FirstOrDefault(a => a.Key == key);
        if (existing is not null)
        {
            existing.UpdateValue(value, valueType);
            return Result.Ok();
        }

        var createResult = UserAttribute.Create(key, value, valueType);
        if (createResult.IsFailed) return createResult.ToResult();

        _attributes.Add(createResult.Value);
        return Result.Ok();
    }

    public void AddRequiredAction(RequiredAction action)
    {
        if (!_pendingActions.Contains(action)) _pendingActions.Add(action);
    }

    public void ClearRequiredAction(RequiredAction action) => _pendingActions.Remove(action);
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests --filter UserAttributesAndActionsTests -v n`
Expected: PASS (6 tests)

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.Identity.Domain/User.cs tests/WardenId.Identity.Domain.Tests/UserAttributesAndActionsTests.cs
git commit -m "feat: add User attribute bag and required-action tracking"
```

---

### Task 19: README + full solution verification

**Files:**
- Create: `src/WardenId.Identity.Domain/README.md`

**Interfaces:**
- None — documentation and final verification only.

- [ ] **Step 1: Write the README**

`src/WardenId.Identity.Domain/README.md`:

```markdown
# WardenId.Identity.Domain

Domain layer for the Identity & Credentials module (BRD M2, FR-ID-01 through FR-ID-13,
excluding FR-ID-14). See `docs/superpowers/specs/2026-07-26-identity-domain-design.md`
for the full design and `docs/superpowers/plans/2026-07-26-identity-domain.md` for how
it was built.

## Aggregate boundary

`User` is the sole aggregate root. Three boundary questions were resolved during design:

- **`PasswordCredential` is a child entity of `User`, not its own aggregate.** Rehash-on-login
  (FR-ID-02) and password changes both mutate `User` atomically in one transaction — there's
  no requirement for eventual consistency between a credential and its owning user.
- **`MfaFactor` (and its `RecoveryCode` batch) is a child entity of `User`, not an independent
  aggregate root.** Nothing in FR-ID-07/FR-ID-09 requires revoking a factor without loading
  the full `User`.
- **The notification port for FR-ID-13 (email delivery) does not live in this project.** It
  lives in `WardenId.SharedKernel.Application`, since Tenancy will likely need email delivery
  too (invites, lifecycle notices) — Identity isn't the only future consumer.

## Why each entity landed where it did

| Entity | Why a child entity, not a bare value object |
|---|---|
| `PasswordCredential` | Mutates in place over time (rehash replaces `Hash`/`AlgorithmVersion` on the same logical credential); `PasswordHistory` entries need to reference prior instances by identity. |
| `PasswordHistoryEntry` | A retired credential snapshot — needs its own identity/audit trail distinct from the live `Credential`. |
| `EmailVerificationToken` / `PasswordResetToken` | Kept as two separate classes (not a shared base) despite an identical shape today — their consumption side effects differ (reset also clears lockout), and a shared base would force them to change in lockstep later. |
| `MfaFactor` | Has its own lifecycle (`Create` → unverified/disabled → `Verify()` → enabled) independent of any single request. |
| `RecoveryCode` | One-time-use, needs individual consumption tracking (`UsedAtUtc`) — a plain string list on `User` couldn't express that. |
| `UserAttribute` | Typed key/value pairs need their own identity for update-in-place semantics (`UpdateValue`). |

## Explicitly out of scope in this pass

- `IUserRepository` — deferred to the future Identity Application/Infrastructure/Presentation
  pass, matching how `IClientRepository` was deferred out of `ClientManagement`'s Domain-only
  pass and added later in `ClientManagement.Application/Abstractions/`.
- FR-ID-14 (impersonation) — Won't-v1, skipped entirely.
- FR-ID-08 (WebAuthn/passkeys) — only the `MfaFactorType.WebAuthnPasskey` enum stub exists.
- `IPasswordHasher`/`IBreachedPasswordChecker` implementations — Infrastructure concern, not
  built here; this project only defines the ports.
```

- [ ] **Step 2: Run the full Identity test suite**

Run: `dotnet test tests/WardenId.Identity.Domain.Tests -v n`
Expected: PASS, all tests across every task in this plan.

- [ ] **Step 3: Build the whole solution to confirm nothing else broke**

Run: `dotnet build WardenId.slnx`
Expected: Build succeeds with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/WardenId.Identity.Domain/README.md
git commit -m "docs: add WardenId.Identity.Domain README"
```
