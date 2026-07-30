# Client Aggregate (Domain Layer) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build out the `Client` aggregate in `WardenId.ClientManagement.Domain` per `docs/superpowers/specs/2026-07-24-client-management-domain-design.md` — enums, value objects, child entities, aggregate behaviors, domain events, errors — with a new xunit test project since none exists yet.

**Architecture:** Follow `WardenId.Tenancy.Domain` conventions exactly: `Result<T>`-returning static factories, `Create`/`FromTrustedSource` split on value objects, error factories in a static `ClientErrors` class tagged with `ClientErrorCodes` metadata, domain events raised via `RaiseDomainEvent` in an `Events/` subfolder.

**Tech Stack:** .NET 10, FluentResults, xunit 2.9.3 + xunit.runner.visualstudio 3.1.5 + Microsoft.NET.Test.Sdk 18.8.1 (new test project, versions pinned centrally in `Directory.Packages.props`).

## Global Constraints

- Target framework `net10.0`, nullable + implicit usings enabled (inherited from root `Directory.Build.props` — do not redeclare in the new test project).
- `RestorePackagesWithLockFile=true` repo-wide — every new project needs `dotnet restore` run once to generate its `packages.lock.json`.
- Package versions go in `Directory.Packages.props` only; project `.csproj` files reference packages by name with no `Version=` attribute.
- Domain layer has zero framework dependencies — no EF Core, no MediatR in `WardenId.ClientManagement.Domain`.
- Value objects: `Create(raw)` validates for external input, `FromTrustedSource(value)` skips validation for EF materialization — never skip this split.
- Aggregate/entity `Id` comes from `SharedKernel.Domain.Entity` (`Guid.CreateVersion7()`), never manually assigned.
- Every state-changing aggregate behavior that matters for eventing (create, secret rotation/expiry, scope changes, disable) raises a `DomainEvent` via `RaiseDomainEvent` — pure collection mutators (add/remove redirect URI, CORS origin) do not.
- This pass is Domain-layer only — no Application/Infrastructure/Presentation projects, no `Scope`/`ApiResource`/`InitialAccessToken` aggregates, no `Properties` bag, no `SoftDelete()`. See spec's "Explicitly Out of Scope" section.

---

## File Structure

**New:**
- `tests/WardenId.ClientManagement.Domain.Tests/WardenId.ClientManagement.Domain.Tests.csproj`
- `tests/WardenId.ClientManagement.Domain.Tests/GrantTypeTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/ClientErrorsTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/ClientIdTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/RedirectUriTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/CorsOriginTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/TokenLifetimeOverridesTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/ClientSecretTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/ClientSigningKeyTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/ClientCreateTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/ClientUriCollectionsTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/ClientSecretBehaviorTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/ClientSigningKeyBehaviorTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/ClientScopeTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/ClientEnabledTests.cs`
- `tests/WardenId.ClientManagement.Domain.Tests/ClientTokenLifetimeTests.cs`
- `src/WardenId.ClientManagement.Domain/ClientErrorCodes.cs`
- `src/WardenId.ClientManagement.Domain/ClientErrors.cs`
- `src/WardenId.ClientManagement.Domain/ClientId.cs`
- `src/WardenId.ClientManagement.Domain/CorsOrigin.cs`
- `src/WardenId.ClientManagement.Domain/TokenLifetimeOverrides.cs`
- `src/WardenId.ClientManagement.Domain/ClientSecret.cs`
- `src/WardenId.ClientManagement.Domain/ClientSigningKey.cs`
- `src/WardenId.ClientManagement.Domain/ClientType.cs`
- `src/WardenId.ClientManagement.Domain/ClientAuthenticationMethod.cs`
- `src/WardenId.ClientManagement.Domain/Events/ClientCreatedDomainEvent.cs`
- `src/WardenId.ClientManagement.Domain/Events/ClientSecretRotatedDomainEvent.cs`
- `src/WardenId.ClientManagement.Domain/Events/ClientSecretExpiredDomainEvent.cs`
- `src/WardenId.ClientManagement.Domain/Events/ClientScopesChangedDomainEvent.cs`
- `src/WardenId.ClientManagement.Domain/Events/ClientDisabledDomainEvent.cs`

**Modify:**
- `Directory.Packages.props` — add xunit stack package versions
- `WardenId.slnx` — register new test project under `/tests/`
- `src/WardenId.ClientManagement.Domain/GrantType.cs` — fix to `[Flags]` power-of-two values
- `src/WardenId.ClientManagement.Domain/RedirectUri.cs` — replace stub with validated VO
- `src/WardenId.ClientManagement.Domain/Client.cs` — replace stub with full aggregate

---

### Task 1: Test project scaffold + fix `GrantType` to `[Flags]`

**Files:**
- Create: `tests/WardenId.ClientManagement.Domain.Tests/WardenId.ClientManagement.Domain.Tests.csproj`
- Create: `tests/WardenId.ClientManagement.Domain.Tests/GrantTypeTests.cs`
- Modify: `Directory.Packages.props`
- Modify: `WardenId.slnx`
- Modify: `src/WardenId.ClientManagement.Domain/GrantType.cs`

**Interfaces:**
- Produces: `GrantType` enum with `[Flags]`, values `AuthorizationCode = 1, ClientCredentials = 2, RefreshToken = 4, DeviceCode = 8` — every later task that references `GrantType` uses these names/values.

- [ ] **Step 1: Add xunit stack to `Directory.Packages.props`**

Add inside the existing `<ItemGroup>`, alphabetically:

```xml
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
```

- [ ] **Step 2: Create the test project file**

`tests/WardenId.ClientManagement.Domain.Tests/WardenId.ClientManagement.Domain.Tests.csproj`:

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
    <ProjectReference Include="..\..\src\WardenId.ClientManagement.Domain\WardenId.ClientManagement.Domain.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="WardenId.ClientManagement.Domain" />
    <Using Include="WardenId.ClientManagement.Domain.Events" />
  </ItemGroup>

</Project>
```

`<Using Include="..." />` adds a project-wide global using — every test file references `Client`, `ClientId`, `GrantType`, domain events, etc. with no per-file `using` statement, so this is required for every subsequent task's test file to compile.

- [ ] **Step 3: Register the project in `WardenId.slnx`**

Replace:

```xml
  <Folder Name="/tests/" />
```

With:

```xml
  <Folder Name="/tests/">
    <Project Path="tests/WardenId.ClientManagement.Domain.Tests/WardenId.ClientManagement.Domain.Tests.csproj" />
  </Folder>
```

- [ ] **Step 4: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/GrantTypeTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class GrantTypeTests
{
    [Fact]
    public void Values_AreBitFlags_AndCanBeCombinedWithoutOverlap()
    {
        var combined = GrantType.AuthorizationCode | GrantType.RefreshToken;

        Assert.True(combined.HasFlag(GrantType.AuthorizationCode));
        Assert.True(combined.HasFlag(GrantType.RefreshToken));
        Assert.False(combined.HasFlag(GrantType.ClientCredentials));
    }
}
```

- [ ] **Step 5: Restore and run to verify it fails**

