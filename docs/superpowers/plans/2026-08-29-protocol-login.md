# Protocol Login Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a login page that authenticates a user against `Identity` and issues an access token, refresh token, and ID token signed with the tenant's ES256 signing key from `Tenancy`.

**Architecture:** New `WardenId.Protocol` module (Domain/Application/Infrastructure/Presentation), composing `Identity` and `Tenancy` in-process via MediatR. `Protocol.Application` stays fully decoupled (its own small abstractions, no reference to Identity/Tenancy types); `Protocol.Infrastructure` is where the cross-module `ISender` calls actually happen, via two adapter classes that reference `Identity.Application`/`Tenancy.Application` for their public Command/Query types only.

**Tech Stack:** .NET 10, MediatR 12.5.0, `Microsoft.IdentityModel.JsonWebTokens` (ES256 JWTs), ASP.NET Core Data Protection (private key decryption, existing shared key ring), EF Core/Npgsql, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-29-protocol-login-design.md`

## Plan-level corrections to the spec

Discovered during planning; both are implementation-detail refinements, not scope changes:

1. **Cross-module reference is unavoidable, and that's fine — but it's narrower than the spec implied.** The spec's "zero cross-reference" framing was too strict: calling `ISender.Send(new Identity...Authenticate.Command(...))` requires a compile-time reference to that Command type, full stop. This plan puts that reference where it does the least damage: `Protocol.Application`'s `Login` handler depends only on two small interfaces it owns (`IAuthenticateUsers`, `IActiveSigningKeyProvider`) with Protocol-local DTOs (plain `Guid`/`string`/`bool` fields, no `Identity.Domain`/`Tenancy.Domain` types). Only `Protocol.Infrastructure` (which already reaches into `Identity`/`Tenancy` via Data Protection's shared purpose string) takes `ProjectReference`s to `Identity.Application` and `Tenancy.Application`, to implement those interfaces as thin `ISender`-calling adapters. `Protocol.Application` stays exactly as decoupled as the spec intended and is trivially unit-testable with plain fakes, matching every other module's Handler-test style in this repo.
2. **`SigningKey.IsActive` is currently never set to `true` anywhere in the codebase** (`Tenancy.Domain/SigningKey.cs`'s `CreateEs256` factory never assigns it, `Tenant.Activate()` checks `IsDeleted`, not `IsActive`). Every signing key ever created — including the master tenant's, via the seeder — has `IsActive == false`. This blocks login outright (no active key would ever be found), so Task 2 fixes it. This is a pre-existing gap, not new scope.

## Global Constraints

- Target framework `net10.0`, nullable + implicit usings enabled (`Directory.Build.props`) — no per-project overrides.
- All package versions come from `Directory.Packages.props`; every package this plan needs (`Microsoft.IdentityModel.JsonWebTokens`, `Microsoft.AspNetCore.DataProtection.Abstractions`, `Microsoft.EntityFrameworkCore.Design`, `Asp.Versioning.Http`, `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`) is already centrally versioned there — add no new `PackageVersion` entries.
- Every project restores with `packages.lock.json` (`RestorePackagesWithLockFile=true`) — after adding any project or package reference, run `dotnet restore --force-evaluate` from the repo root before building.
- Follow the existing 4-layer module pattern exactly (`Domain` → zero framework deps; `Application` → MediatR command/query + `Abstractions/`; `Infrastructure` → EF Core + DI wiring; `Presentation` → minimal-API `IEndpoint`s), and the vertical-slice `Features/<Aggregate>/<UseCase>/` layout within Application/Presentation.
- No module takes a `ProjectReference` to another module's `Application`/`Domain` project, **except** the two adapters in Task 7 (`Protocol.Infrastructure` → `Identity.Application`, `Protocol.Infrastructure` → `Tenancy.Application`) — a deliberate, narrow exception explained above.
- New aggregates: private constructor + static `Create(...)` factory returning `Result<T>`; derive from `SharedKernel.Domain.AggregateRoot`; `Id` is set by the base `Entity` class (`Guid.CreateVersion7()`), never assign it manually.
- Domain/Application layers return `FluentResults.Result`/`Result<T>`, never throw for expected failures. Error factories live in a static `<Module>Errors` class tagged with `<Module>ErrorCodes.MetadataKey` = `"ErrorCode"`.
- `dotnet ef migrations add <Name> --project src/WardenId.Protocol.Infrastructure --startup-project WardenId.Gateway --context ProtocolDbContext` for the new module's migration (run from repo root).
- Test projects: xUnit, `IsPackable=false`, no mocking framework — hand-written `Fake*` classes in a `Fakes/` folder, `Using` items in the `.csproj` for common namespaces (see `tests/WardenId.Identity.Application.Tests` for the exact shape to copy).

---

### Task 1: `WardenId.Protocol.Domain` — `RefreshToken` aggregate

**Files:**
- Create: `src/WardenId.Protocol.Domain/WardenId.Protocol.Domain.csproj`
- Create: `src/WardenId.Protocol.Domain/RefreshToken.cs`
- Create: `src/WardenId.Protocol.Domain/ProtocolErrorCodes.cs`
- Create: `src/WardenId.Protocol.Domain/ProtocolErrors.cs`
- Create: `tests/WardenId.Protocol.Domain.Tests/WardenId.Protocol.Domain.Tests.csproj`
- Create: `tests/WardenId.Protocol.Domain.Tests/RefreshTokenTests.cs`
- Modify: `WardenId.slnx` — add both new project entries

**Interfaces:**
- Produces: `WardenId.Protocol.Domain.RefreshToken` (aggregate), `RefreshToken.Create(Guid tenantId, Guid userId, string tokenHash, DateTimeOffset expiresAtUtc) -> Result<RefreshToken>`, properties `TenantId`, `UserId`, `TokenHash`, `ExpiresAtUtc`, `IsRevoked` (all `private set`). `ProtocolErrors.TokenHashRequired()`, `ProtocolErrors.RefreshTokenExpiryMustBeFuture()`.

- [ ] **Step 1: Create the project file**

```xml
<!-- src/WardenId.Protocol.Domain/WardenId.Protocol.Domain.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
      <ProjectReference Include="..\WardenId.SharedKernel.Domain\WardenId.SharedKernel.Domain.csproj" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: Add both projects to `WardenId.slnx`**