Run: `dotnet restore tests/WardenId.ClientManagement.Domain.Tests/WardenId.ClientManagement.Domain.Tests.csproj`
Then: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter GrantTypeTests -v n`
Expected: FAIL — `combined.HasFlag(GrantType.ClientCredentials)` is `true` (current stub has `RefreshToken = 3`, whose bits overlap `ClientCredentials = 2`), so `Assert.False` fails.

- [ ] **Step 6: Fix `GrantType`**

`src/WardenId.ClientManagement.Domain/GrantType.cs`:

```csharp
namespace WardenId.ClientManagement.Domain;

[Flags]
public enum GrantType
{
    AuthorizationCode = 1,
    ClientCredentials = 2,
    RefreshToken = 4,
    DeviceCode = 8
}
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter GrantTypeTests -v n`
Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add Directory.Packages.props WardenId.slnx tests/WardenId.ClientManagement.Domain.Tests src/WardenId.ClientManagement.Domain/GrantType.cs
git commit -m "test: scaffold ClientManagement.Domain test project, fix GrantType to Flags"
```

---

### Task 2: `ClientErrorCodes` + `ClientErrors`

**Files:**
- Create: `src/WardenId.ClientManagement.Domain/ClientErrorCodes.cs`
- Create: `src/WardenId.ClientManagement.Domain/ClientErrors.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/ClientErrorsTests.cs`

**Interfaces:**
- Consumes: nothing new (FluentResults `Error`, already a project dependency via `WardenId.Tenancy.Domain`'s pattern — must add `FluentResults` PackageReference to `WardenId.ClientManagement.Domain.csproj` in this task since the Domain project doesn't reference it yet).
- Produces: `ClientErrorCodes.MetadataKey/NotFound/Validation` (strings), `ClientErrors.NotFound` (`Error` property), `ClientErrors.Validation(string reason)`, and every specific factory used by later tasks: `RedirectUriEmpty()`, `RedirectUriMustBeAbsolute()`, `RedirectUriCannotContainFragment()`, `RedirectUriCannotContainWildcard()`, `CorsOriginEmpty()`, `CorsOriginInvalidFormat()`, `CorsOriginCannotContainPath()`, `TokenLifetimeMustBePositive(string fieldName)`, `ClientDisplayNameRequired()`, `ClientDisplayNameTooLong()`, `PublicClientCannotHaveSecret()`, `SecretNotFound()`, `SigningKeyRequiresPrivateKeyJwt()` — all return `Error`.

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/ClientErrorsTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class ClientErrorsTests
{
    [Fact]
    public void Validation_SetsValidationErrorCodeMetadata()
    {
        var error = ClientErrors.Validation("some reason");

        Assert.Equal("some reason", error.Message);
        Assert.Equal(ClientErrorCodes.Validation, error.Metadata[ClientErrorCodes.MetadataKey]);
    }

    [Fact]
    public void NotFound_SetsNotFoundErrorCodeMetadata()
    {
        var error = ClientErrors.NotFound;

        Assert.Equal(ClientErrorCodes.NotFound, error.Metadata[ClientErrorCodes.MetadataKey]);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientErrorsTests -v n`
Expected: FAIL — build error, `ClientErrors`/`ClientErrorCodes` do not exist.

- [ ] **Step 3: Add `FluentResults` to the Domain project**

Modify `src/WardenId.ClientManagement.Domain/WardenId.ClientManagement.Domain.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
      <PackageReference Include="FluentResults" />
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include="..\WardenId.Tenancy.Domain\WardenId.Tenancy.Domain.csproj" />
    </ItemGroup>

</Project>
```

- [ ] **Step 4: Implement `ClientErrorCodes`**

`src/WardenId.ClientManagement.Domain/ClientErrorCodes.cs`:

```csharp
namespace WardenId.ClientManagement.Domain;

public static class ClientErrorCodes
{
    public const string MetadataKey = "ErrorCode";

    public const string NotFound = "NotFound";
    public const string Validation = "Validation";
}
```

- [ ] **Step 5: Implement `ClientErrors`**

`src/WardenId.ClientManagement.Domain/ClientErrors.cs`:

```csharp
using FluentResults;

namespace WardenId.ClientManagement.Domain;

public static class ClientErrors
{
    public static Error NotFound =>
        new Error("Client not found.")
            .WithMetadata(ClientErrorCodes.MetadataKey, ClientErrorCodes.NotFound);

    public static Error Validation(string reason)
        => new Error(reason)
            .WithMetadata(ClientErrorCodes.MetadataKey, ClientErrorCodes.Validation);

    public static Error RedirectUriEmpty()
        => Validation("Redirect URI is required.");

    public static Error RedirectUriMustBeAbsolute()
        => Validation("Redirect URI must be an absolute URI.");

    public static Error RedirectUriCannotContainFragment()
        => Validation("Redirect URI cannot contain a fragment.");

    public static Error RedirectUriCannotContainWildcard()
        => Validation("Redirect URI cannot contain a wildcard.");

    public static Error CorsOriginEmpty()
        => Validation("CORS origin is required.");

    public static Error CorsOriginInvalidFormat()
        => Validation("CORS origin must be a valid origin (scheme and host only).");

    public static Error CorsOriginCannotContainPath()
        => Validation("CORS origin cannot contain a path, query, or fragment.");

    public static Error TokenLifetimeMustBePositive(string fieldName)
        => Validation($"{fieldName} must be a positive number of seconds.");

    public static Error ClientDisplayNameRequired()
        => Validation("Client display name is required.");

    public static Error ClientDisplayNameTooLong()
        => Validation("Client display name cannot exceed 200 characters.");

    public static Error PublicClientCannotHaveSecret()
        => Validation("Public clients cannot have client secrets.");

    public static Error SecretNotFound()
        => Validation("Client secret not found.");

    public static Error SigningKeyRequiresPrivateKeyJwt()
        => Validation("Signing keys can only be added to clients using the PrivateKeyJwt authentication method.");
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientErrorsTests -v n`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add Directory.Packages.props src/WardenId.ClientManagement.Domain tests/WardenId.ClientManagement.Domain.Tests/ClientErrorsTests.cs
git commit -m "feat: add ClientErrorCodes/ClientErrors"
```

---

### Task 3: `ClientId` value object

**Files:**
- Create: `src/WardenId.ClientManagement.Domain/ClientId.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/ClientIdTests.cs`

**Interfaces:**
- Consumes: `WardenId.SharedKernel.Domain.ValueObject`
- Produces: `ClientId.Generate()` → `ClientId`, `ClientId.FromTrustedSource(string value)` → `ClientId`, `ClientId.Value` (`string`, get-only). Used by `Client.Create` (Task 9).

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/ClientIdTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class ClientIdTests
{
    [Fact]
    public void Generate_ProducesA32CharacterValue()
    {
        var clientId = ClientId.Generate();

        Assert.Equal(32, clientId.Value.Length);
    }

    [Fact]
    public void Generate_ProducesDistinctValuesAcrossCalls()
    {
        var first = ClientId.Generate();
        var second = ClientId.Generate();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void FromTrustedSource_ThrowsOnNullOrEmpty()
    {
        Assert.Throws<ArgumentException>(() => ClientId.FromTrustedSource(""));
    }

    [Fact]
    public void FromTrustedSource_RoundTripsTheValue()
    {
        var clientId = ClientId.FromTrustedSource("abc123");

        Assert.Equal("abc123", clientId.Value);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientIdTests -v n`
Expected: FAIL — build error, `ClientId` does not exist.

- [ ] **Step 3: Implement `ClientId`**

`src/WardenId.ClientManagement.Domain/ClientId.cs`:

```csharp
using System.Security.Cryptography;
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class ClientId : ValueObject
{
    public const int Length = 32;

    private const string Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    public string Value { get; }

    private ClientId(string value) => Value = value;

    public static ClientId Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(Length);
        var chars = new char[Length];

        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new ClientId(new string(chars));
    }

    public static ClientId FromTrustedSource(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Client id cannot be null or empty.", nameof(value))
            : new ClientId(value);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientIdTests -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.ClientManagement.Domain/ClientId.cs tests/WardenId.ClientManagement.Domain.Tests/ClientIdTests.cs
git commit -m "feat: add ClientId value object"
```

---

### Task 4: `RedirectUri` value object (replace stub)

**Files:**
- Modify: `src/WardenId.ClientManagement.Domain/RedirectUri.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/RedirectUriTests.cs`

**Interfaces:**
- Consumes: `ClientErrors.RedirectUriEmpty()`, `RedirectUriMustBeAbsolute()`, `RedirectUriCannotContainFragment()`, `RedirectUriCannotContainWildcard()` (Task 2)
- Produces: `RedirectUri.Create(string? raw)` → `Result<RedirectUri>`, `RedirectUri.FromTrustedSource(string value)` → `RedirectUri`, `RedirectUri.Value` (`string`). Used for both `RedirectUris` and `PostLogoutRedirectUris` on `Client` (Task 9/10).

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/RedirectUriTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class RedirectUriTests
{
    [Fact]
    public void Create_FailsOnEmpty()
    {
        var result = RedirectUri.Create("");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsOnRelativeUri()
    {
        var result = RedirectUri.Create("/callback");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsOnFragment()
    {
        var result = RedirectUri.Create("https://app.example.com/callback#token");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsOnWildcard()
    {
        var result = RedirectUri.Create("https://*.example.com/callback");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_SucceedsOnValidAbsoluteUri()
    {
        var result = RedirectUri.Create("https://app.example.com/callback");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://app.example.com/callback", result.Value.Value);
    }

    [Fact]
    public void TwoInstancesWithSameValue_AreEqual()
    {
        var first = RedirectUri.Create("https://app.example.com/callback").Value;
        var second = RedirectUri.Create("https://app.example.com/callback").Value;

        Assert.Equal(first, second);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter RedirectUriTests -v n`
Expected: FAIL — current stub has no `Create`/`FromTrustedSource`, build error.

- [ ] **Step 3: Implement `RedirectUri`**

Replace `src/WardenId.ClientManagement.Domain/RedirectUri.cs` entirely:

```csharp
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class RedirectUri : ValueObject
{
    public string Value { get; }

    private RedirectUri(string value) => Value = value;

    public static Result<RedirectUri> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Result.Fail(ClientErrors.RedirectUriEmpty());

        var candidate = raw.Trim();

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            return Result.Fail(ClientErrors.RedirectUriMustBeAbsolute());

        if (!string.IsNullOrEmpty(uri.Fragment))
            return Result.Fail(ClientErrors.RedirectUriCannotContainFragment());

        if (candidate.Contains('*'))
            return Result.Fail(ClientErrors.RedirectUriCannotContainWildcard());

        return Result.Ok(new RedirectUri(candidate));
    }

    public static RedirectUri FromTrustedSource(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Redirect uri cannot be null or empty.", nameof(value))
            : new RedirectUri(value);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter RedirectUriTests -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.ClientManagement.Domain/RedirectUri.cs tests/WardenId.ClientManagement.Domain.Tests/RedirectUriTests.cs
git commit -m "feat: validate RedirectUri (absolute, no fragment, no wildcard)"
```

---

### Task 5: `CorsOrigin` value object

**Files:**
- Create: `src/WardenId.ClientManagement.Domain/CorsOrigin.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/CorsOriginTests.cs`

**Interfaces:**
- Consumes: `ClientErrors.CorsOriginEmpty()`, `CorsOriginInvalidFormat()`, `CorsOriginCannotContainPath()` (Task 2)
- Produces: `CorsOrigin.Create(string? raw)` → `Result<CorsOrigin>`, `CorsOrigin.FromTrustedSource(string value)` → `CorsOrigin`, `CorsOrigin.Value` (`string`). Used by `Client.CorsOrigins` (Task 10).

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/CorsOriginTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class CorsOriginTests
{
    [Fact]
    public void Create_FailsOnEmpty()
    {
        var result = CorsOrigin.Create("");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsOnRelativeValue()
    {
        var result = CorsOrigin.Create("example.com");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenPathIncluded()
    {
        var result = CorsOrigin.Create("https://app.example.com/some/path");

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_SucceedsOnBareOrigin()
    {
        var result = CorsOrigin.Create("https://app.example.com");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://app.example.com", result.Value.Value);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter CorsOriginTests -v n`
Expected: FAIL — build error, `CorsOrigin` does not exist.

- [ ] **Step 3: Implement `CorsOrigin`**

`src/WardenId.ClientManagement.Domain/CorsOrigin.cs`:

```csharp
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class CorsOrigin : ValueObject
{
    public string Value { get; }

    private CorsOrigin(string value) => Value = value;

    public static Result<CorsOrigin> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Result.Fail(ClientErrors.CorsOriginEmpty());

        var candidate = raw.Trim();

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            return Result.Fail(ClientErrors.CorsOriginInvalidFormat());

        if (uri.PathAndQuery != "/" || !string.IsNullOrEmpty(uri.Fragment))
            return Result.Fail(ClientErrors.CorsOriginCannotContainPath());

        return Result.Ok(new CorsOrigin(candidate));
    }

    public static CorsOrigin FromTrustedSource(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Cors origin cannot be null or empty.", nameof(value))
            : new CorsOrigin(value);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter CorsOriginTests -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.ClientManagement.Domain/CorsOrigin.cs tests/WardenId.ClientManagement.Domain.Tests/CorsOriginTests.cs
git commit -m "feat: add CorsOrigin value object"
```

---

### Task 6: `TokenLifetimeOverrides` value object

**Files:**
- Create: `src/WardenId.ClientManagement.Domain/TokenLifetimeOverrides.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/TokenLifetimeOverridesTests.cs`

**Interfaces:**
- Consumes: `ClientErrors.TokenLifetimeMustBePositive(string fieldName)` (Task 2)
- Produces: `TokenLifetimeOverrides.Create(int? accessTokenSeconds, int? idTokenSeconds, int? refreshTokenSeconds)` → `Result<TokenLifetimeOverrides>`, `.FromTrustedSource(...)` → `TokenLifetimeOverrides`, properties `AccessTokenSeconds`/`IdTokenSeconds`/`RefreshTokenSeconds` (all `int?`). Used by `Client.TokenLifetimeOverrides` (Task 9) and `UpdateTokenLifetimeOverrides` (Task 15).

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/TokenLifetimeOverridesTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class TokenLifetimeOverridesTests
{
    [Fact]
    public void Create_FailsWhenAccessTokenSecondsIsNotPositive()
    {
        var result = TokenLifetimeOverrides.Create(0, null, null);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenIdTokenSecondsIsNotPositive()
    {
        var result = TokenLifetimeOverrides.Create(null, -5, null);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenRefreshTokenSecondsIsNotPositive()
    {
        var result = TokenLifetimeOverrides.Create(null, null, 0);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_SucceedsWhenAllNull()
    {
        var result = TokenLifetimeOverrides.Create(null, null, null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_SucceedsWithPositiveValues()
    {
        var result = TokenLifetimeOverrides.Create(3600, 3600, 86400);

        Assert.True(result.IsSuccess);
        Assert.Equal(3600, result.Value.AccessTokenSeconds);
        Assert.Equal(86400, result.Value.RefreshTokenSeconds);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter TokenLifetimeOverridesTests -v n`
Expected: FAIL — build error, `TokenLifetimeOverrides` does not exist.

- [ ] **Step 3: Implement `TokenLifetimeOverrides`**

`src/WardenId.ClientManagement.Domain/TokenLifetimeOverrides.cs`:

```csharp
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class TokenLifetimeOverrides : ValueObject
{
    public int? AccessTokenSeconds { get; }
    public int? IdTokenSeconds { get; }
    public int? RefreshTokenSeconds { get; }

    private TokenLifetimeOverrides(int? accessTokenSeconds, int? idTokenSeconds, int? refreshTokenSeconds)
    {
        AccessTokenSeconds = accessTokenSeconds;
        IdTokenSeconds = idTokenSeconds;
        RefreshTokenSeconds = refreshTokenSeconds;
    }

    public static Result<TokenLifetimeOverrides> Create(
        int? accessTokenSeconds, int? idTokenSeconds, int? refreshTokenSeconds)
    {
        if (accessTokenSeconds is <= 0)
            return Result.Fail(ClientErrors.TokenLifetimeMustBePositive(nameof(accessTokenSeconds)));

        if (idTokenSeconds is <= 0)
            return Result.Fail(ClientErrors.TokenLifetimeMustBePositive(nameof(idTokenSeconds)));

        if (refreshTokenSeconds is <= 0)
            return Result.Fail(ClientErrors.TokenLifetimeMustBePositive(nameof(refreshTokenSeconds)));

        return Result.Ok(new TokenLifetimeOverrides(accessTokenSeconds, idTokenSeconds, refreshTokenSeconds));
    }

    public static TokenLifetimeOverrides FromTrustedSource(
        int? accessTokenSeconds, int? idTokenSeconds, int? refreshTokenSeconds)
        => new(accessTokenSeconds, idTokenSeconds, refreshTokenSeconds);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AccessTokenSeconds;
        yield return IdTokenSeconds;
        yield return RefreshTokenSeconds;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter TokenLifetimeOverridesTests -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.ClientManagement.Domain/TokenLifetimeOverrides.cs tests/WardenId.ClientManagement.Domain.Tests/TokenLifetimeOverridesTests.cs
git commit -m "feat: add TokenLifetimeOverrides value object"
```

---

### Task 7: `ClientSecret` child entity

**Files:**
- Create: `src/WardenId.ClientManagement.Domain/ClientSecret.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/ClientSecretTests.cs`

**Interfaces:**
- Consumes: `WardenId.SharedKernel.Domain.Entity` (gives `Id`)
- Produces: `ClientSecret.Create(string hashedValue, string label, DateTimeOffset? expiresAt)` → `ClientSecret`, `.HashedValue`/`.Label`/`.ExpiresAt` (get-only), `.SetExpiry(DateTimeOffset expiresAt)`, `.IsExpired(DateTimeOffset asOf)` → `bool`. Used by `Client.AddSecret`/`RotateSecret`/`ExpireSecret` (Task 11).

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/ClientSecretTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class ClientSecretTests
{
    [Fact]
    public void Create_SetsProvidedValues()
    {
        var secret = ClientSecret.Create("hashed", "primary", expiresAt: null);

        Assert.Equal("hashed", secret.HashedValue);
        Assert.Equal("primary", secret.Label);
        Assert.Null(secret.ExpiresAt);
    }

    [Fact]
    public void IsExpired_FalseWhenNoExpiry()
    {
        var secret = ClientSecret.Create("hashed", "primary", expiresAt: null);

        Assert.False(secret.IsExpired(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsExpired_TrueWhenPastExpiry()
    {
        var secret = ClientSecret.Create("hashed", "primary", DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.True(secret.IsExpired(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SetExpiry_UpdatesExpiresAt()
    {
        var secret = ClientSecret.Create("hashed", "primary", expiresAt: null);
        var expiry = DateTimeOffset.UtcNow.AddDays(1);

        secret.SetExpiry(expiry);

        Assert.Equal(expiry, secret.ExpiresAt);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientSecretTests -v n`
Expected: FAIL — build error, `ClientSecret` does not exist.

- [ ] **Step 3: Implement `ClientSecret`**

`src/WardenId.ClientManagement.Domain/ClientSecret.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class ClientSecret : Entity
{
    // EF Core materialization only
    private ClientSecret() { }

    private ClientSecret(string hashedValue, string label, DateTimeOffset? expiresAt)
    {
        HashedValue = hashedValue;
        Label = label;
        ExpiresAt = expiresAt;
    }

    public string HashedValue { get; private set; }
    public string Label { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsExpired(DateTimeOffset asOf) => ExpiresAt is not null && ExpiresAt <= asOf;

    public static ClientSecret Create(string hashedValue, string label, DateTimeOffset? expiresAt) =>
        new(hashedValue, label, expiresAt);

    public void SetExpiry(DateTimeOffset expiresAt) => ExpiresAt = expiresAt;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientSecretTests -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.ClientManagement.Domain/ClientSecret.cs tests/WardenId.ClientManagement.Domain.Tests/ClientSecretTests.cs
git commit -m "feat: add ClientSecret child entity"
```

---

### Task 8: `ClientSigningKey` child entity

**Files:**
- Create: `src/WardenId.ClientManagement.Domain/ClientSigningKey.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/ClientSigningKeyTests.cs`

**Interfaces:**
- Consumes: `WardenId.SharedKernel.Domain.Entity`
- Produces: `ClientSigningKey.Create(string kid, string publicKeyJwk, string algorithm, DateTimeOffset? expiresAt)` → `ClientSigningKey`, `.Kid`/`.PublicKeyJwk`/`.Algorithm`/`.ExpiresAt` (get-only). Used by `Client.AddSigningKey` (Task 12).

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/ClientSigningKeyTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class ClientSigningKeyTests
{
    [Fact]
    public void Create_SetsProvidedValues()
    {
        var key = ClientSigningKey.Create("kid-1", "{\"kty\":\"EC\"}", "ES256", expiresAt: null);

        Assert.Equal("kid-1", key.Kid);
        Assert.Equal("{\"kty\":\"EC\"}", key.PublicKeyJwk);
        Assert.Equal("ES256", key.Algorithm);
        Assert.Null(key.ExpiresAt);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientSigningKeyTests -v n`
Expected: FAIL — build error, `ClientSigningKey` does not exist.

- [ ] **Step 3: Implement `ClientSigningKey`**

`src/WardenId.ClientManagement.Domain/ClientSigningKey.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class ClientSigningKey : Entity
{
    // EF Core materialization only
    private ClientSigningKey() { }

    private ClientSigningKey(string kid, string publicKeyJwk, string algorithm, DateTimeOffset? expiresAt)
    {
        Kid = kid;
        PublicKeyJwk = publicKeyJwk;
        Algorithm = algorithm;
        ExpiresAt = expiresAt;
    }

    public string Kid { get; private set; }
    public string PublicKeyJwk { get; private set; }
    public string Algorithm { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    public static ClientSigningKey Create(
        string kid, string publicKeyJwk, string algorithm, DateTimeOffset? expiresAt) =>
        new(kid, publicKeyJwk, algorithm, expiresAt);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientSigningKeyTests -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.ClientManagement.Domain/ClientSigningKey.cs tests/WardenId.ClientManagement.Domain.Tests/ClientSigningKeyTests.cs
git commit -m "feat: add ClientSigningKey child entity"
```

---

### Task 9: `ClientType`/`ClientAuthenticationMethod` enums + `Client` aggregate skeleton + `Create`

**Files:**
- Create: `src/WardenId.ClientManagement.Domain/ClientType.cs`
- Create: `src/WardenId.ClientManagement.Domain/ClientAuthenticationMethod.cs`
- Create: `src/WardenId.ClientManagement.Domain/Events/ClientCreatedDomainEvent.cs`
- Modify: `src/WardenId.ClientManagement.Domain/Client.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/ClientCreateTests.cs`

**Interfaces:**
- Consumes: `ClientId.Generate()` (Task 3), `ClientErrors.ClientDisplayNameRequired()`/`ClientDisplayNameTooLong()` (Task 2), `GrantType` (Task 1)
- Produces: `ClientType { Confidential, Public }`, `ClientAuthenticationMethod { SharedSecret, PrivateKeyJwt }`, `Client.Create(Guid tenantId, ClientType clientType, string displayName, GrantType allowedGrantTypes, ClientAuthenticationMethod clientAuthenticationMethod, bool isFirstParty, bool consentRequired, string? description = null)` → `Result<Client>`. Properties `TenantId`, `ClientId`, `ClientType`, `DisplayName`, `Description`, `Enabled`, `IsFirstParty`, `AllowedGrantTypes`, `RequirePkce`, `TokenLifetimeOverrides`, `ClientAuthenticationMethod`, `ConsentRequired` — all consumed by Tasks 10-15.

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/ClientCreateTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class ClientCreateTests
{
    [Fact]
    public void Create_FailsOnEmptyDisplayName()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_FailsWhenDisplayNameTooLong()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, new string('a', 201), GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_ForcesPkceForPublicClients()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Public, "SPA client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.RequirePkce);
    }

    [Fact]
    public void Create_DoesNotForcePkceForConfidentialClients()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Backend client", GrantType.ClientCredentials,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.RequirePkce);
    }

    [Fact]
    public void Create_AssignsAGeneratedClientId()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Backend client", GrantType.ClientCredentials,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.Equal(32, result.Value.ClientId.Value.Length);
    }

    [Fact]
    public void Create_RaisesClientCreatedDomainEvent()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Backend client", GrantType.ClientCredentials,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        var domainEvent = Assert.Single(result.Value.DomainEvents);
        Assert.IsType<ClientCreatedDomainEvent>(domainEvent);
    }

    [Fact]
    public void Create_DefaultsEnabledToTrue()
    {
        var result = Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Backend client", GrantType.ClientCredentials,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true);

        Assert.True(result.Value.Enabled);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientCreateTests -v n`
Expected: FAIL — build error, `ClientType`/`ClientAuthenticationMethod`/`Client.Create` do not exist yet.

- [ ] **Step 3: Add the enums**

`src/WardenId.ClientManagement.Domain/ClientType.cs`:

```csharp
namespace WardenId.ClientManagement.Domain;

public enum ClientType
{
    Confidential = 1,
    Public = 2
}
```

`src/WardenId.ClientManagement.Domain/ClientAuthenticationMethod.cs`:

```csharp
namespace WardenId.ClientManagement.Domain;

public enum ClientAuthenticationMethod
{
    SharedSecret = 1,
    PrivateKeyJwt = 2
}
```

- [ ] **Step 4: Add the domain event**

`src/WardenId.ClientManagement.Domain/Events/ClientCreatedDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain.Events;

public sealed record ClientCreatedDomainEvent(ClientId ClientId) : DomainEvent;
```

- [ ] **Step 5: Implement the `Client` aggregate skeleton + `Create`**

Replace `src/WardenId.ClientManagement.Domain/Client.cs` entirely:

```csharp
using FluentResults;
using WardenId.ClientManagement.Domain.Events;
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class Client : AggregateRoot
{
    private Client(
        Guid tenantId,
        ClientId clientId,
        ClientType clientType,
        string displayName,
        string? description,
        GrantType allowedGrantTypes,
        ClientAuthenticationMethod clientAuthenticationMethod,
        bool requirePkce,
        bool isFirstParty,
        bool consentRequired)
    {
        TenantId = tenantId;
        ClientId = clientId;
        ClientType = clientType;
        DisplayName = displayName;
        Description = description;
        AllowedGrantTypes = allowedGrantTypes;
        ClientAuthenticationMethod = clientAuthenticationMethod;
        RequirePkce = requirePkce;
        IsFirstParty = isFirstParty;
        ConsentRequired = consentRequired;
        Enabled = true;
    }

    public Guid TenantId { get; private set; }
    public ClientId ClientId { get; private set; }
    public ClientType ClientType { get; private set; }
    public string DisplayName { get; private set; }
    public string? Description { get; private set; }
    public bool Enabled { get; private set; }
    public bool IsFirstParty { get; private set; }
    public GrantType AllowedGrantTypes { get; private set; }
    public bool RequirePkce { get; private set; }
    public TokenLifetimeOverrides? TokenLifetimeOverrides { get; private set; }
    public ClientAuthenticationMethod ClientAuthenticationMethod { get; private set; }
    public bool ConsentRequired { get; private set; }

    private readonly List<RedirectUri> _redirectUris = new();
    public IReadOnlyCollection<RedirectUri> RedirectUris => _redirectUris.AsReadOnly();

    private readonly List<RedirectUri> _postLogoutRedirectUris = new();
    public IReadOnlyCollection<RedirectUri> PostLogoutRedirectUris => _postLogoutRedirectUris.AsReadOnly();

    private readonly List<CorsOrigin> _corsOrigins = new();
    public IReadOnlyCollection<CorsOrigin> CorsOrigins => _corsOrigins.AsReadOnly();

    private readonly List<ClientSecret> _secrets = new();
    public IReadOnlyCollection<ClientSecret> Secrets => _secrets.AsReadOnly();

    private readonly List<ClientSigningKey> _signingKeys = new();
    public IReadOnlyCollection<ClientSigningKey> SigningKeys => _signingKeys.AsReadOnly();

    private readonly List<Guid> _assignedScopeIds = new();
    public IReadOnlyCollection<Guid> AssignedScopeIds => _assignedScopeIds.AsReadOnly();

    public static Result<Client> Create(
        Guid tenantId,
        ClientType clientType,
        string displayName,
        GrantType allowedGrantTypes,
        ClientAuthenticationMethod clientAuthenticationMethod,
        bool isFirstParty,
        bool consentRequired,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return Result.Fail(ClientErrors.ClientDisplayNameRequired());

        var trimmed = displayName.Trim();

        if (trimmed.Length > 200) return Result.Fail(ClientErrors.ClientDisplayNameTooLong());

        var requirePkce = clientType == ClientType.Public;

        var client = new Client(
            tenantId,
            ClientId.Generate(),
            clientType,
            trimmed,
            description,
            allowedGrantTypes,
            clientAuthenticationMethod,
            requirePkce,
            isFirstParty,
            consentRequired);

        client.RaiseDomainEvent(new ClientCreatedDomainEvent(client.ClientId));
        return Result.Ok(client);
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientCreateTests -v n`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add src/WardenId.ClientManagement.Domain tests/WardenId.ClientManagement.Domain.Tests/ClientCreateTests.cs
git commit -m "feat: add Client aggregate skeleton with Create factory"
```

---

### Task 10: Redirect/post-logout/CORS collection mutators + `ValidateRedirectUri`

**Files:**
- Modify: `src/WardenId.ClientManagement.Domain/Client.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/ClientUriCollectionsTests.cs`

**Interfaces:**
- Consumes: `Client.Create` (Task 9), `RedirectUri.Create` (Task 4), `CorsOrigin.Create` (Task 5)
- Produces: `Client.AddRedirectUri(RedirectUri)`, `.RemoveRedirectUri(RedirectUri)`, `.AddPostLogoutRedirectUri(RedirectUri)`, `.RemovePostLogoutRedirectUri(RedirectUri)`, `.AddCorsOrigin(CorsOrigin)`, `.RemoveCorsOrigin(CorsOrigin)`, `.ValidateRedirectUri(RedirectUri)` → `bool`.

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/ClientUriCollectionsTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class ClientUriCollectionsTests
{
    private static Client CreateClient() =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void AddRedirectUri_DedupesIdenticalValues()
    {
        var client = CreateClient();
        var uri = RedirectUri.Create("https://app.example.com/callback").Value;

        client.AddRedirectUri(uri);
        client.AddRedirectUri(uri);

        Assert.Single(client.RedirectUris);
    }

    [Fact]
    public void RemoveRedirectUri_RemovesTheMatchingEntry()
    {
        var client = CreateClient();
        var uri = RedirectUri.Create("https://app.example.com/callback").Value;
        client.AddRedirectUri(uri);

        client.RemoveRedirectUri(uri);

        Assert.Empty(client.RedirectUris);
    }

    [Fact]
    public void ValidateRedirectUri_TrueOnlyForRegisteredUris()
    {
        var client = CreateClient();
        var registered = RedirectUri.Create("https://app.example.com/callback").Value;
        var other = RedirectUri.Create("https://evil.example.com/callback").Value;
        client.AddRedirectUri(registered);

        Assert.True(client.ValidateRedirectUri(registered));
        Assert.False(client.ValidateRedirectUri(other));
    }

    [Fact]
    public void PostLogoutRedirectUris_AddAndRemoveIndependentlyOfRedirectUris()
    {
        var client = CreateClient();
        var uri = RedirectUri.Create("https://app.example.com/logged-out").Value;

        client.AddPostLogoutRedirectUri(uri);

        Assert.Single(client.PostLogoutRedirectUris);
        Assert.Empty(client.RedirectUris);

        client.RemovePostLogoutRedirectUri(uri);

        Assert.Empty(client.PostLogoutRedirectUris);
    }

    [Fact]
    public void CorsOrigins_AddDedupesAndRemoveWorks()
    {
        var client = CreateClient();
        var origin = CorsOrigin.Create("https://app.example.com").Value;

        client.AddCorsOrigin(origin);
        client.AddCorsOrigin(origin);

        Assert.Single(client.CorsOrigins);

        client.RemoveCorsOrigin(origin);

        Assert.Empty(client.CorsOrigins);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientUriCollectionsTests -v n`
Expected: FAIL — build error, the mutator methods don't exist yet.

- [ ] **Step 3: Add the mutators to `Client`**

Add inside the `Client` class in `src/WardenId.ClientManagement.Domain/Client.cs`, after `Create`:

```csharp
    public void AddRedirectUri(RedirectUri uri)
    {
        if (!_redirectUris.Contains(uri)) _redirectUris.Add(uri);
    }

    public void RemoveRedirectUri(RedirectUri uri) => _redirectUris.Remove(uri);

    public void AddPostLogoutRedirectUri(RedirectUri uri)
    {
        if (!_postLogoutRedirectUris.Contains(uri)) _postLogoutRedirectUris.Add(uri);
    }

    public void RemovePostLogoutRedirectUri(RedirectUri uri) => _postLogoutRedirectUris.Remove(uri);

    public void AddCorsOrigin(CorsOrigin origin)
    {
        if (!_corsOrigins.Contains(origin)) _corsOrigins.Add(origin);
    }

    public void RemoveCorsOrigin(CorsOrigin origin) => _corsOrigins.Remove(origin);

    public bool ValidateRedirectUri(RedirectUri requested) => _redirectUris.Contains(requested);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientUriCollectionsTests -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.ClientManagement.Domain/Client.cs tests/WardenId.ClientManagement.Domain.Tests/ClientUriCollectionsTests.cs
git commit -m "feat: add redirect/post-logout/CORS collection mutators to Client"
```

---

### Task 11: `AddSecret`/`RotateSecret`/`ExpireSecret`

**Files:**
- Create: `src/WardenId.ClientManagement.Domain/Events/ClientSecretRotatedDomainEvent.cs`
- Create: `src/WardenId.ClientManagement.Domain/Events/ClientSecretExpiredDomainEvent.cs`
- Modify: `src/WardenId.ClientManagement.Domain/Client.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/ClientSecretBehaviorTests.cs`

**Interfaces:**
- Consumes: `ClientSecret.Create`/`.SetExpiry` (Task 7), `ClientErrors.PublicClientCannotHaveSecret()`/`SecretNotFound()` (Task 2)
- Produces: `Client.AddSecret(ClientSecret)` → `Result`, `.RotateSecret(ClientSecret newSecret, DateTimeOffset overlapExpiresAt)` → `Result`, `.ExpireSecret(Guid secretId)` → `Result`.

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/ClientSecretBehaviorTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class ClientSecretBehaviorTests
{
    private static Client CreateClient(ClientType clientType = ClientType.Confidential) =>
        Client.Create(
            Guid.NewGuid(), clientType, "Test client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void AddSecret_FailsForPublicClients()
    {
        var client = CreateClient(ClientType.Public);
        var secret = ClientSecret.Create("hashed", "primary", expiresAt: null);

        var result = client.AddSecret(secret);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void AddSecret_SucceedsForConfidentialClients()
    {
        var client = CreateClient();
        var secret = ClientSecret.Create("hashed", "primary", expiresAt: null);

        var result = client.AddSecret(secret);

        Assert.True(result.IsSuccess);
        Assert.Single(client.Secrets);
    }

    [Fact]
    public void RotateSecret_AddsNewSecretAndExpiresNonExpiringOnes()
    {
        var client = CreateClient();
        var original = ClientSecret.Create("hashed-old", "primary", expiresAt: null);
        client.AddSecret(original);
        var overlapExpiry = DateTimeOffset.UtcNow.AddDays(7);
        var replacement = ClientSecret.Create("hashed-new", "primary", expiresAt: null);

        var result = client.RotateSecret(replacement, overlapExpiry);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, client.Secrets.Count);
        Assert.Equal(overlapExpiry, original.ExpiresAt);
        Assert.Null(replacement.ExpiresAt);
    }

    [Fact]
    public void RotateSecret_RaisesClientSecretRotatedDomainEvent()
    {
        var client = CreateClient();
        var replacement = ClientSecret.Create("hashed-new", "primary", expiresAt: null);

        client.RotateSecret(replacement, DateTimeOffset.UtcNow.AddDays(7));

        Assert.Contains(client.DomainEvents, e => e is ClientSecretRotatedDomainEvent);
    }

    [Fact]
    public void ExpireSecret_SetsExpiryToNow()
    {
        var client = CreateClient();
        var secret = ClientSecret.Create("hashed", "primary", expiresAt: null);
        client.AddSecret(secret);

        var result = client.ExpireSecret(secret.Id);

        Assert.True(result.IsSuccess);
        Assert.True(secret.IsExpired(DateTimeOffset.UtcNow.AddSeconds(1)));
    }

    [Fact]
    public void ExpireSecret_FailsWhenSecretNotFound()
    {
        var client = CreateClient();

        var result = client.ExpireSecret(Guid.NewGuid());

        Assert.True(result.IsFailed);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientSecretBehaviorTests -v n`
Expected: FAIL — build error, `AddSecret`/`RotateSecret`/`ExpireSecret` don't exist yet.

- [ ] **Step 3: Add the domain events**

`src/WardenId.ClientManagement.Domain/Events/ClientSecretRotatedDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain.Events;

public sealed record ClientSecretRotatedDomainEvent(Guid SecretId) : DomainEvent;
```

`src/WardenId.ClientManagement.Domain/Events/ClientSecretExpiredDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain.Events;

public sealed record ClientSecretExpiredDomainEvent(Guid SecretId) : DomainEvent;
```

- [ ] **Step 4: Add the behaviors to `Client`**

Add inside the `Client` class in `src/WardenId.ClientManagement.Domain/Client.cs`, after `ValidateRedirectUri`:

```csharp
    public Result AddSecret(ClientSecret secret)
    {
        if (ClientType == ClientType.Public) return Result.Fail(ClientErrors.PublicClientCannotHaveSecret());

        _secrets.Add(secret);
        return Result.Ok();
    }

    public Result RotateSecret(ClientSecret newSecret, DateTimeOffset overlapExpiresAt)
    {
        var addResult = AddSecret(newSecret);
        if (addResult.IsFailed) return addResult;

        foreach (var existing in _secrets.Where(s => s.Id != newSecret.Id && s.ExpiresAt is null))
        {
            existing.SetExpiry(overlapExpiresAt);
        }

        RaiseDomainEvent(new ClientSecretRotatedDomainEvent(newSecret.Id));
        return Result.Ok();
    }

    public Result ExpireSecret(Guid secretId)
    {
        var secret = _secrets.FirstOrDefault(s => s.Id == secretId);
        if (secret is null) return Result.Fail(ClientErrors.SecretNotFound());

        secret.SetExpiry(DateTimeOffset.UtcNow);
        RaiseDomainEvent(new ClientSecretExpiredDomainEvent(secretId));
        return Result.Ok();
    }
```

Add the events namespace to the top-of-file `using` list if not already present (it was added in Task 9): `using WardenId.ClientManagement.Domain.Events;`.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientSecretBehaviorTests -v n`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.ClientManagement.Domain tests/WardenId.ClientManagement.Domain.Tests/ClientSecretBehaviorTests.cs
git commit -m "feat: add Client secret rotation/expiry behaviors"
```

---

### Task 12: `AddSigningKey`

**Files:**
- Modify: `src/WardenId.ClientManagement.Domain/Client.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/ClientSigningKeyBehaviorTests.cs`

**Interfaces:**
- Consumes: `ClientSigningKey.Create` (Task 8), `ClientErrors.SigningKeyRequiresPrivateKeyJwt()` (Task 2)
- Produces: `Client.AddSigningKey(ClientSigningKey)` → `Result`.

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/ClientSigningKeyBehaviorTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class ClientSigningKeyBehaviorTests
{
    private static Client CreateClient(ClientAuthenticationMethod method) =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.ClientCredentials,
            method, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void AddSigningKey_FailsWhenNotPrivateKeyJwt()
    {
        var client = CreateClient(ClientAuthenticationMethod.SharedSecret);
        var key = ClientSigningKey.Create("kid-1", "{}", "ES256", expiresAt: null);

        var result = client.AddSigningKey(key);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void AddSigningKey_SucceedsWhenPrivateKeyJwt()
    {
        var client = CreateClient(ClientAuthenticationMethod.PrivateKeyJwt);
        var key = ClientSigningKey.Create("kid-1", "{}", "ES256", expiresAt: null);

        var result = client.AddSigningKey(key);

        Assert.True(result.IsSuccess);
        Assert.Single(client.SigningKeys);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientSigningKeyBehaviorTests -v n`
Expected: FAIL — build error, `AddSigningKey` doesn't exist yet.

- [ ] **Step 3: Add the behavior to `Client`**

Add inside the `Client` class, after `ExpireSecret`:

```csharp
    public Result AddSigningKey(ClientSigningKey key)
    {
        if (ClientAuthenticationMethod != ClientAuthenticationMethod.PrivateKeyJwt)
            return Result.Fail(ClientErrors.SigningKeyRequiresPrivateKeyJwt());

        _signingKeys.Add(key);
        return Result.Ok();
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientSigningKeyBehaviorTests -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.ClientManagement.Domain/Client.cs tests/WardenId.ClientManagement.Domain.Tests/ClientSigningKeyBehaviorTests.cs
git commit -m "feat: add Client.AddSigningKey behavior"
```

---

### Task 13: `AssignScope`/`RemoveScope`

**Files:**
- Create: `src/WardenId.ClientManagement.Domain/Events/ClientScopesChangedDomainEvent.cs`
- Modify: `src/WardenId.ClientManagement.Domain/Client.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/ClientScopeTests.cs`

**Interfaces:**
- Produces: `Client.AssignScope(Guid scopeId)`, `.RemoveScope(Guid scopeId)`.

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/ClientScopeTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class ClientScopeTests
{
    private static Client CreateClient() =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void AssignScope_AddsTheScopeIdAndRaisesEvent()
    {
        var client = CreateClient();
        var scopeId = Guid.NewGuid();

        client.AssignScope(scopeId);

        Assert.Contains(scopeId, client.AssignedScopeIds);
        Assert.Contains(client.DomainEvents, e => e is ClientScopesChangedDomainEvent changed && changed.Assigned);
    }

    [Fact]
    public void AssignScope_IsIdempotent_NoDuplicateOrSecondEvent()
    {
        var client = CreateClient();
        var scopeId = Guid.NewGuid();

        client.AssignScope(scopeId);
        client.AssignScope(scopeId);

        Assert.Single(client.AssignedScopeIds);
        Assert.Single(client.DomainEvents.OfType<ClientScopesChangedDomainEvent>());
    }

    [Fact]
    public void RemoveScope_RemovesTheScopeIdAndRaisesEvent()
    {
        var client = CreateClient();
        var scopeId = Guid.NewGuid();
        client.AssignScope(scopeId);

        client.RemoveScope(scopeId);

        Assert.DoesNotContain(scopeId, client.AssignedScopeIds);
        Assert.Contains(client.DomainEvents, e => e is ClientScopesChangedDomainEvent changed && !changed.Assigned);
    }

    [Fact]
    public void RemoveScope_NoOpAndNoEventWhenNotAssigned()
    {
        var client = CreateClient();

        client.RemoveScope(Guid.NewGuid());

        Assert.DoesNotContain(client.DomainEvents, e => e is ClientScopesChangedDomainEvent);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientScopeTests -v n`
Expected: FAIL — build error, `AssignScope`/`RemoveScope`/`ClientScopesChangedDomainEvent` don't exist yet.

- [ ] **Step 3: Add the domain event**

`src/WardenId.ClientManagement.Domain/Events/ClientScopesChangedDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain.Events;

public sealed record ClientScopesChangedDomainEvent(Guid ScopeId, bool Assigned) : DomainEvent;
```

- [ ] **Step 4: Add the behaviors to `Client`**

Add inside the `Client` class, after `AddSigningKey`:

```csharp
    public void AssignScope(Guid scopeId)
    {
        if (_assignedScopeIds.Contains(scopeId)) return;

        _assignedScopeIds.Add(scopeId);
        RaiseDomainEvent(new ClientScopesChangedDomainEvent(scopeId, Assigned: true));
    }

    public void RemoveScope(Guid scopeId)
    {
        if (!_assignedScopeIds.Remove(scopeId)) return;

        RaiseDomainEvent(new ClientScopesChangedDomainEvent(scopeId, Assigned: false));
    }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientScopeTests -v n`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.ClientManagement.Domain tests/WardenId.ClientManagement.Domain.Tests/ClientScopeTests.cs
git commit -m "feat: add Client.AssignScope/RemoveScope behaviors"
```

---

### Task 14: `Disable`/`Enable`

**Files:**
- Create: `src/WardenId.ClientManagement.Domain/Events/ClientDisabledDomainEvent.cs`
- Modify: `src/WardenId.ClientManagement.Domain/Client.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/ClientEnabledTests.cs`

**Interfaces:**
- Produces: `Client.Disable()`, `.Enable()`.

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/ClientEnabledTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class ClientEnabledTests
{
    private static Client CreateClient() =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void Disable_SetsEnabledFalseAndRaisesEvent()
    {
        var client = CreateClient();

        client.Disable();

        Assert.False(client.Enabled);
        Assert.Contains(client.DomainEvents, e => e is ClientDisabledDomainEvent);
    }

    [Fact]
    public void Enable_SetsEnabledTrueWithoutRaisingAnEvent()
    {
        var client = CreateClient();
        client.Disable();
        var eventCountAfterDisable = client.DomainEvents.Count;

        client.Enable();

        Assert.True(client.Enabled);
        Assert.Equal(eventCountAfterDisable, client.DomainEvents.Count);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientEnabledTests -v n`
Expected: FAIL — build error, `Disable`/`Enable`/`ClientDisabledDomainEvent` don't exist yet.

- [ ] **Step 3: Add the domain event**

`src/WardenId.ClientManagement.Domain/Events/ClientDisabledDomainEvent.cs`:

```csharp
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain.Events;

public sealed record ClientDisabledDomainEvent : DomainEvent;
```

- [ ] **Step 4: Add the behaviors to `Client`**

Add inside the `Client` class, after `RemoveScope`:

```csharp
    public void Disable()
    {
        Enabled = false;
        RaiseDomainEvent(new ClientDisabledDomainEvent());
    }

    public void Enable() => Enabled = true;
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientEnabledTests -v n`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.ClientManagement.Domain tests/WardenId.ClientManagement.Domain.Tests/ClientEnabledTests.cs
git commit -m "feat: add Client.Disable/Enable behaviors"
```

---

### Task 15: `UpdateTokenLifetimeOverrides` + full solution verification

**Files:**
- Modify: `src/WardenId.ClientManagement.Domain/Client.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/ClientTokenLifetimeTests.cs`

**Interfaces:**
- Consumes: `TokenLifetimeOverrides` (Task 6)
- Produces: `Client.UpdateTokenLifetimeOverrides(TokenLifetimeOverrides? overrides)`.

- [ ] **Step 1: Write the failing test**

`tests/WardenId.ClientManagement.Domain.Tests/ClientTokenLifetimeTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class ClientTokenLifetimeTests
{
    private static Client CreateClient() =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void UpdateTokenLifetimeOverrides_SetsTheOverrides()
    {
        var client = CreateClient();
        var overrides = TokenLifetimeOverrides.Create(3600, 3600, 86400).Value;

        client.UpdateTokenLifetimeOverrides(overrides);

        Assert.Equal(overrides, client.TokenLifetimeOverrides);
    }

    [Fact]
    public void UpdateTokenLifetimeOverrides_CanClearBackToNull()
    {
        var client = CreateClient();
        client.UpdateTokenLifetimeOverrides(TokenLifetimeOverrides.Create(3600, 3600, 86400).Value);

        client.UpdateTokenLifetimeOverrides(null);

        Assert.Null(client.TokenLifetimeOverrides);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientTokenLifetimeTests -v n`
Expected: FAIL — build error, `UpdateTokenLifetimeOverrides` doesn't exist yet.

- [ ] **Step 3: Add the behavior to `Client`**

Add inside the `Client` class, after `Enable`:

```csharp
    public void UpdateTokenLifetimeOverrides(TokenLifetimeOverrides? overrides) => TokenLifetimeOverrides = overrides;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter ClientTokenLifetimeTests -v n`
Expected: PASS

- [ ] **Step 5: Run the full solution build and full test suite**

Run: `dotnet build WardenId.slnx`
Expected: Build succeeds, 0 errors.

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests -v n`
Expected: All tests PASS (55 tests across Tasks 1-15).

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.ClientManagement.Domain/Client.cs tests/WardenId.ClientManagement.Domain.Tests/ClientTokenLifetimeTests.cs
git commit -m "feat: add Client.UpdateTokenLifetimeOverrides behavior"
```