Add a new folder block (after `/src/WardenId.Identity/`'s closing `</Folder>`, before `<Folder Name="/tests/">`):

```xml
  <Folder Name="/src/WardenId.Protocol/">
    <Project Path="src/WardenId.Protocol.Domain/WardenId.Protocol.Domain.csproj" />
  </Folder>
```

Inside the existing `<Folder Name="/tests/">` block, add:

```xml
    <Project Path="tests/WardenId.Protocol.Domain.Tests/WardenId.Protocol.Domain.Tests.csproj" />
```

- [ ] **Step 3: Write the test project file**

```xml
<!-- tests/WardenId.Protocol.Domain.Tests/WardenId.Protocol.Domain.Tests.csproj -->
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
    <ProjectReference Include="..\..\src\WardenId.Protocol.Domain\WardenId.Protocol.Domain.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="WardenId.Protocol.Domain" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Write the failing tests**

```csharp
// tests/WardenId.Protocol.Domain.Tests/RefreshTokenTests.cs
namespace WardenId.Protocol.Domain.Tests;

public class RefreshTokenTests
{
    [Fact]
    public void Create_Succeeds_WithValidHashAndFutureExpiry()
    {
        var result = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "somehash", DateTimeOffset.UtcNow.AddDays(30));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsRevoked);
    }

    [Fact]
    public void Create_Fails_WhenTokenHashIsEmpty()
    {
        var result = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "", DateTimeOffset.UtcNow.AddDays(30));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_Fails_WhenExpiryIsInThePast()
    {
        var result = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "somehash", DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.True(result.IsFailed);
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail to compile (no `RefreshToken` type yet)**

Run: `dotnet test tests/WardenId.Protocol.Domain.Tests/WardenId.Protocol.Domain.Tests.csproj`
Expected: build error, `RefreshToken` does not exist in namespace `WardenId.Protocol.Domain`.

- [ ] **Step 6: Implement `ProtocolErrorCodes` and `ProtocolErrors`**

```csharp
// src/WardenId.Protocol.Domain/ProtocolErrorCodes.cs
namespace WardenId.Protocol.Domain;

public static class ProtocolErrorCodes
{
    public const string MetadataKey = "ErrorCode";

    public const string Validation = "Validation";
}
```

```csharp
// src/WardenId.Protocol.Domain/ProtocolErrors.cs
using FluentResults;

namespace WardenId.Protocol.Domain;

public static class ProtocolErrors
{
    public static Error Validation(string reason)
        => new Error(reason).WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.Validation);

    public static Error TokenHashRequired() => Validation("Refresh token hash is required.");

    public static Error RefreshTokenExpiryMustBeFuture() => Validation("Refresh token expiry must be in the future.");
}
```

- [ ] **Step 7: Implement `RefreshToken`**

```csharp
// src/WardenId.Protocol.Domain/RefreshToken.cs
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Protocol.Domain;

public sealed class RefreshToken : AggregateRoot
{
    // EF Core materialization only
    private RefreshToken() { }

    private RefreshToken(Guid tenantId, Guid userId, string tokenHash, DateTimeOffset expiresAtUtc)
    {
        TenantId = tenantId;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public bool IsRevoked { get; private set; }

    public static Result<RefreshToken> Create(Guid tenantId, Guid userId, string tokenHash, DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(tokenHash)) return Result.Fail(ProtocolErrors.TokenHashRequired());
        if (expiresAtUtc <= DateTimeOffset.UtcNow) return Result.Fail(ProtocolErrors.RefreshTokenExpiryMustBeFuture());

        return Result.Ok(new RefreshToken(tenantId, userId, tokenHash, expiresAtUtc));
    }
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/WardenId.Protocol.Domain.Tests/WardenId.Protocol.Domain.Tests.csproj`
Expected: 3 passed.

- [ ] **Step 9: Restore and build the whole solution**

Run: `dotnet restore WardenId.slnx --force-evaluate && dotnet build WardenId.slnx`
Expected: build succeeds.

- [ ] **Step 10: Commit**

```bash
git add src/WardenId.Protocol.Domain tests/WardenId.Protocol.Domain.Tests WardenId.slnx
git commit -m "feat: add Protocol.Domain with RefreshToken aggregate"
```

---

### Task 2: Fix `SigningKey.IsActive`, promote the DataProtection purpose string, add a Tenancy error

**Files:**
- Modify: `src/WardenId.Tenancy.Domain/SigningKey.cs`
- Modify: `src/WardenId.Tenancy.Domain/TenantErrors.cs`
- Create: `src/WardenId.Tenancy.Application/Abstractions/SigningKeyProtectionPurposes.cs`
- Modify: `src/WardenId.Tenancy.Infrastructure/Es256SigningKeyGenerator.cs`

**Interfaces:**
- Produces: `WardenId.Tenancy.Application.Abstractions.SigningKeyProtectionPurposes.PrivateKey` (public const string), `TenantErrors.TenantHasNoActiveSigningKey()`.
- Consumes (Task 7): `SigningKeyProtectionPurposes.PrivateKey` is read by `Protocol.Infrastructure.JwtIssuer`.

No test project exists for `Tenancy` (`Tenancy.Domain`/`Tenancy.Application` have no `tests/` counterpart in this repo) — this task is verified by build only, consistent with how the rest of `Tenancy` is currently covered.

- [ ] **Step 1: Set `IsActive = true` when a key is created**

In `src/WardenId.Tenancy.Domain/SigningKey.cs`, change the private constructor:

```csharp
    private SigningKey(
        Guid signingKeyId,
        string algorithm, 
        string publicKeyJwk,
        byte[] encryptedPrivateKey)
    {
        SigningKeyId =  signingKeyId;
        Algorithm = algorithm;
        PublicKeyJwk = publicKeyJwk;
        EncryptedPrivateKey = encryptedPrivateKey;
        IsActive = true;
    }
```

(Only the added `IsActive = true;` line changes — every currently-created key is meant to be its tenant's one active key; rotation, which would explicitly deactivate an old key, doesn't exist yet.)

- [ ] **Step 2: Add the new Tenancy error**

In `src/WardenId.Tenancy.Domain/TenantErrors.cs`, add (near `TenantNoActiveSigningKey`):

```csharp
    public static Error TenantHasNoActiveSigningKey()
        => Validation("Tenant has no active signing key.");
```

- [ ] **Step 3: Promote the DataProtection purpose string to a shared public constant**

```csharp
// src/WardenId.Tenancy.Application/Abstractions/SigningKeyProtectionPurposes.cs
namespace WardenId.Tenancy.Application.Abstractions;

/// <summary>
/// Data Protection purpose string used to encrypt/decrypt tenant signing-key private key
/// material. Shared verbatim with WardenId.Protocol.Infrastructure, which decrypts the key
/// at token-signing time (see SigningKeyMaterial's doc comment) — bump the version suffix on
/// both sides together if the protection scheme ever changes incompatibly.
/// </summary>
public static class SigningKeyProtectionPurposes
{
    public const string PrivateKey = "WardenId.Tenancy.SigningKeys.v1";
}
```

- [ ] **Step 4: Point `Es256SigningKeyGenerator` at the shared constant**

In `src/WardenId.Tenancy.Infrastructure/Es256SigningKeyGenerator.cs`, replace:

```csharp
    // Bump the version suffix if the protection scheme ever changes incompatibly —
    // existing encrypted keys become undecryptable across a purpose-string change.
    private const string DataProtectionPurpose = "WardenId.Tenancy.SigningKeys.v1";
 
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
```

with:

```csharp
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector(SigningKeyProtectionPurposes.PrivateKey);
```

- [ ] **Step 5: Build**

Run: `dotnet build WardenId.slnx`
Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Tenancy.Domain/SigningKey.cs src/WardenId.Tenancy.Domain/TenantErrors.cs \
  src/WardenId.Tenancy.Application/Abstractions/SigningKeyProtectionPurposes.cs \
  src/WardenId.Tenancy.Infrastructure/Es256SigningKeyGenerator.cs
git commit -m "fix: activate signing keys on creation; share DataProtection purpose string"
```

---

### Task 3: `Tenancy.Application` — `GetActiveForSigning` query

**Files:**
- Create: `src/WardenId.Tenancy.Application/Features/SigningKeys/GetActiveForSigning/Query.cs`
- Create: `src/WardenId.Tenancy.Application/Features/SigningKeys/GetActiveForSigning/ActiveSigningKeyDto.cs`
- Create: `src/WardenId.Tenancy.Application/Features/SigningKeys/GetActiveForSigning/Handler.cs`

**Interfaces:**
- Consumes: `ITenancyDbContext.Tenants` (`DbSet<Tenant>`, existing), `TenantErrors.NotFound`, `TenantErrors.TenantHasNoActiveSigningKey()` (Task 2).
- Produces: `WardenId.Tenancy.Application.Features.SigningKeys.GetActiveForSigning.Query(Guid TenantId) : IRequest<Result<ActiveSigningKeyDto>>`, `ActiveSigningKeyDto(Guid KeyId, string Algorithm, byte[] EncryptedPrivateKey)`. Consumed by `Protocol.Infrastructure.MediatRSigningKeyProvider` in Task 7.

- [ ] **Step 1: Write the DTO**

```csharp
// src/WardenId.Tenancy.Application/Features/SigningKeys/GetActiveForSigning/ActiveSigningKeyDto.cs
namespace WardenId.Tenancy.Application.Features.SigningKeys.GetActiveForSigning;

public sealed record ActiveSigningKeyDto(Guid KeyId, string Algorithm, byte[] EncryptedPrivateKey);
```

- [ ] **Step 2: Write the query**

```csharp
// src/WardenId.Tenancy.Application/Features/SigningKeys/GetActiveForSigning/Query.cs
using FluentResults;
using MediatR;

namespace WardenId.Tenancy.Application.Features.SigningKeys.GetActiveForSigning;

public sealed record Query(Guid TenantId) : IRequest<Result<ActiveSigningKeyDto>>;
```

- [ ] **Step 3: Write the handler**

```csharp
// src/WardenId.Tenancy.Application/Features/SigningKeys/GetActiveForSigning/Handler.cs
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Domain;

namespace WardenId.Tenancy.Application.Features.SigningKeys.GetActiveForSigning;

public sealed class Handler(ITenancyDbContext dbContext) : IRequestHandler<Query, Result<ActiveSigningKeyDto>>
{
    public async Task<Result<ActiveSigningKeyDto>> Handle(Query request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .Include(t => t.SigningKeys)
            .Where(t => t.Id == request.TenantId && !t.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null) return Result.Fail(TenantErrors.NotFound);

        var activeKey = tenant.SigningKeys.FirstOrDefault(k => k.IsActive && !k.IsDeleted);
        if (activeKey is null) return Result.Fail(TenantErrors.TenantHasNoActiveSigningKey());

        return Result.Ok(new ActiveSigningKeyDto(activeKey.SigningKeyId, activeKey.Algorithm, activeKey.EncryptedPrivateKey));
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build WardenId.slnx`
Expected: build succeeds. `Tenancy.Application`'s `DependencyInjection.AddTenancyApplication` already calls `AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()))`, so this handler is auto-registered — no DI change needed.

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.Tenancy.Application/Features/SigningKeys
git commit -m "feat: add GetActiveForSigning query to Tenancy.Application"
```

---

### Task 4: `WardenId.Protocol.Application` — abstractions, options, DI wiring

**Files:**
- Create: `src/WardenId.Protocol.Application/WardenId.Protocol.Application.csproj`
- Create: `src/WardenId.Protocol.Application/Abstractions/IAuthenticateUsers.cs`
- Create: `src/WardenId.Protocol.Application/Abstractions/IActiveSigningKeyProvider.cs`
- Create: `src/WardenId.Protocol.Application/Abstractions/IJwtIssuer.cs`
- Create: `src/WardenId.Protocol.Application/Abstractions/IRefreshTokenRepository.cs`
- Create: `src/WardenId.Protocol.Application/ProtocolTokenOptions.cs`
- Create: `src/WardenId.Protocol.Application/ConfigurationKeys.cs`
- Create: `src/WardenId.Protocol.Application/DependencyInjection.cs`
- Modify: `WardenId.slnx` — add project entry (inside the `/src/WardenId.Protocol/` folder added in Task 1)

**Interfaces:**
- Produces: `IAuthenticateUsers.AuthenticateAsync(Guid, string, string, CancellationToken) -> Task<Result<AuthenticatedUserInfo>>`; `AuthenticatedUserInfo(Guid UserId, string Email, bool EmailVerified, string Username, bool IsActive, bool HasPendingActions)`. `IActiveSigningKeyProvider.GetActiveSigningKeyAsync(Guid, CancellationToken) -> Task<Result<ActiveSigningKey>>`; `ActiveSigningKey(Guid KeyId, string Algorithm, byte[] EncryptedPrivateKey)`. `IJwtIssuer.IssueAccessToken(ActiveSigningKey, Guid, Guid, DateTimeOffset, TimeSpan) -> string`; `IJwtIssuer.IssueIdToken(ActiveSigningKey, Guid, Guid, string, bool, string, DateTimeOffset, TimeSpan) -> string`. `IRefreshTokenRepository.CreateAsync(RefreshToken, Guid, CancellationToken) -> Task`. `ProtocolTokenOptions { IssuerBaseUri, AccessTokenLifetime, IdTokenLifetime, RefreshTokenLifetime }`.
- Consumed by: Task 6 (`Login` feature, same project), Task 7 (`Protocol.Infrastructure` implements all four interfaces).

- [ ] **Step 1: Write the project file**

```xml
<!-- src/WardenId.Protocol.Application/WardenId.Protocol.Application.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
      <ProjectReference Include="..\WardenId.SharedKernel.Application\WardenId.SharedKernel.Application.csproj" />
      <ProjectReference Include="..\WardenId.Protocol.Domain\WardenId.Protocol.Domain.csproj" />
    </ItemGroup>

    <ItemGroup>
      <PackageReference Include="MediatR" />
      <PackageReference Include="Microsoft.Extensions.Options" />
      <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: Add to `WardenId.slnx`**

Inside the `/src/WardenId.Protocol/` folder added in Task 1:

```xml
    <Project Path="src/WardenId.Protocol.Application/WardenId.Protocol.Application.csproj" />
```

- [ ] **Step 3: Write the abstractions**

```csharp
// src/WardenId.Protocol.Application/Abstractions/IAuthenticateUsers.cs
using FluentResults;

namespace WardenId.Protocol.Application.Abstractions;

public interface IAuthenticateUsers
{
    Task<Result<AuthenticatedUserInfo>> AuthenticateAsync(
        Guid tenantId, string emailOrUsername, string password, CancellationToken cancellationToken);
}

public sealed record AuthenticatedUserInfo(
    Guid UserId,
    string Email,
    bool EmailVerified,
    string Username,
    bool IsActive,
    bool HasPendingActions);
```

```csharp
// src/WardenId.Protocol.Application/Abstractions/IActiveSigningKeyProvider.cs
using FluentResults;

namespace WardenId.Protocol.Application.Abstractions;

public interface IActiveSigningKeyProvider
{
    Task<Result<ActiveSigningKey>> GetActiveSigningKeyAsync(Guid tenantId, CancellationToken cancellationToken);
}

public sealed record ActiveSigningKey(Guid KeyId, string Algorithm, byte[] EncryptedPrivateKey);
```

```csharp
// src/WardenId.Protocol.Application/Abstractions/IJwtIssuer.cs
namespace WardenId.Protocol.Application.Abstractions;

public interface IJwtIssuer
{
    string IssueAccessToken(ActiveSigningKey key, Guid tenantId, Guid userId, DateTimeOffset issuedAtUtc, TimeSpan lifetime);

    string IssueIdToken(
        ActiveSigningKey key, Guid tenantId, Guid userId, string email, bool emailVerified, string username,
        DateTimeOffset issuedAtUtc, TimeSpan lifetime);
}
```

```csharp
// src/WardenId.Protocol.Application/Abstractions/IRefreshTokenRepository.cs
using WardenId.Protocol.Domain;

namespace WardenId.Protocol.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task CreateAsync(RefreshToken refreshToken, Guid userId, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Write `ProtocolTokenOptions` and `ConfigurationKeys`**

```csharp
// src/WardenId.Protocol.Application/ProtocolTokenOptions.cs
namespace WardenId.Protocol.Application;

public sealed class ProtocolTokenOptions
{
    public string IssuerBaseUri { get; init; } = "https://wardenid.local";
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan IdTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);
}
```

```csharp
// src/WardenId.Protocol.Application/ConfigurationKeys.cs
namespace WardenId.Protocol.Application;

public static class ConfigurationKeys
{
    public static class ProtocolToken
    {
        public const string Section = "ProtocolToken";
    }
}
```

- [ ] **Step 5: Write `DependencyInjection.cs`**

```csharp
// src/WardenId.Protocol.Application/DependencyInjection.cs
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WardenId.Protocol.Application;

public static class DependencyInjection
{
    public static void AddProtocolApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
        services.Configure<ProtocolTokenOptions>(configuration.GetSection(ConfigurationKeys.ProtocolToken.Section));
    }
}
```

- [ ] **Step 6: Restore and build**

Run: `dotnet restore WardenId.slnx --force-evaluate && dotnet build WardenId.slnx`
Expected: build succeeds (no `Features/` yet, but the project compiles standalone).

- [ ] **Step 7: Commit**

```bash
git add src/WardenId.Protocol.Application WardenId.slnx
git commit -m "feat: add Protocol.Application abstractions and DI wiring"
```

---

### Task 5: Extend `Identity`'s `Authenticate` result with `Email`/`Username`

**Files:**
- Modify: `src/WardenId.Identity.Application/Features/Users/Authenticate/AuthenticatedUser.cs`
- Modify: `src/WardenId.Identity.Application/Features/Users/Authenticate/Handler.cs`

**Interfaces:**
- Produces: `AuthenticatedUser(Guid UserId, string Email, string Username, UserStatus Status, IReadOnlyCollection<RequiredAction> PendingActions)` — two new fields, existing three unchanged in name/type. Consumed by `Protocol.Infrastructure.MediatRUserAuthenticator` in Task 7.
- Consumes: `User.Email.Value`, `User.Username.Value` (existing, already used identically in `Features/Users/Get/Handler.cs`).

This is a small, additive change to an existing DTO — the `/auth/authenticate` HTTP endpoint's JSON response gains two fields, nothing is removed or renamed. `tests/WardenId.Identity.Application.Tests/Features/Users/Authenticate/HandlerTests.cs` only asserts individual properties (`result.Value.UserId`, etc.), never the full shape, so no test changes are needed — but re-run them to be sure.

- [ ] **Step 1: Add the two fields to the DTO**

```csharp
// src/WardenId.Identity.Application/Features/Users/Authenticate/AuthenticatedUser.cs
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Authenticate;

public sealed record AuthenticatedUser(
    Guid UserId, string Email, string Username, UserStatus Status, IReadOnlyCollection<RequiredAction> PendingActions);
```

- [ ] **Step 2: Update the handler's return statement**

In `src/WardenId.Identity.Application/Features/Users/Authenticate/Handler.cs`, change the final line:

```csharp
        return Result.Ok(new AuthenticatedUser(user.Id, user.Status, user.PendingActions));
```

to:

```csharp
        return Result.Ok(new AuthenticatedUser(user.Id, user.Email.Value, user.Username.Value, user.Status, user.PendingActions));
```

- [ ] **Step 3: Run the existing Authenticate tests to confirm nothing broke**

Run: `dotnet test tests/WardenId.Identity.Application.Tests/WardenId.Identity.Application.Tests.csproj --filter "FullyQualifiedName~Authenticate"`
Expected: all existing tests still pass.

- [ ] **Step 4: Build the whole solution**

Run: `dotnet build WardenId.slnx`
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.Identity.Application/Features/Users/Authenticate
git commit -m "feat: include email/username in AuthenticatedUser"
```

---

### Task 6: `Protocol.Application` — `Login` feature

**Files:**
- Create: `src/WardenId.Protocol.Application/Features/Login/Command.cs`
- Create: `src/WardenId.Protocol.Application/Features/Login/TokenSet.cs`
- Create: `src/WardenId.Protocol.Application/Features/Login/LoginResult.cs`
- Create: `src/WardenId.Protocol.Application/Features/Login/Handler.cs`
- Create: `tests/WardenId.Protocol.Application.Tests/WardenId.Protocol.Application.Tests.csproj`
- Create: `tests/WardenId.Protocol.Application.Tests/Fakes/FakeAuthenticateUsers.cs`
- Create: `tests/WardenId.Protocol.Application.Tests/Fakes/FakeActiveSigningKeyProvider.cs`
- Create: `tests/WardenId.Protocol.Application.Tests/Fakes/FakeJwtIssuer.cs`
- Create: `tests/WardenId.Protocol.Application.Tests/Fakes/FakeRefreshTokenRepository.cs`
- Create: `tests/WardenId.Protocol.Application.Tests/Features/Login/HandlerTests.cs`
- Modify: `WardenId.slnx` — add the test project entry

**Interfaces:**
- Consumes: `IAuthenticateUsers`, `IActiveSigningKeyProvider`, `IJwtIssuer`, `IRefreshTokenRepository`, `ProtocolTokenOptions` (Task 4), `RefreshToken.Create` (Task 1).
- Produces: `WardenId.Protocol.Application.Features.Login.Command(Guid TenantId, string EmailOrUsername, string Password) : IRequest<Result<LoginResult>>`; `TokenSet` (JSON-shaped: `access_token`, `refresh_token`, `id_token`, `token_type`, `expires_in`); `LoginResult(bool RequiresAdditionalAction, TokenSet? Tokens)`. Consumed by `Protocol.Presentation.Endpoint` in Task 8.

- [ ] **Step 1: Write the test project file**

```xml
<!-- tests/WardenId.Protocol.Application.Tests/WardenId.Protocol.Application.Tests.csproj -->
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
    <ProjectReference Include="..\..\src\WardenId.Protocol.Application\WardenId.Protocol.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="FluentResults" />
    <Using Include="WardenId.Protocol.Domain" />
    <Using Include="WardenId.Protocol.Application" />
    <Using Include="WardenId.Protocol.Application.Abstractions" />
    <Using Include="WardenId.Protocol.Application.Tests.Fakes" />
  </ItemGroup>

</Project>
```

Add it inside the `<Folder Name="/tests/">` block in `WardenId.slnx`:

```xml
    <Project Path="tests/WardenId.Protocol.Application.Tests/WardenId.Protocol.Application.Tests.csproj" />
```

- [ ] **Step 2: Write the fakes**

```csharp
// tests/WardenId.Protocol.Application.Tests/Fakes/FakeAuthenticateUsers.cs
namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeAuthenticateUsers : IAuthenticateUsers
{
    public Result<AuthenticatedUserInfo>? Response { get; set; }

    public Task<Result<AuthenticatedUserInfo>> AuthenticateAsync(
        Guid tenantId, string emailOrUsername, string password, CancellationToken cancellationToken) =>
        Task.FromResult(Response ?? throw new InvalidOperationException("Response not configured."));
}
```

```csharp
// tests/WardenId.Protocol.Application.Tests/Fakes/FakeActiveSigningKeyProvider.cs
namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeActiveSigningKeyProvider : IActiveSigningKeyProvider
{
    public Result<ActiveSigningKey>? Response { get; set; }

    public Task<Result<ActiveSigningKey>> GetActiveSigningKeyAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(Response ?? throw new InvalidOperationException("Response not configured."));
}
```

```csharp
// tests/WardenId.Protocol.Application.Tests/Fakes/FakeJwtIssuer.cs
namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeJwtIssuer : IJwtIssuer
{
    public string IssueAccessToken(ActiveSigningKey key, Guid tenantId, Guid userId, DateTimeOffset issuedAtUtc, TimeSpan lifetime) =>
        $"access:{userId}";

    public string IssueIdToken(
        ActiveSigningKey key, Guid tenantId, Guid userId, string email, bool emailVerified, string username,
        DateTimeOffset issuedAtUtc, TimeSpan lifetime) =>
        $"id:{userId}";
}
```

```csharp
// tests/WardenId.Protocol.Application.Tests/Fakes/FakeRefreshTokenRepository.cs
namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Created { get; } = [];

    public Task CreateAsync(RefreshToken refreshToken, Guid userId, CancellationToken cancellationToken)
    {
        Created.Add(refreshToken);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Write the failing tests**

```csharp
// tests/WardenId.Protocol.Application.Tests/Features/Login/HandlerTests.cs
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Features.Login;

namespace WardenId.Protocol.Application.Tests.Features.Login;

public class HandlerTests
{
    private static AuthenticatedUserInfo ActiveUser(Guid userId) =>
        new(userId, "someone@example.com", true, "someone", IsActive: true, HasPendingActions: false);

    [Fact]
    public async Task Handle_IssuesTokens_WhenUserIsActiveWithNoPendingActions()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var authenticator = new FakeAuthenticateUsers { Response = Result.Ok(ActiveUser(userId)) };
        var signingKeyProvider = new FakeActiveSigningKeyProvider
        {
            Response = Result.Ok(new ActiveSigningKey(Guid.NewGuid(), "ES256", [1, 2, 3]))
        };
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var handler = new Handler(
            authenticator, signingKeyProvider, new FakeJwtIssuer(), refreshTokenRepository,
            Options.Create(new ProtocolTokenOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone", "password"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.RequiresAdditionalAction);
        Assert.NotNull(result.Value.Tokens);
        Assert.Equal("Bearer", result.Value.Tokens!.TokenType);
        Assert.Single(refreshTokenRepository.Created);
    }

    [Fact]
    public async Task Handle_SkipsTokenIssuance_WhenUserHasPendingActions()
    {
        var userId = Guid.NewGuid();
        var authenticator = new FakeAuthenticateUsers
        {
            Response = Result.Ok(ActiveUser(userId) with { HasPendingActions = true })
        };
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var handler = new Handler(
            authenticator, new FakeActiveSigningKeyProvider(), new FakeJwtIssuer(), refreshTokenRepository,
            Options.Create(new ProtocolTokenOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "someone", "password"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.RequiresAdditionalAction);
        Assert.Null(result.Value.Tokens);
        Assert.Empty(refreshTokenRepository.Created);
    }

    [Fact]
    public async Task Handle_SkipsTokenIssuance_WhenUserIsNotActive()
    {
        var userId = Guid.NewGuid();
        var authenticator = new FakeAuthenticateUsers
        {
            Response = Result.Ok(ActiveUser(userId) with { IsActive = false })
        };
        var handler = new Handler(
            authenticator, new FakeActiveSigningKeyProvider(), new FakeJwtIssuer(), new FakeRefreshTokenRepository(),
            Options.Create(new ProtocolTokenOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "someone", "password"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.RequiresAdditionalAction);
    }

    [Fact]
    public async Task Handle_PropagatesFailure_WhenAuthenticationFails()
    {
        var authenticator = new FakeAuthenticateUsers
        {
            Response = Result.Fail<AuthenticatedUserInfo>(
                new Error("Invalid credentials.").WithMetadata("ErrorCode", "InvalidCredentials"))
        };
        var handler = new Handler(
            authenticator, new FakeActiveSigningKeyProvider(), new FakeJwtIssuer(), new FakeRefreshTokenRepository(),
            Options.Create(new ProtocolTokenOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "someone", "wrong"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("InvalidCredentials", result.Errors[0].Metadata["ErrorCode"]);
    }

    [Fact]
    public async Task Handle_PropagatesFailure_WhenNoActiveSigningKeyExists()
    {
        var userId = Guid.NewGuid();
        var authenticator = new FakeAuthenticateUsers { Response = Result.Ok(ActiveUser(userId)) };
        var signingKeyProvider = new FakeActiveSigningKeyProvider
        {
            Response = Result.Fail<ActiveSigningKey>(new Error("Tenant has no active signing key."))
        };
        var handler = new Handler(
            authenticator, signingKeyProvider, new FakeJwtIssuer(), new FakeRefreshTokenRepository(),
            Options.Create(new ProtocolTokenOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "someone", "password"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail to compile (no `Command`/`Handler` yet)**

Run: `dotnet test tests/WardenId.Protocol.Application.Tests/WardenId.Protocol.Application.Tests.csproj`
Expected: build error, types don't exist under `WardenId.Protocol.Application.Features.Login`.

- [ ] **Step 5: Write `TokenSet` and `LoginResult`**

```csharp
// src/WardenId.Protocol.Application/Features/Login/TokenSet.cs
using System.Text.Json.Serialization;

namespace WardenId.Protocol.Application.Features.Login;

public sealed record TokenSet(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("id_token")] string IdToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
```

```csharp
// src/WardenId.Protocol.Application/Features/Login/LoginResult.cs
namespace WardenId.Protocol.Application.Features.Login;

public sealed record LoginResult(bool RequiresAdditionalAction, TokenSet? Tokens);
```

- [ ] **Step 6: Write `Command`**

```csharp
// src/WardenId.Protocol.Application/Features/Login/Command.cs
using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.Login;

public sealed record Command(Guid TenantId, string EmailOrUsername, string Password) : IRequest<Result<LoginResult>>;
```

- [ ] **Step 7: Write `Handler`**

```csharp
// src/WardenId.Protocol.Application/Features/Login/Handler.cs
using System.Security.Cryptography;
using System.Text;
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Domain;

namespace WardenId.Protocol.Application.Features.Login;

public sealed class Handler(
    IAuthenticateUsers authenticator,
    IActiveSigningKeyProvider signingKeyProvider,
    IJwtIssuer jwtIssuer,
    IRefreshTokenRepository refreshTokenRepository,
    IOptions<ProtocolTokenOptions> options) : IRequestHandler<Command, Result<LoginResult>>
{
    public async Task<Result<LoginResult>> Handle(Command request, CancellationToken cancellationToken)
    {
        var authResult = await authenticator.AuthenticateAsync(
            request.TenantId, request.EmailOrUsername, request.Password, cancellationToken);
        if (authResult.IsFailed) return Result.Fail<LoginResult>(authResult.Errors);

        var user = authResult.Value;
        if (!user.IsActive || user.HasPendingActions)
            return Result.Ok(new LoginResult(RequiresAdditionalAction: true, Tokens: null));

        var keyResult = await signingKeyProvider.GetActiveSigningKeyAsync(request.TenantId, cancellationToken);
        if (keyResult.IsFailed) return Result.Fail<LoginResult>(keyResult.Errors);
        var key = keyResult.Value;

        var now = DateTimeOffset.UtcNow;
        var accessLifetime = options.Value.AccessTokenLifetime;

        var accessToken = jwtIssuer.IssueAccessToken(key, request.TenantId, user.UserId, now, accessLifetime);
        var idToken = jwtIssuer.IssueIdToken(
            key, request.TenantId, user.UserId, user.Email, user.EmailVerified, user.Username, now, options.Value.IdTokenLifetime);

        var (rawRefreshToken, refreshTokenHash) = GenerateRefreshToken();
        var refreshTokenResult = RefreshToken.Create(
            request.TenantId, user.UserId, refreshTokenHash, now.Add(options.Value.RefreshTokenLifetime));
        if (refreshTokenResult.IsFailed) return Result.Fail<LoginResult>(refreshTokenResult.Errors);

        await refreshTokenRepository.CreateAsync(refreshTokenResult.Value, user.UserId, cancellationToken);

        var tokens = new TokenSet(accessToken, rawRefreshToken, idToken, "Bearer", (int)accessLifetime.TotalSeconds);
        return Result.Ok(new LoginResult(RequiresAdditionalAction: false, tokens));
    }

    private static (string Raw, string Hash) GenerateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (raw, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))));
    }
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/WardenId.Protocol.Application.Tests/WardenId.Protocol.Application.Tests.csproj`
Expected: 5 passed.

- [ ] **Step 9: Restore and build the whole solution**

Run: `dotnet restore WardenId.slnx --force-evaluate && dotnet build WardenId.slnx`
Expected: build succeeds.

- [ ] **Step 10: Commit**

```bash
git add src/WardenId.Protocol.Application/Features tests/WardenId.Protocol.Application.Tests WardenId.slnx
git commit -m "feat: add Protocol.Application Login feature"
```

---

### Task 7: `WardenId.Protocol.Infrastructure` — persistence, JWT issuance, cross-module adapters

**Files:**
- Create: `src/WardenId.Protocol.Infrastructure/WardenId.Protocol.Infrastructure.csproj`
- Create: `src/WardenId.Protocol.Infrastructure/Persistence/SchemaConsts.cs`
- Create: `src/WardenId.Protocol.Infrastructure/Persistence/ProtocolDbContext.cs`
- Create: `src/WardenId.Protocol.Infrastructure/Persistence/ProtocolDbContextDesignTimeFactory.cs`
- Create: `src/WardenId.Protocol.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs`
- Create: `src/WardenId.Protocol.Infrastructure/Persistence/RefreshTokenRepository.cs`
- Create: `src/WardenId.Protocol.Infrastructure/JwtIssuer.cs`
- Create: `src/WardenId.Protocol.Infrastructure/MediatRUserAuthenticator.cs`
- Create: `src/WardenId.Protocol.Infrastructure/MediatRSigningKeyProvider.cs`
- Create: `src/WardenId.Protocol.Infrastructure/DependencyInjection.cs`
- Create: `src/WardenId.Protocol.Infrastructure/Persistence/Migrations/*` (generated by `dotnet ef`)
- Modify: `WardenId.slnx` — add project entry

**Interfaces:**
- Consumes: `IJwtIssuer`/`IAuthenticateUsers`/`IActiveSigningKeyProvider`/`IRefreshTokenRepository` (Task 4), `RefreshToken` (Task 1), `Identity.Application.Features.Users.Authenticate.Command`/`AuthenticatedUser` (Task 5), `Tenancy.Application.Features.SigningKeys.GetActiveForSigning.Query`/`ActiveSigningKeyDto` (Task 3), `SigningKeyProtectionPurposes.PrivateKey` (Task 2).
- Produces: `ProtocolDbContext` (EF DbContext, schema `Protocol`), `AddProtocolInfrastructure(this IServiceCollection, IConfiguration)` — consumed by the Gateway in Task 9.

- [ ] **Step 1: Write the project file**

```xml
<!-- src/WardenId.Protocol.Infrastructure/WardenId.Protocol.Infrastructure.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
      <ProjectReference Include="..\WardenId.SharedKernel.Persistence\WardenId.SharedKernel.Persistence.csproj" />
      <ProjectReference Include="..\WardenId.Protocol.Application\WardenId.Protocol.Application.csproj" />
      <ProjectReference Include="..\WardenId.Identity.Application\WardenId.Identity.Application.csproj" />
      <ProjectReference Include="..\WardenId.Tenancy.Application\WardenId.Tenancy.Application.csproj" />
    </ItemGroup>

    <ItemGroup>
      <PackageReference Include="Microsoft.AspNetCore.DataProtection.Abstractions" />
      <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
      <PackageReference Include="Microsoft.IdentityModel.JsonWebTokens" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: Add to `WardenId.slnx`**

Inside `/src/WardenId.Protocol/`:

```xml
    <Project Path="src/WardenId.Protocol.Infrastructure/WardenId.Protocol.Infrastructure.csproj" />
```

- [ ] **Step 3: Write `SchemaConsts` and the EF configuration**

```csharp
// src/WardenId.Protocol.Infrastructure/Persistence/SchemaConsts.cs
namespace WardenId.Protocol.Infrastructure.Persistence;

public static class SchemaConsts
{
    public const string RefreshTokensTable = "RefreshTokens";
}
```

```csharp
// src/WardenId.Protocol.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.Protocol.Domain;

namespace WardenId.Protocol.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable(SchemaConsts.RefreshTokensTable);

        builder.Property(r => r.TokenHash)
            .HasMaxLength(64) // SHA-256 hex digest length
            .IsRequired();

        builder.HasIndex(r => r.TokenHash).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.UserId });

        builder.Ignore(r => r.DomainEvents);
    }
}
```

- [ ] **Step 4: Write `ProtocolDbContext` and its design-time factory**

```csharp
// src/WardenId.Protocol.Infrastructure/Persistence/ProtocolDbContext.cs
using Microsoft.EntityFrameworkCore;
using WardenId.Protocol.Domain;
using WardenId.SharedKernel.Persistence;
using WardenId.SharedKernel.Persistence.Audit;

namespace WardenId.Protocol.Infrastructure.Persistence;

public sealed class ProtocolDbContext(
    DbContextOptions<ProtocolDbContext> options) : DbContextBase(options)
{
    public const string Schema = "Protocol";

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProtocolDbContext).Assembly);

        // AuditEventConfiguration lives in SharedKernel.Persistence, a different assembly —
        // ApplyConfigurationsFromAssembly above only scans this module's own assembly, so it
        // needs applying explicitly. AuditDbContext owns the migration for audit.events; every
        // other module maps the same entity but excludes it from its own migrations.
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.Entity<AuditEventRecord>().ToTable("events", "audit", t => t.ExcludeFromMigrations());

        base.OnModelCreating(modelBuilder);
    }
}
```

```csharp
// src/WardenId.Protocol.Infrastructure/Persistence/ProtocolDbContextDesignTimeFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using WardenId.SharedKernel.Persistence;

namespace WardenId.Protocol.Infrastructure.Persistence;

// Used by `dotnet ef` tooling only.
internal sealed class ProtocolDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ProtocolDbContext>
{
    public ProtocolDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), AppConfigurationConstants.PathToGateway))
            .AddJsonFile(AppConfigurationConstants.AppSettingFileName)
            .Build();

        var connectionString = configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey)
            ?? throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage);

        var options = new DbContextOptionsBuilder<ProtocolDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ProtocolDbContext(options);
    }
}
```

- [ ] **Step 5: Write `RefreshTokenRepository`**

```csharp
// src/WardenId.Protocol.Infrastructure/Persistence/RefreshTokenRepository.cs
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Domain;

namespace WardenId.Protocol.Infrastructure.Persistence;

public sealed class RefreshTokenRepository(ProtocolDbContext dbContext) : IRefreshTokenRepository
{
    public async Task CreateAsync(RefreshToken refreshToken, Guid userId, CancellationToken cancellationToken)
    {
        await dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }
}
```

- [ ] **Step 6: Write `JwtIssuer`**

```csharp
// src/WardenId.Protocol.Infrastructure/JwtIssuer.cs
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WardenId.Protocol.Application;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Tenancy.Application.Abstractions;

namespace WardenId.Protocol.Infrastructure;

/// <summary>
/// Decrypts a tenant's ES256 signing key (protected by Tenancy with the shared
/// SigningKeyProtectionPurposes.PrivateKey purpose string) and signs access/ID tokens with it.
/// This is the "only ever decrypted inside the Protocol module" consumer referenced in
/// Tenancy's SigningKeyMaterial doc comment.
/// </summary>
public sealed class JwtIssuer(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<ProtocolTokenOptions> options) : IJwtIssuer
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector(SigningKeyProtectionPurposes.PrivateKey);

    public string IssueAccessToken(ActiveSigningKey key, Guid tenantId, Guid userId, DateTimeOffset issuedAtUtc, TimeSpan lifetime) =>
        Sign(
            key, tenantId, userId, issuedAtUtc, lifetime,
            extraClaims: new Dictionary<string, object>
            {
                ["tenant_id"] = tenantId.ToString(),
                ["token_use"] = "access"
            });

    public string IssueIdToken(
        ActiveSigningKey key, Guid tenantId, Guid userId, string email, bool emailVerified, string username,
        DateTimeOffset issuedAtUtc, TimeSpan lifetime) =>
        Sign(
            key, tenantId, userId, issuedAtUtc, lifetime,
            extraClaims: new Dictionary<string, object>
            {
                ["email"] = email,
                ["email_verified"] = emailVerified,
                ["preferred_username"] = username,
                ["token_use"] = "id"
            });

    private string Sign(
        ActiveSigningKey key, Guid tenantId, Guid userId, DateTimeOffset issuedAtUtc, TimeSpan lifetime,
        Dictionary<string, object> extraClaims)
    {
        var privateKeyPkcs8 = _protector.Unprotect(key.EncryptedPrivateKey);
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(privateKeyPkcs8, out _);

            var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = key.KeyId.ToString("N") };
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

            var claims = new Dictionary<string, object>(extraClaims)
            {
                [JwtRegisteredClaimNames.Iss] = $"{options.Value.IssuerBaseUri}/tenants/{tenantId}",
                [JwtRegisteredClaimNames.Sub] = userId.ToString()
            };

            var descriptor = new SecurityTokenDescriptor
            {
                Claims = claims,
                IssuedAt = issuedAtUtc.UtcDateTime,
                NotBefore = issuedAtUtc.UtcDateTime,
                Expires = issuedAtUtc.Add(lifetime).UtcDateTime,
                SigningCredentials = credentials
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyPkcs8);
        }
    }
}
```

- [ ] **Step 7: Write the two MediatR adapters**

```csharp
// src/WardenId.Protocol.Infrastructure/MediatRUserAuthenticator.cs
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Features.Users.Authenticate;
using WardenId.Identity.Domain;
using WardenId.Protocol.Application.Abstractions;

namespace WardenId.Protocol.Infrastructure;

public sealed class MediatRUserAuthenticator(ISender sender) : IAuthenticateUsers
{
    public async Task<Result<AuthenticatedUserInfo>> AuthenticateAsync(
        Guid tenantId, string emailOrUsername, string password, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new Command(tenantId, emailOrUsername, password), cancellationToken);
        if (result.IsFailed) return Result.Fail<AuthenticatedUserInfo>(result.Errors);

        var user = result.Value;
        return Result.Ok(new AuthenticatedUserInfo(
            user.UserId,
            user.Email,
            !user.PendingActions.Contains(RequiredAction.VerifyEmail),
            user.Username,
            user.Status == UserStatus.Active,
            user.PendingActions.Count > 0));
    }
}
```

```csharp
// src/WardenId.Protocol.Infrastructure/MediatRSigningKeyProvider.cs
using FluentResults;
using MediatR;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Tenancy.Application.Features.SigningKeys.GetActiveForSigning;

namespace WardenId.Protocol.Infrastructure;

public sealed class MediatRSigningKeyProvider(ISender sender) : IActiveSigningKeyProvider
{
    public async Task<Result<ActiveSigningKey>> GetActiveSigningKeyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new Query(tenantId), cancellationToken);
        if (result.IsFailed) return Result.Fail<ActiveSigningKey>(result.Errors);

        var key = result.Value;
        return Result.Ok(new ActiveSigningKey(key.KeyId, key.Algorithm, key.EncryptedPrivateKey));
    }
}
```

- [ ] **Step 8: Write `DependencyInjection.cs`**

```csharp
// src/WardenId.Protocol.Infrastructure/DependencyInjection.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Infrastructure.Persistence;
using WardenId.SharedKernel.Persistence;

namespace WardenId.Protocol.Infrastructure;

public static class DependencyInjection
{
    public static void AddProtocolInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProtocolDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey) ??
                throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage),
                postgresSql =>
                    postgresSql.MigrationsHistoryTable("__EFMigrationsHistory", ProtocolDbContext.Schema));
            options.AddSharedInterceptors(serviceProvider);
        });

        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IJwtIssuer, JwtIssuer>();
        services.AddScoped<IAuthenticateUsers, MediatRUserAuthenticator>();
        services.AddScoped<IActiveSigningKeyProvider, MediatRSigningKeyProvider>();

        MigrateAsync(services).GetAwaiter().GetResult();
    }

    private static async Task MigrateAsync(IServiceCollection services)
    {
        var context = services.BuildServiceProvider().GetRequiredService<ProtocolDbContext>();
        await context.Database.MigrateAsync(CancellationToken.None);
    }
}
```

Note: this deliberately does **not** call `services.AddDataProtection()` — that's already called once, application-wide, by `Tenancy.Infrastructure.DependencyInjection.AddTenancyInfrastructure` (persisted via `PersistKeysToDbContext<TenancyDbContext>`). `JwtIssuer` only resolves the already-configured `IDataProtectionProvider` from DI, exactly like `Audit.Infrastructure`'s `Es256CheckpointSigner` does — a second `AddDataProtection()` call from this module would risk the "whichever module registers last wins the XmlRepository" problem documented on `Es256CheckpointSigner`.

- [ ] **Step 9: Restore, build, then generate the migration**

Run: `dotnet restore WardenId.slnx --force-evaluate && dotnet build WardenId.slnx`
Expected: build succeeds (this task can't fully build until Task 8/9 wire the Gateway too, if `Program.cs` isn't touched yet — building the `Protocol.Infrastructure` project alone is enough to validate this task: `dotnet build src/WardenId.Protocol.Infrastructure/WardenId.Protocol.Infrastructure.csproj`).

Once Task 9 has wired `AddProtocolModule` into `Program.cs` (so `dotnet ef` can discover the context through the Gateway's DI), generate the migration:

Run: `dotnet ef migrations add AddRefreshTokens --project src/WardenId.Protocol.Infrastructure --startup-project WardenId.Gateway --context ProtocolDbContext`
Expected: creates `src/WardenId.Protocol.Infrastructure/Persistence/Migrations/*_AddRefreshTokens.cs` and the model snapshot.

(If this step is done before Task 9's Gateway wiring exists, `dotnet ef` will fail to resolve `ProtocolDbContext` through the startup project — do Task 9 first if `AddProtocolInfrastructure` isn't called anywhere yet, then come back and run this command.)

- [ ] **Step 10: Commit**

```bash
git add src/WardenId.Protocol.Infrastructure WardenId.slnx
git commit -m "feat: add Protocol.Infrastructure — persistence, JWT issuance, MediatR adapters"
```

---

### Task 8: `WardenId.Protocol.Presentation` — login page + endpoint

**Files:**
- Create: `src/WardenId.Protocol.Presentation/WardenId.Protocol.Presentation.csproj`
- Create: `src/WardenId.Protocol.Presentation/Templates/Login.html`
- Create: `src/WardenId.Protocol.Presentation/Templates/LoginSuccess.html`
- Create: `src/WardenId.Protocol.Presentation/Features/Login/Request.cs`
- Create: `src/WardenId.Protocol.Presentation/Features/Login/LoginPageRenderer.cs`
- Create: `src/WardenId.Protocol.Presentation/Features/Login/Endpoint.cs`
- Modify: `WardenId.slnx` — add project entry

**Interfaces:**
- Consumes: `IReadFileService` (`WardenId.SharedKernel.FileOperation`, transitively available via `SharedKernel.Presentation` → `SharedKernel.Application` → `SharedKernel.FileOperation`), `Command`/`LoginResult`/`TokenSet` (Task 6), `IEndpoint` (`WardenId.SharedKernel.Presentation`).
- Produces: `GET /api/v{version:apiVersion}/tenants/{tenantId:guid}/auth/login`, `POST` same route — wired automatically by `EndpointExtensions.AddEndPoints`'s reflection scan, no manual registration.

- [ ] **Step 1: Write the project file**

```xml
<!-- src/WardenId.Protocol.Presentation/WardenId.Protocol.Presentation.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Asp.Versioning.Http" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\WardenId.SharedKernel.Presentation\WardenId.SharedKernel.Presentation.csproj" />
    <ProjectReference Include="..\WardenId.Protocol.Application\WardenId.Protocol.Application.csproj" />
  </ItemGroup>
  <ItemGroup>
    <None Include="Templates\**\*.html" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add to `WardenId.slnx`**

Inside `/src/WardenId.Protocol/`:

```xml
    <Project Path="src/WardenId.Protocol.Presentation/WardenId.Protocol.Presentation.csproj" />
```

- [ ] **Step 3: Write the HTML templates**

```html
<!-- src/WardenId.Protocol.Presentation/Templates/Login.html -->
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Sign in — WardenId</title>
<style>
  body { font-family: 'Segoe UI', Helvetica, Arial, sans-serif; background:#f2f4f8; margin:0; padding:32px 16px; }
  .card { max-width:360px; margin:0 auto; background:#fff; border-radius:16px; padding:32px; box-shadow:0 2px 16px rgba(17,24,39,0.06); }
  h1 { font-size:20px; color:#111827; margin:0 0 20px 0; }
  label { display:block; font-size:13px; color:#4b5563; margin-bottom:16px; }
  input { width:100%; box-sizing:border-box; padding:10px; margin-top:4px; border:1px solid #d1d5db; border-radius:8px; font-size:14px; }
  button { width:100%; padding:10px; background:#4f46e5; color:#fff; border:none; border-radius:8px; font-size:14px; cursor:pointer; }
  .error { color:#b91c1c; font-size:13px; margin:0 0 16px 0; }
  .error:empty { display:none; }
</style>
</head>
<body>
  <div class="card">
    <h1>Sign in to WardenId</h1>
    <p class="error">{{ErrorMessage}}</p>
    <form method="post">
      <label>Email or username
        <input type="text" name="EmailOrUsername" required autofocus>
      </label>
      <label>Password
        <input type="password" name="Password" required>
      </label>
      <button type="submit">Sign in</button>
    </form>
  </div>
</body>
</html>
```

```html
<!-- src/WardenId.Protocol.Presentation/Templates/LoginSuccess.html -->
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Signed in — WardenId</title>
<style>
  body { font-family: 'Segoe UI', Helvetica, Arial, sans-serif; background:#f2f4f8; margin:0; padding:32px 16px; }
  .card { max-width:560px; margin:0 auto; background:#fff; border-radius:16px; padding:32px; box-shadow:0 2px 16px rgba(17,24,39,0.06); }
  h1 { font-size:20px; color:#111827; margin:0 0 16px 0; }
  pre { background:#f5f5ff; border:1px solid #e0e0fb; border-radius:8px; padding:16px; overflow-x:auto; font-size:13px; }
</style>
</head>
<body>
  <div class="card">
    <h1>Signed in</h1>
    <pre>{{TokenPayloadJson}}</pre>
  </div>
</body>
</html>
```

- [ ] **Step 4: Write `Request`**

```csharp
// src/WardenId.Protocol.Presentation/Features/Login/Request.cs
using WardenId.Protocol.Application.Features.Login;

namespace WardenId.Protocol.Presentation.Features.Login;

public sealed record Request(string EmailOrUsername, string Password)
{
    public Command ToCommand(Guid tenantId) => new(tenantId, EmailOrUsername, Password);
}
```

- [ ] **Step 5: Write `LoginPageRenderer`**

```csharp
// src/WardenId.Protocol.Presentation/Features/Login/LoginPageRenderer.cs
using System.Net;
using WardenId.SharedKernel.FileOperation;

namespace WardenId.Protocol.Presentation.Features.Login;

internal static class LoginPageRenderer
{
    private const string LoginTemplatePath = "Templates/Login.html";
    private const string SuccessTemplatePath = "Templates/LoginSuccess.html";

    public static async Task<string> RenderFormAsync(
        IReadFileService readFileService, string errorMessage, CancellationToken cancellationToken)
    {
        var template = await ReadTemplateAsync(readFileService, LoginTemplatePath, cancellationToken);
        return template.Replace("{{ErrorMessage}}", WebUtility.HtmlEncode(errorMessage));
    }

    public static async Task<string> RenderSuccessAsync(
        IReadFileService readFileService, string tokenPayloadJson, CancellationToken cancellationToken)
    {
        var template = await ReadTemplateAsync(readFileService, SuccessTemplatePath, cancellationToken);
        return template.Replace("{{TokenPayloadJson}}", WebUtility.HtmlEncode(tokenPayloadJson));
    }

    private static async Task<string> ReadTemplateAsync(
        IReadFileService readFileService, string templatePath, CancellationToken cancellationToken)
    {
        var result = await readFileService.ReadFileContentAsync(templatePath, cancellationToken);
        return result.ValueOrDefault ?? throw new InvalidOperationException($"Template not found: {templatePath}");
    }
}
```

- [ ] **Step 6: Write `Endpoint`**

```csharp
// src/WardenId.Protocol.Presentation/Features/Login/Endpoint.cs
using System.Text.Json;
using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.FileOperation;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Protocol.Presentation.Features.Login;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Protocol")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = app.MapGroup("/api/v{version:apiVersion}/tenants/{tenantId:guid}/auth/login")
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithTags("Protocol - Login");

        group.MapGet("", async (
                [FromServices] IReadFileService readFileService,
                CancellationToken cancellationToken) =>
            {
                var html = await LoginPageRenderer.RenderFormAsync(readFileService, errorMessage: "", cancellationToken);
                return Results.Content(html, "text/html");
            })
            .WithSummary("Render the login page")
            .Produces(StatusCodes.Status200OK);

        group.MapPost("", async (
                [FromRoute] Guid tenantId,
                [FromForm] Request request,
                [FromServices] ISender sender,
                [FromServices] IReadFileService readFileService,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToCommand(tenantId), cancellationToken);

                if (result.IsFailed)
                {
                    var errorHtml = await LoginPageRenderer.RenderFormAsync(
                        readFileService, result.Errors.FirstOrDefault()?.Message ?? "Sign in failed.", cancellationToken);
                    return Results.Content(errorHtml, "text/html", statusCode: StatusCodes.Status400BadRequest);
                }

                if (result.Value.Tokens is null)
                {
                    var pendingHtml = await LoginPageRenderer.RenderFormAsync(
                        readFileService,
                        "Additional action required before you can sign in — verify your email or complete account setup.",
                        cancellationToken);
                    return Results.Content(pendingHtml, "text/html");
                }

                var payloadJson = JsonSerializer.Serialize(result.Value.Tokens, new JsonSerializerOptions { WriteIndented = true });
                var successHtml = await LoginPageRenderer.RenderSuccessAsync(readFileService, payloadJson, cancellationToken);
                return Results.Content(successHtml, "text/html");
            })
            .WithSummary("Authenticate and issue tokens")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }
}
```

- [ ] **Step 7: Restore and build**

Run: `dotnet restore WardenId.slnx --force-evaluate && dotnet build src/WardenId.Protocol.Presentation/WardenId.Protocol.Presentation.csproj`
Expected: build succeeds.

- [ ] **Step 8: Commit**

```bash
git add src/WardenId.Protocol.Presentation WardenId.slnx
git commit -m "feat: add Protocol.Presentation login page and endpoint"
```

---

### Task 9: Gateway wiring, config, migration, full build

**Files:**
- Create: `WardenId.Gateway/DependencyInjection/ProtocolDependencyInjection.cs`
- Modify: `WardenId.Gateway/Program.cs`
- Modify: `WardenId.Gateway/appsettings.json`

**Interfaces:**
- Consumes: `AddProtocolPresentation` (Task 8's `AddProtocolPresentation` — see Step 1 below, defined here since no other task defines it), `AddProtocolInfrastructure` (Task 7).

Note: Task 8 didn't add a `Protocol.Presentation/DependencyInjection.cs` — add it here alongside the Gateway wiring since it's a one-line pass-through and belongs with the rest of this task's DI plumbing.

- [ ] **Step 1: Add `Protocol.Presentation`'s `DependencyInjection.cs`**

```csharp
// src/WardenId.Protocol.Presentation/DependencyInjection.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardenId.Protocol.Application;

namespace WardenId.Protocol.Presentation;

public static class DependencyInjection
{
    public static void AddProtocolPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProtocolApplication(configuration);
    }
}
```

- [ ] **Step 2: Write the Gateway module DI file**

```csharp
// WardenId.Gateway/DependencyInjection/ProtocolDependencyInjection.cs
using WardenId.Protocol.Infrastructure;
using WardenId.Protocol.Presentation;

namespace WardenId.Gateway.DependencyInjection;

public static class ProtocolDependencyInjection
{
    public static void AddProtocolModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddProtocolPresentation(configuration);
        services.AddProtocolInfrastructure(configuration);
    }
}
```

- [ ] **Step 3: Wire it into `Program.cs`**

In `WardenId.Gateway/Program.cs`, add the using and the module call, right after `AddIdentityModule`:

```csharp
using WardenId.Gateway.DependencyInjection;
```

(already present)

```csharp
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddProtocolModule(builder.Configuration);
builder.Services.AddGateway(builder.Configuration);
```

- [ ] **Step 4: Add the `ProtocolToken` config section**

In `WardenId.Gateway/appsettings.json`, add a new top-level section (after `"Smtp"`, before `"OpenTelemetry"`):

```json
  "ProtocolToken": {
    "IssuerBaseUri": "https://wardenid.local",
    "AccessTokenLifetime": "00:15:00",
    "IdTokenLifetime": "00:15:00",
    "RefreshTokenLifetime": "30.00:00:00"
  },
```

- [ ] **Step 5: Restore and build the whole solution**

Run: `dotnet restore WardenId.slnx --force-evaluate && dotnet build WardenId.slnx`
Expected: build succeeds.

- [ ] **Step 6: Generate the `Protocol` migration (deferred from Task 7 — needs the Gateway wiring above to resolve `ProtocolDbContext`)**

Run: `dotnet ef migrations add AddRefreshTokens --project src/WardenId.Protocol.Infrastructure --startup-project WardenId.Gateway --context ProtocolDbContext`
Expected: creates `src/WardenId.Protocol.Infrastructure/Persistence/Migrations/*_AddRefreshTokens.cs`, `*.Designer.cs`, and updates `ProtocolDbContextModelSnapshot.cs`.

- [ ] **Step 7: Run every existing test project to confirm nothing regressed**

Run: `dotnet test WardenId.slnx`
Expected: all tests pass (Audit, ClientManagement.Domain, Identity.Application, Identity.Domain, Protocol.Domain, Protocol.Application).

- [ ] **Step 8: Commit**

```bash
git add src/WardenId.Protocol.Presentation/DependencyInjection.cs \
  WardenId.Gateway/DependencyInjection/ProtocolDependencyInjection.cs \
  WardenId.Gateway/Program.cs WardenId.Gateway/appsettings.json \
  src/WardenId.Protocol.Infrastructure/Persistence/Migrations
git commit -m "feat: wire Protocol module into the Gateway; add RefreshTokens migration"
```

---

### Task 10: Manual end-to-end verification

**Files:** none (verification only).

- [ ] **Step 1: Run the app**

Run: `dotnet run --project WardenId.Gateway`
Expected: starts without error; migrations for `Tenancy`, `Identity`, and `Protocol` all apply; master tenant/admin seed (if configured) runs.

- [ ] **Step 2: If testing against a pre-existing dev database, fix already-seeded signing keys**

Task 2 fixed `IsActive` going forward, but any tenant already seeded before this fix (e.g. a master tenant created by an earlier run) has an existing `SigningKeys` row with `IsActive = false`, which Task 3's query would never find. Either drop and recreate the dev database (migrations + seeders will run clean), or manually run:

```sql
UPDATE "Tenancy"."SigningKeys" SET "IsActive" = true;
```

- [ ] **Step 3: Register and verify a user via Swagger**

Open `/swagger` (Development only). Use the `Register` endpoint under a known tenant id, then `VerifyEmail` with the raw token returned in the response (no email dispatch is wired — see `Identity.Application`'s spec, unchanged by this plan).

- [ ] **Step 4: Load the login page in a browser**

Navigate to `/api/v1/tenants/{tenantId}/auth/login`. Expected: the login form renders.

- [ ] **Step 5: Submit valid credentials**

Expected: a page renders showing formatted JSON with `access_token`, `refresh_token`, `id_token`, `token_type: "Bearer"`, `expires_in`.

- [ ] **Step 6: Submit invalid credentials**

Expected: the login form re-renders with an inline error message, HTTP 400.

- [ ] **Step 7: Decode and verify the access token**

Paste `access_token` into jwt.io (or any JWT decoder). Confirm claims: `iss` = `{IssuerBaseUri}/tenants/{tenantId}`, `sub` = the user's id, `tenant_id`, `token_use: "access"`, `exp`/`iat`/`nbf` present, header `alg: ES256`, `kid` = the tenant's signing key id.

- [ ] **Step 8: Verify the signature against the tenant's public key**

Query `SELECT "PublicKeyJwk" FROM "Tenancy"."SigningKeys" WHERE "TenantId" = '{tenantId}' AND "IsActive" = true;` and verify the JWT's ES256 signature against that JWK (jwt.io's "verify signature" panel accepts a public key/JWK). Confirms `JwtIssuer` decrypted and used the correct private key.
