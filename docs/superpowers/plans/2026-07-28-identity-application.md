# Identity Application Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `WardenId.Identity.Application` — MediatR commands/queries covering all 12 `User` domain behaviors plus a login-orchestration `Authenticate` command, following the exact layered pattern already used by `WardenId.Tenancy.Application`/`WardenId.ClientManagement.Application`.

**Architecture:** One `Features/Users/<UseCase>/` folder per use case (`Command.cs`/`Query.cs` + `Handler.cs` + a DTO file where needed), an `Abstractions/` folder for `IUserRepository`/`IIdentityDbContext` (Infrastructure implements these in a future pass), `IdentityPolicyOptions` bound from `appsettings` for password/lockout/MFA policy defaults, and a `DependencyInjection.cs` registering MediatR + options binding.

**Tech Stack:** .NET 10, MediatR 12.5, FluentResults 4.0, EF Core 10 (abstractions + InMemory for tests only), xUnit 2.9, hand-written fakes (no mocking framework is used anywhere in this repo).

**Spec:** `docs/superpowers/specs/2026-07-27-identity-application-design.md`

## Global Constraints

- Target framework `net10.0`, nullable + implicit usings enabled (inherited from `Directory.Build.props`) — do not add per-project overrides.
- `RestorePackagesWithLockFile=true` is repo-wide — after adding any `PackageReference`, run `dotnet restore --force-evaluate` on the touched project so `packages.lock.json` regenerates. Commit the updated lock file with the code that needed it.
- Package versions are centrally managed in `Directory.Packages.props` — add new `PackageVersion` entries there, reference by name only (no `Version=` attribute) in each `.csproj`.
- Domain/Application layers return `FluentResults.Result`/`Result<T>` — never throw for expected failures. `Result.Fail(x.Errors)` propagates nested failures verbatim (matches `Tenancy.Application`/`ClientManagement.Application`).
- Audit-actor convention for this module (resolves an inconsistency between the two existing modules — Tenancy threads a real actor id, ClientManagement currently passes `TenantId` as filler): **self-service commands** (the user acting on their own account — register, verify email, authenticate, change password, reset password, enroll/verify MFA) pass the affected `User`'s own `Id` as the repository's actor-id parameter. **Admin/management commands** (unlock, disable, soft-delete, revoke MFA factor, set attribute, required-action add/clear) take an explicit `Guid ActorUserId` command field and pass that instead.
- No mocking framework exists in this repo (confirmed: not present in `Directory.Packages.props`) — all Application-layer tests use hand-written fakes (`FakeUserRepository`, `FakePasswordHasher`, `FakeBreachedPasswordChecker`), matching how `Identity.Domain.Tests` needs no mocking at all.
- Every mutating command loads the aggregate through `IUserRepository.GetByIdAsync`, calls exactly one `User` behavior method, and on success calls the matching repository persistence method (`UpdateAsync`/`SoftDeleteAsync`) before returning `Result.Ok()`. On domain-method failure, return the failure immediately without persisting.
- This plan is Application-layer only — no `Identity.Infrastructure`, `Identity.Presentation`, EF migrations, or Gateway wiring. `IIdentityDbContext`/`IUserRepository` are abstractions only; a `TestIdentityDbContext` (test-project-only, using EF Core InMemory) exists solely to exercise the two read-side queries and is **not** shipped as real Infrastructure — the mapping was hand-verified during planning (see Task 11) and Infrastructure is free to map the real `IdentityDbContext` differently later.

---

## Task 1: Project scaffolding, abstractions, options, and test fakes

**Files:**
- Modify: `Directory.Packages.props` — add `Microsoft.EntityFrameworkCore.InMemory` (10.0.10), `Microsoft.Extensions.Options` (10.0.0), `Microsoft.Extensions.Options.ConfigurationExtensions` (10.0.0)
- Modify: `WardenId.slnx` — add `Identity.Application` project and the new test project
- Create: `src/WardenId.Identity.Application/WardenId.Identity.Application.csproj`
- Create: `src/WardenId.Identity.Application/Abstractions/IUserRepository.cs`
- Create: `src/WardenId.Identity.Application/Abstractions/IIdentityDbContext.cs`
- Create: `src/WardenId.Identity.Application/IdentityPolicyOptions.cs`
- Create: `src/WardenId.Identity.Application/ConfigurationKeys.cs`
- Create: `src/WardenId.Identity.Application/DependencyInjection.cs`
- Create: `tests/WardenId.Identity.Application.Tests/WardenId.Identity.Application.Tests.csproj`
- Create: `tests/WardenId.Identity.Application.Tests/Fakes/FakeUserRepository.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Fakes/FakePasswordHasher.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Fakes/FakeBreachedPasswordChecker.cs`

**Interfaces:**
- Produces: `IUserRepository` (`CreateAsync`, `GetByIdAsync`, `GetByEmailAsync`, `GetByUsernameAsync`, `ExistsByEmailAsync`, `ExistsByUsernameAsync`, `UpdateAsync`, `SoftDeleteAsync`), `IIdentityDbContext` (`DbSet<User> Users`), `IdentityPolicyOptions` (all fields below), `FakeUserRepository`/`FakePasswordHasher`/`FakeBreachedPasswordChecker` — every later task's tests construct these directly.

- [ ] **Step 1: Add the three new package versions**

In `Directory.Packages.props`, insert alphabetically among the existing `Microsoft.EntityFrameworkCore*`/`Microsoft.Extensions.*` lines:

```xml
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.10" />
```
(directly after the `Microsoft.EntityFrameworkCore.Design` line)

```xml
    <PackageVersion Include="Microsoft.Extensions.Options" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="10.0.0" />
```
(directly after the `Microsoft.Extensions.Configuration.Json` line, before `Microsoft.Extensions.DependencyInjection.Abstractions`)

- [ ] **Step 2: Create the Application project file**

`src/WardenId.Identity.Application/WardenId.Identity.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
      <ProjectReference Include="..\WardenId.SharedKernel.Application\WardenId.SharedKernel.Application.csproj" />
      <ProjectReference Include="..\WardenId.Identity.Domain\WardenId.Identity.Domain.csproj" />
    </ItemGroup>

    <ItemGroup>
      <PackageReference Include="MediatR" />
      <PackageReference Include="Microsoft.Extensions.Options" />
      <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
    </ItemGroup>

</Project>
```

- [ ] **Step 3: Create the abstractions**

`src/WardenId.Identity.Application/Abstractions/IUserRepository.cs`:

```csharp
using FluentResults;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Abstractions;

public interface IUserRepository
{
    Task CreateAsync(User user, Guid userId, CancellationToken cancellationToken = default);

    Task<Result<User>> GetByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<Result<User>> GetByEmailAsync(Guid tenantId, Email email, CancellationToken cancellationToken = default);

    Task<Result<User>> GetByUsernameAsync(Guid tenantId, Username username, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(Guid tenantId, Email email, CancellationToken cancellationToken = default);

    Task<bool> ExistsByUsernameAsync(Guid tenantId, Username username, CancellationToken cancellationToken = default);

    Task UpdateAsync(User user, Guid userId, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(User user, Guid userId, CancellationToken cancellationToken = default);
}
```

`src/WardenId.Identity.Application/Abstractions/IIdentityDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WardenId.Identity.Domain;
using WardenId.SharedKernel.Application.Abstractions;

namespace WardenId.Identity.Application.Abstractions;

public interface IIdentityDbContext : IDbContext
{
    DbSet<User> Users { get; }
}
```

- [ ] **Step 4: Create the policy options and configuration keys**

`src/WardenId.Identity.Application/IdentityPolicyOptions.cs`:

```csharp
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application;

public sealed class IdentityPolicyOptions
{
    public int PasswordMinLength { get; init; } = 8;
    public bool PasswordRequireUppercase { get; init; } = true;
    public bool PasswordRequireDigit { get; init; } = true;
    public bool PasswordRequireSymbol { get; init; }
    public bool PasswordBreachCheckEnabled { get; init; } = true;
    public int PasswordHistoryCount { get; init; } = 5;
    public int CurrentPasswordAlgorithmVersion { get; init; } = 1;
    public int MaxFailedLoginAttempts { get; init; } = 5;
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);
    public MfaPolicyMode MfaPolicyMode { get; init; } = MfaPolicyMode.Optional;
}
```

`src/WardenId.Identity.Application/ConfigurationKeys.cs`:

```csharp
namespace WardenId.Identity.Application;

public static class ConfigurationKeys
{
    public static class IdentityPolicy
    {
        public const string Section = "IdentityPolicy";
    }
}
```

- [ ] **Step 5: Create the DependencyInjection entry point**

`src/WardenId.Identity.Application/DependencyInjection.cs`:

```csharp
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WardenId.Identity.Application;

public static class DependencyInjection
{
    public static void AddIdentityApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
        services.Configure<IdentityPolicyOptions>(configuration.GetSection(ConfigurationKeys.IdentityPolicy.Section));
    }
}
```

- [ ] **Step 6: Wire the new project into the solution**

In `WardenId.slnx`, change:

```xml
  <Folder Name="/src/WardenId.Identity/">
    <Project Path="src/WardenId.Identity.Domain/WardenId.Identity.Domain.csproj" />
  </Folder>
```

to:

```xml
  <Folder Name="/src/WardenId.Identity/">
    <Project Path="src/WardenId.Identity.Application/WardenId.Identity.Application.csproj" />
    <Project Path="src/WardenId.Identity.Domain/WardenId.Identity.Domain.csproj" />
  </Folder>
```

and change:

```xml
  <Folder Name="/tests/">
    <Project Path="tests/WardenId.ClientManagement.Domain.Tests/WardenId.ClientManagement.Domain.Tests.csproj" />
    <Project Path="tests/WardenId.Identity.Domain.Tests/WardenId.Identity.Domain.Tests.csproj" />
  </Folder>
```

to:

```xml
  <Folder Name="/tests/">
    <Project Path="tests/WardenId.ClientManagement.Domain.Tests/WardenId.ClientManagement.Domain.Tests.csproj" />
    <Project Path="tests/WardenId.Identity.Application.Tests/WardenId.Identity.Application.Tests.csproj" />
    <Project Path="tests/WardenId.Identity.Domain.Tests/WardenId.Identity.Domain.Tests.csproj" />
  </Folder>
```

- [ ] **Step 7: Create the test project file**

`tests/WardenId.Identity.Application.Tests/WardenId.Identity.Application.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\WardenId.Identity.Application\WardenId.Identity.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="FluentResults" />
    <Using Include="WardenId.Identity.Domain" />
    <Using Include="WardenId.Identity.Application" />
    <Using Include="WardenId.Identity.Application.Abstractions" />
    <Using Include="WardenId.Identity.Application.Tests.Fakes" />
  </ItemGroup>

</Project>
```

- [ ] **Step 8: Create the fakes**

`tests/WardenId.Identity.Application.Tests/Fakes/FakeUserRepository.cs`:

```csharp
using FluentResults;

namespace WardenId.Identity.Application.Tests.Fakes;

public sealed class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, User> _usersById = new();

    public Task CreateAsync(User user, Guid userId, CancellationToken cancellationToken = default)
    {
        _usersById[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task<Result<User>> GetByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = _usersById.GetValueOrDefault(userId);
        return Task.FromResult(user is not null && user.TenantId == tenantId
            ? Result.Ok(user)
            : Result.Fail<User>(UserErrors.NotFound));
    }

    public Task<Result<User>> GetByEmailAsync(Guid tenantId, Email email, CancellationToken cancellationToken = default)
    {
        var user = _usersById.Values.FirstOrDefault(u => u.TenantId == tenantId && u.Email == email);
        return Task.FromResult(user is not null ? Result.Ok(user) : Result.Fail<User>(UserErrors.NotFound));
    }

    public Task<Result<User>> GetByUsernameAsync(Guid tenantId, Username username, CancellationToken cancellationToken = default)
    {
        var user = _usersById.Values.FirstOrDefault(u => u.TenantId == tenantId && u.Username == username);
        return Task.FromResult(user is not null ? Result.Ok(user) : Result.Fail<User>(UserErrors.NotFound));
    }

    public Task<bool> ExistsByEmailAsync(Guid tenantId, Email email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_usersById.Values.Any(u => u.TenantId == tenantId && u.Email == email));

    public Task<bool> ExistsByUsernameAsync(Guid tenantId, Username username, CancellationToken cancellationToken = default) =>
        Task.FromResult(_usersById.Values.Any(u => u.TenantId == tenantId && u.Username == username));

    public Task UpdateAsync(User user, Guid userId, CancellationToken cancellationToken = default)
    {
        _usersById[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task SoftDeleteAsync(User user, Guid userId, CancellationToken cancellationToken = default)
    {
        _usersById[user.Id] = user;
        return Task.CompletedTask;
    }
}
```

`tests/WardenId.Identity.Application.Tests/Fakes/FakePasswordHasher.cs`:

```csharp
namespace WardenId.Identity.Application.Tests.Fakes;

public sealed class FakePasswordHasher : IPasswordHasher
{
    public bool ForceNeedsRehash { get; set; }

    public string Hash(string plaintext) => $"hashed:{plaintext}";

    public bool Verify(string plaintext, string hash) => hash == Hash(plaintext);

    public bool NeedsRehash(string hash) => ForceNeedsRehash;
}
```

`tests/WardenId.Identity.Application.Tests/Fakes/FakeBreachedPasswordChecker.cs`:

```csharp
namespace WardenId.Identity.Application.Tests.Fakes;

public sealed class FakeBreachedPasswordChecker : IBreachedPasswordChecker
{
    public bool IsBreached { get; set; }

    public Task<bool> IsBreachedAsync(string plaintext) => Task.FromResult(IsBreached);
}
```

- [ ] **Step 9: Build the solution**

Run: `dotnet build WardenId.slnx`
Expected: succeeds (0 errors). This is scaffolding only — no behavior to test yet, so the "test" for this task is a clean build.

- [ ] **Step 10: Restore lock files and commit**

```bash
dotnet restore WardenId.slnx --force-evaluate
git add Directory.Packages.props WardenId.slnx \
  src/WardenId.Identity.Application tests/WardenId.Identity.Application.Tests
git commit -m "feat: scaffold Identity.Application project, abstractions, and test fakes"
```

---

## Task 2: SecureTokenGenerator, PasswordPolicyFactory, and Register

**Files:**
- Modify: `src/WardenId.Identity.Domain/UserErrors.cs` — add `EmailAlreadyInUse()`/`UsernameAlreadyInUse()`
- Create: `src/WardenId.Identity.Application/SecureTokenGenerator.cs`
- Create: `src/WardenId.Identity.Application/PasswordPolicyFactory.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/Register/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/Register/UserRegistered.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/Register/Handler.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/Register/HandlerTests.cs`

**Interfaces:**
- Consumes: `IUserRepository`, `IPasswordHasher`, `IBreachedPasswordChecker` (Task 1), `IdentityPolicyOptions` (Task 1).
- Produces: `SecureTokenGenerator.Generate() -> (string Raw, string Hash)` and `SecureTokenGenerator.Hash(string raw) -> string` — reused by every later feature that issues a token or recovery code (Tasks 3, 6, 8). `PasswordPolicyFactory.Create(IdentityPolicyOptions) -> Result<PasswordPolicy>` — reused by Tasks 5 and 6. `Register.Command`/`Handler`/`UserRegistered(Guid UserId, string RawVerificationToken)`.

- [ ] **Step 1: Add the two new Domain error factories**

In `src/WardenId.Identity.Domain/UserErrors.cs`, add after `AttributeKeyRequired()`:

```csharp
    public static Error EmailAlreadyInUse() => Validation("Email is already in use.");
    public static Error UsernameAlreadyInUse() => Validation("Username is already in use.");
```

- [ ] **Step 2: Write the failing test for Register**

`tests/WardenId.Identity.Application.Tests/Features/Users/Register/HandlerTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Features.Users.Register;

namespace WardenId.Identity.Application.Tests.Features.Users.Register;

public class HandlerTests
{
    private static Handler CreateHandler(
        FakeUserRepository repository,
        FakePasswordHasher? passwordHasher = null,
        FakeBreachedPasswordChecker? breachedPasswordChecker = null,
        IdentityPolicyOptions? options = null) =>
        new(
            repository,
            passwordHasher ?? new FakePasswordHasher(),
            breachedPasswordChecker ?? new FakeBreachedPasswordChecker(),
            Options.Create(options ?? new IdentityPolicyOptions()));

    [Fact]
    public async Task Handle_ReturnsUserIdAndRawToken_WhenEmailAndUsernameAreUnique()
    {
        var handler = CreateHandler(new FakeUserRepository());

        var result = await handler.Handle(
            new Command(Guid.NewGuid(), "someone@example.com", "someone", "P@ssw0rd1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.UserId);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RawVerificationToken));
    }

    [Fact]
    public async Task Handle_Fails_WhenEmailAlreadyInUse()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeUserRepository();
        var handler = CreateHandler(repository);

        await handler.Handle(new Command(tenantId, "dup@example.com", "first", "P@ssw0rd1"), CancellationToken.None);
        var result = await handler.Handle(new Command(tenantId, "dup@example.com", "second", "P@ssw0rd1"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenUsernameAlreadyInUse()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeUserRepository();
        var handler = CreateHandler(repository);

        await handler.Handle(new Command(tenantId, "one@example.com", "dupname", "P@ssw0rd1"), CancellationToken.None);
        var result = await handler.Handle(new Command(tenantId, "two@example.com", "dupname", "P@ssw0rd1"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenPasswordViolatesPolicy()
    {
        var handler = CreateHandler(new FakeUserRepository(), options: new IdentityPolicyOptions { PasswordMinLength = 12 });

        var result = await handler.Handle(
            new Command(Guid.NewGuid(), "someone@example.com", "someone", "short1A"),
            CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenEmailFormatIsInvalid()
    {
        var handler = CreateHandler(new FakeUserRepository());

        var result = await handler.Handle(
            new Command(Guid.NewGuid(), "not-an-email", "someone", "P@ssw0rd1"),
            CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
```

- [ ] **Step 3: Run the test and confirm it fails to compile**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~Register"`
Expected: build error — `Command`, `Handler`, `UserRegistered` do not exist yet.

- [ ] **Step 4: Implement SecureTokenGenerator and PasswordPolicyFactory**

`src/WardenId.Identity.Application/SecureTokenGenerator.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace WardenId.Identity.Application;

internal static class SecureTokenGenerator
{
    public static (string Raw, string Hash) Generate()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return (raw, Hash(raw));
    }

    public static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
```

`src/WardenId.Identity.Application/PasswordPolicyFactory.cs`:

```csharp
using FluentResults;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application;

internal static class PasswordPolicyFactory
{
    public static Result<PasswordPolicy> Create(IdentityPolicyOptions options) =>
        PasswordPolicy.Create(
            options.PasswordMinLength,
            options.PasswordRequireUppercase,
            options.PasswordRequireDigit,
            options.PasswordRequireSymbol,
            options.PasswordBreachCheckEnabled,
            options.PasswordHistoryCount);
}
```

- [ ] **Step 5: Implement Register**

`src/WardenId.Identity.Application/Features/Users/Register/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Register;

public sealed record Command(
    Guid TenantId,
    string Email,
    string Username,
    string Password) : IRequest<Result<UserRegistered>>;
```

`src/WardenId.Identity.Application/Features/Users/Register/UserRegistered.cs`:

```csharp
namespace WardenId.Identity.Application.Features.Users.Register;

public sealed record UserRegistered(Guid UserId, string RawVerificationToken);
```

`src/WardenId.Identity.Application/Features/Users/Register/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Register;

public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Command, Result<UserRegistered>>
{
    public async Task<Result<UserRegistered>> Handle(Command request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailed) return Result.Fail(emailResult.Errors);

        var usernameResult = Username.Create(request.Username);
        if (usernameResult.IsFailed) return Result.Fail(usernameResult.Errors);

        var email = emailResult.Value;
        var username = usernameResult.Value;

        if (await repository.ExistsByEmailAsync(request.TenantId, email, cancellationToken))
            return Result.Fail(UserErrors.EmailAlreadyInUse());

        if (await repository.ExistsByUsernameAsync(request.TenantId, username, cancellationToken))
            return Result.Fail(UserErrors.UsernameAlreadyInUse());

        var policyResult = PasswordPolicyFactory.Create(options.Value);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value;

        var evaluateResult = await PasswordPolicyEvaluator.EvaluateAsync(
            policy, request.Password, currentCredential: null, history: [], passwordHasher, breachedPasswordChecker);
        if (evaluateResult.IsFailed) return evaluateResult;

        var registerResult = User.Register(request.TenantId, email, username);
        if (registerResult.IsFailed) return Result.Fail(registerResult.Errors);
        var user = registerResult.Value;

        var (rawToken, tokenHash) = SecureTokenGenerator.Generate();
        var verificationToken = EmailVerificationToken.Create(tokenHash, DateTimeOffset.UtcNow.AddHours(24));
        user.AddEmailVerificationToken(verificationToken);

        var credential = PasswordCredential.Create(
            passwordHasher.Hash(request.Password),
            "Argon2id",
            options.Value.CurrentPasswordAlgorithmVersion,
            DateTimeOffset.UtcNow);

        var setPasswordResult = user.SetPassword(credential, policy);
        if (setPasswordResult.IsFailed) return Result.Fail(setPasswordResult.Errors);

        await repository.CreateAsync(user, user.Id, cancellationToken);

        return Result.Ok(new UserRegistered(user.Id, rawToken));
    }
}
```

- [ ] **Step 6: Run the test and confirm it passes**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~Register"`
Expected: 5 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/WardenId.Identity.Domain/UserErrors.cs \
  src/WardenId.Identity.Application/SecureTokenGenerator.cs \
  src/WardenId.Identity.Application/PasswordPolicyFactory.cs \
  src/WardenId.Identity.Application/Features/Users/Register \
  tests/WardenId.Identity.Application.Tests/Features/Users/Register
git commit -m "feat: add Identity Register command"
```

---

## Task 3: RequestEmailVerification and VerifyEmail

**Files:**
- Create: `src/WardenId.Identity.Application/Features/Users/RequestEmailVerification/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/RequestEmailVerification/EmailVerificationRequested.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/RequestEmailVerification/Handler.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/VerifyEmail/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/VerifyEmail/Handler.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/RequestEmailVerification/HandlerTests.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/VerifyEmail/HandlerTests.cs`
- Create: `tests/WardenId.Identity.Application.Tests/TestUsers.cs` (shared seeding helper — from this task on, tests need an already-registered `User` without going through the `Register` command, to keep features test-isolated)

**Interfaces:**
- Consumes: `SecureTokenGenerator` (Task 2), `IUserRepository` (Task 1).
- Produces: `TestUsers.CreatePendingVerification(tenantId, email, username) -> User` — a plain Domain-only constructed user (registered, no token/password), used by this and all later feature tests that need a seeded user without depending on the `Register` handler.

- [ ] **Step 1: Add the shared test-seeding helper**

`tests/WardenId.Identity.Application.Tests/TestUsers.cs`:

```csharp
namespace WardenId.Identity.Application.Tests;

public static class TestUsers
{
    public static User CreatePendingVerification(Guid tenantId, string email = "someone@example.com", string username = "someone") =>
        User.Register(tenantId, Email.Create(email).Value, Username.Create(username).Value).Value;
}
```

- [ ] **Step 2: Write the failing tests**

`tests/WardenId.Identity.Application.Tests/Features/Users/RequestEmailVerification/HandlerTests.cs`:

```csharp
using WardenId.Identity.Application.Features.Users.RequestEmailVerification;

namespace WardenId.Identity.Application.Tests.Features.Users.RequestEmailVerification;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsRawToken_AndAddsTokenToUser()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);

        var handler = new Handler(repository);
        var result = await handler.Handle(new Command(tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RawVerificationToken));
        Assert.Single(user.EmailVerificationTokens);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserNotFound()
    {
        var handler = new Handler(new FakeUserRepository());

        var result = await handler.Handle(new Command(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
```

`tests/WardenId.Identity.Application.Tests/Features/Users/VerifyEmail/HandlerTests.cs`:

```csharp
using WardenId.Identity.Application.Features.Users.VerifyEmail;

namespace WardenId.Identity.Application.Tests.Features.Users.VerifyEmail;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ActivatesUser_WhenTokenIsValid()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var rawToken = "raw-token";
        user.AddEmailVerificationToken(EmailVerificationToken.Create(SecureTokenGenerator.Hash(rawToken), DateTimeOffset.UtcNow.AddHours(1)));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);

        var handler = new Handler(repository);
        var result = await handler.Handle(new Command(tenantId, user.Id, rawToken), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public async Task Handle_Fails_WhenTokenDoesNotMatch()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.AddEmailVerificationToken(EmailVerificationToken.Create(SecureTokenGenerator.Hash("correct-token"), DateTimeOffset.UtcNow.AddHours(1)));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);

        var handler = new Handler(repository);
        var result = await handler.Handle(new Command(tenantId, user.Id, "wrong-token"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserStatus.PendingVerification, user.Status);
    }
}
```

Note: `SecureTokenGenerator` is `internal` — the test project sees it because internals are visible within the same compiled assembly only if declared so; since the test project is a *separate* assembly, this test needs `SecureTokenGenerator.Hash` to be reachable. Change `SecureTokenGenerator` from `internal static class` to `public static class` now (in `src/WardenId.Identity.Application/SecureTokenGenerator.cs`, Task 2's file) — it is a small deliberate exception to "no test-only public surface," justified because these tests must construct tokens exactly the way the handlers do, and re-implementing SHA-256 hex hashing in the test project would silently drift from the real implementation.

- [ ] **Step 3: Run the tests and confirm they fail to compile**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~EmailVerification"`
Expected: build error — `Command`/`Handler`/`EmailVerificationRequested` (both features) do not exist yet.

- [ ] **Step 4: Make SecureTokenGenerator public**

In `src/WardenId.Identity.Application/SecureTokenGenerator.cs`, change:

```csharp
internal static class SecureTokenGenerator
```

to:

```csharp
public static class SecureTokenGenerator
```

- [ ] **Step 5: Implement RequestEmailVerification**

`src/WardenId.Identity.Application/Features/Users/RequestEmailVerification/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.RequestEmailVerification;

public sealed record Command(Guid TenantId, Guid UserId) : IRequest<Result<EmailVerificationRequested>>;
```

`src/WardenId.Identity.Application/Features/Users/RequestEmailVerification/EmailVerificationRequested.cs`:

```csharp
namespace WardenId.Identity.Application.Features.Users.RequestEmailVerification;

public sealed record EmailVerificationRequested(string RawVerificationToken);
```

`src/WardenId.Identity.Application/Features/Users/RequestEmailVerification/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RequestEmailVerification;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result<EmailVerificationRequested>>
{
    public async Task<Result<EmailVerificationRequested>> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        var (rawToken, tokenHash) = SecureTokenGenerator.Generate();
        var token = EmailVerificationToken.Create(tokenHash, DateTimeOffset.UtcNow.AddHours(24));
        user.AddEmailVerificationToken(token);

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok(new EmailVerificationRequested(rawToken));
    }
}
```

- [ ] **Step 6: Implement VerifyEmail**

`src/WardenId.Identity.Application/Features/Users/VerifyEmail/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.VerifyEmail;

public sealed record Command(Guid TenantId, Guid UserId, string RawToken) : IRequest<Result>;
```

`src/WardenId.Identity.Application/Features/Users/VerifyEmail/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application.Features.Users.VerifyEmail;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;
        var tokenHash = SecureTokenGenerator.Hash(request.RawToken);

        var verifyResult = user.VerifyEmail(tokenHash);
        if (verifyResult.IsFailed) return verifyResult;

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok();
    }
}
```

- [ ] **Step 7: Run the tests and confirm they pass**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~EmailVerification"`
Expected: 4 tests pass.

- [ ] **Step 8: Commit**

```bash
git add src/WardenId.Identity.Application/SecureTokenGenerator.cs \
  src/WardenId.Identity.Application/Features/Users/RequestEmailVerification \
  src/WardenId.Identity.Application/Features/Users/VerifyEmail \
  tests/WardenId.Identity.Application.Tests/TestUsers.cs \
  tests/WardenId.Identity.Application.Tests/Features/Users/RequestEmailVerification \
  tests/WardenId.Identity.Application.Tests/Features/Users/VerifyEmail
git commit -m "feat: add Identity email verification commands"
```

---

## Task 4: Authenticate

**Files:**
- Modify: `src/WardenId.Identity.Domain/UserErrors.cs` — add `InvalidCredentials()`
- Create: `src/WardenId.Identity.Application/Features/Users/Authenticate/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/Authenticate/AuthenticatedUser.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/Authenticate/Handler.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/Authenticate/HandlerTests.cs`

**Interfaces:**
- Consumes: `TestUsers.CreatePendingVerification` (Task 3), `IUserRepository`/`IPasswordHasher` (Task 1), `IdentityPolicyOptions` (Task 1).
- Produces: `Authenticate.Command`/`Handler`/`AuthenticatedUser(Guid UserId, UserStatus Status, IReadOnlyCollection<RequiredAction> PendingActions)`.

- [ ] **Step 1: Add the Domain error factory**

In `src/WardenId.Identity.Domain/UserErrors.cs`, add after `UsernameAlreadyInUse()`:

```csharp
    public static Error InvalidCredentials() => Validation("Invalid credentials.");
```

- [ ] **Step 2: Write the failing tests**

`tests/WardenId.Identity.Application.Tests/Features/Users/Authenticate/HandlerTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Features.Users.Authenticate;

namespace WardenId.Identity.Application.Tests.Features.Users.Authenticate;

public class HandlerTests
{
    private const string Password = "P@ssw0rd1";

    private static async Task<(Guid TenantId, User User, FakeUserRepository Repository, FakePasswordHasher Hasher)> SeedActiveUserAsync()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var hasher = new FakePasswordHasher();
        user.SetPassword(PasswordCredential.Create(hasher.Hash(Password), "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.FromTrustedSource(8, true, true, false, true, 5));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        return (tenantId, user, repository, hasher);
    }

    [Fact]
    public async Task Handle_Succeeds_WithCorrectPasswordByEmail()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", Password), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.UserId);
    }

    [Fact]
    public async Task Handle_Succeeds_WithCorrectPasswordByUsername()
    {
        var (tenantId, _, repository, hasher) = await SeedActiveUserAsync();
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone", Password), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_Fails_WithWrongPassword_AndRecordsFailedLogin()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", "wrong-password"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(1, user.Lockout.FailedAttemptCount);
    }

    [Fact]
    public async Task Handle_Fails_WithUnknownEmail_SameErrorAsWrongPassword()
    {
        var handler = new Handler(new FakeUserRepository(), new FakePasswordHasher(), Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "unknown@example.com", "whatever"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_Locks_AfterMaxFailedAttempts()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions { MaxFailedLoginAttempts = 1 }));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", "wrong-password"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.Equal(UserErrorCodes.AccountLocked, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_RejectsImmediately_WhenAlreadyLockedWithFutureLockout()
    {
        var (tenantId, user, repository, hasher) = await SeedActiveUserAsync();
        user.RecordFailedLogin(1, TimeSpan.FromMinutes(15));
        var handler = new Handler(repository, hasher, Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(tenantId, "someone@example.com", Password), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.AccountLocked, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Equal(1, user.Lockout.FailedAttemptCount);
    }
}
```

- [ ] **Step 3: Run the tests and confirm they fail to compile**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~Authenticate"`
Expected: build error — `Command`/`Handler`/`AuthenticatedUser` do not exist yet.

- [ ] **Step 4: Implement Authenticate**

`src/WardenId.Identity.Application/Features/Users/Authenticate/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Authenticate;

public sealed record Command(Guid TenantId, string EmailOrUsername, string Password) : IRequest<Result<AuthenticatedUser>>;
```

`src/WardenId.Identity.Application/Features/Users/Authenticate/AuthenticatedUser.cs`:

```csharp
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Authenticate;

public sealed record AuthenticatedUser(Guid UserId, UserStatus Status, IReadOnlyCollection<RequiredAction> PendingActions);
```

`src/WardenId.Identity.Application/Features/Users/Authenticate/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Authenticate;

public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Command, Result<AuthenticatedUser>>
{
    public async Task<Result<AuthenticatedUser>> Handle(Command request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.EmailOrUsername);

        var userResult = emailResult.IsSuccess
            ? await repository.GetByEmailAsync(request.TenantId, emailResult.Value, cancellationToken)
            : await LookupByUsernameAsync(request, cancellationToken);

        if (userResult.IsFailed) return Result.Fail(UserErrors.InvalidCredentials());

        var user = userResult.Value;

        if (user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } lockedUntil && lockedUntil > DateTimeOffset.UtcNow)
            return Result.Fail(UserErrors.AccountLocked(lockedUntil));

        if (user.Credential is null || !passwordHasher.Verify(request.Password, user.Credential.Hash))
        {
            var failResult = user.RecordFailedLogin(options.Value.MaxFailedLoginAttempts, options.Value.LockoutDuration);
            if (failResult.IsFailed) return Result.Fail(failResult.Errors);

            await repository.UpdateAsync(user, user.Id, cancellationToken);

            return user.Status == UserStatus.Locked && user.Lockout.LockedUntilUtc is { } newLockedUntil
                ? Result.Fail(UserErrors.AccountLocked(newLockedUntil))
                : Result.Fail(UserErrors.InvalidCredentials());
        }

        user.RecordSuccessfulLogin();

        if (user.Credential.NeedsRehash(options.Value.CurrentPasswordAlgorithmVersion))
            user.RehashCredential(passwordHasher.Hash(request.Password), options.Value.CurrentPasswordAlgorithmVersion);

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok(new AuthenticatedUser(user.Id, user.Status, user.PendingActions));
    }

    private async Task<Result<User>> LookupByUsernameAsync(Command request, CancellationToken cancellationToken)
    {
        var usernameResult = Username.Create(request.EmailOrUsername);
        if (usernameResult.IsFailed) return Result.Fail<User>(UserErrors.InvalidCredentials());

        return await repository.GetByUsernameAsync(request.TenantId, usernameResult.Value, cancellationToken);
    }
}
```

- [ ] **Step 5: Run the tests and confirm they pass**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~Authenticate"`
Expected: 6 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Domain/UserErrors.cs \
  src/WardenId.Identity.Application/Features/Users/Authenticate \
  tests/WardenId.Identity.Application.Tests/Features/Users/Authenticate
git commit -m "feat: add Identity Authenticate command"
```

---

## Task 5: ChangePassword

**Files:**
- Create: `src/WardenId.Identity.Application/Features/Users/ChangePassword/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/ChangePassword/Handler.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/ChangePassword/HandlerTests.cs`

**Interfaces:**
- Consumes: `PasswordPolicyFactory` (Task 2), `TestUsers` (Task 3).
- Produces: `ChangePassword.Command`/`Handler` (`Result`, no payload).

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Application.Tests/Features/Users/ChangePassword/HandlerTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Features.Users.ChangePassword;

namespace WardenId.Identity.Application.Tests.Features.Users.ChangePassword;

public class HandlerTests
{
    private const string CurrentPassword = "P@ssw0rd1";

    private static async Task<(User User, FakeUserRepository Repository, FakePasswordHasher Hasher)> SeedUserAsync()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var hasher = new FakePasswordHasher();
        user.SetPassword(
            PasswordCredential.Create(hasher.Hash(CurrentPassword), "Argon2id", 1, DateTimeOffset.UtcNow),
            PasswordPolicy.FromTrustedSource(8, true, true, false, true, 5));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        return (user, repository, hasher);
    }

    [Fact]
    public async Task Handle_UpdatesPassword_WhenCurrentPasswordIsCorrect()
    {
        var (user, repository, hasher) = await SeedUserAsync();
        var handler = new Handler(repository, hasher, new FakeBreachedPasswordChecker(), Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(
            new Command(user.TenantId, user.Id, CurrentPassword, "N3wP@ssword"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(hasher.Verify("N3wP@ssword", user.Credential!.Hash));
    }

    [Fact]
    public async Task Handle_Fails_WhenCurrentPasswordIsWrong()
    {
        var (user, repository, hasher) = await SeedUserAsync();
        var handler = new Handler(repository, hasher, new FakeBreachedPasswordChecker(), Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(
            new Command(user.TenantId, user.Id, "wrong-current-password", "N3wP@ssword"),
            CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenNewPasswordReusesCurrentPassword()
    {
        var (user, repository, hasher) = await SeedUserAsync();
        var handler = new Handler(repository, hasher, new FakeBreachedPasswordChecker(), Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(
            new Command(user.TenantId, user.Id, CurrentPassword, CurrentPassword),
            CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail to compile**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~ChangePassword"`
Expected: build error — `Command`/`Handler` do not exist yet.

- [ ] **Step 3: Implement ChangePassword**

`src/WardenId.Identity.Application/Features/Users/ChangePassword/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.ChangePassword;

public sealed record Command(Guid TenantId, Guid UserId, string CurrentPassword, string NewPassword) : IRequest<Result>;
```

`src/WardenId.Identity.Application/Features/Users/ChangePassword/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.ChangePassword;

public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        if (user.Credential is null || !passwordHasher.Verify(request.CurrentPassword, user.Credential.Hash))
            return Result.Fail(UserErrors.InvalidCredentials());

        var policyResult = PasswordPolicyFactory.Create(options.Value);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value;

        var evaluateResult = await PasswordPolicyEvaluator.EvaluateAsync(
            policy, request.NewPassword, user.Credential, user.PasswordHistory, passwordHasher, breachedPasswordChecker);
        if (evaluateResult.IsFailed) return evaluateResult;

        var credential = PasswordCredential.Create(
            passwordHasher.Hash(request.NewPassword), "Argon2id", options.Value.CurrentPasswordAlgorithmVersion, DateTimeOffset.UtcNow);

        var setPasswordResult = user.SetPassword(credential, policy);
        if (setPasswordResult.IsFailed) return setPasswordResult;

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok();
    }
}
```

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~ChangePassword"`
Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.Identity.Application/Features/Users/ChangePassword \
  tests/WardenId.Identity.Application.Tests/Features/Users/ChangePassword
git commit -m "feat: add Identity ChangePassword command"
```

---

## Task 6: RequestPasswordReset and ResetPassword

**Files:**
- Create: `src/WardenId.Identity.Application/Features/Users/RequestPasswordReset/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/RequestPasswordReset/PasswordResetRequested.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/RequestPasswordReset/Handler.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/ResetPassword/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/ResetPassword/Handler.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/RequestPasswordReset/HandlerTests.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/ResetPassword/HandlerTests.cs`

**Interfaces:**
- Consumes: `SecureTokenGenerator`, `PasswordPolicyFactory` (Task 2), `TestUsers` (Task 3).
- Produces: `RequestPasswordReset.Command`/`Handler`/`PasswordResetRequested(string RawResetToken)` (nullable payload), `ResetPassword.Command`/`Handler`.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Application.Tests/Features/Users/RequestPasswordReset/HandlerTests.cs`:

```csharp
using WardenId.Identity.Application.Features.Users.RequestPasswordReset;

namespace WardenId.Identity.Application.Tests.Features.Users.RequestPasswordReset;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsRawToken_WhenEmailMatchesAUser()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com");
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, "someone@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.RawResetToken));
        Assert.Single(user.PasswordResetTokens);
    }

    [Fact]
    public async Task Handle_ReturnsSuccessWithNullPayload_WhenEmailDoesNotMatch()
    {
        var handler = new Handler(new FakeUserRepository());

        var result = await handler.Handle(new Command(Guid.NewGuid(), "nobody@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }
}
```

`tests/WardenId.Identity.Application.Tests/Features/Users/ResetPassword/HandlerTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Features.Users.ResetPassword;

namespace WardenId.Identity.Application.Tests.Features.Users.ResetPassword;

public class HandlerTests
{
    [Fact]
    public async Task Handle_SetsNewPassword_AndClearsLockout_WhenTokenIsValid()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com");
        var hasher = new FakePasswordHasher();
        user.SetPassword(PasswordCredential.Create(hasher.Hash("OldP@ss1"), "Argon2id", 1, DateTimeOffset.UtcNow), PasswordPolicy.FromTrustedSource(8, true, true, false, true, 5));
        user.RecordFailedLogin(3, TimeSpan.FromMinutes(15));
        const string rawToken = "raw-reset-token";
        user.RequestPasswordReset(PasswordResetToken.Create(SecureTokenGenerator.Hash(rawToken), DateTimeOffset.UtcNow.AddHours(1)));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);

        var handler = new Handler(repository, hasher, new FakeBreachedPasswordChecker(), Options.Create(new IdentityPolicyOptions()));
        var result = await handler.Handle(new Command(tenantId, "someone@example.com", rawToken, "N3wP@ssword"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(hasher.Verify("N3wP@ssword", user.Credential!.Hash));
        Assert.Equal(0, user.Lockout.FailedAttemptCount);
    }

    [Fact]
    public async Task Handle_Fails_WhenTokenDoesNotMatch()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com");
        user.RequestPasswordReset(PasswordResetToken.Create(SecureTokenGenerator.Hash("correct-token"), DateTimeOffset.UtcNow.AddHours(1)));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);

        var handler = new Handler(repository, new FakePasswordHasher(), new FakeBreachedPasswordChecker(), Options.Create(new IdentityPolicyOptions()));
        var result = await handler.Handle(new Command(tenantId, "someone@example.com", "wrong-token", "N3wP@ssword"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenEmailDoesNotMatchAnyUser()
    {
        var handler = new Handler(new FakeUserRepository(), new FakePasswordHasher(), new FakeBreachedPasswordChecker(), Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Command(Guid.NewGuid(), "nobody@example.com", "any-token", "N3wP@ssword"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail to compile**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~PasswordReset"`
Expected: build error — neither feature's `Command`/`Handler` exists yet.

- [ ] **Step 3: Implement RequestPasswordReset**

`src/WardenId.Identity.Application/Features/Users/RequestPasswordReset/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.RequestPasswordReset;

public sealed record Command(Guid TenantId, string Email) : IRequest<Result<PasswordResetRequested?>>;
```

`src/WardenId.Identity.Application/Features/Users/RequestPasswordReset/PasswordResetRequested.cs`:

```csharp
namespace WardenId.Identity.Application.Features.Users.RequestPasswordReset;

public sealed record PasswordResetRequested(string RawResetToken);
```

`src/WardenId.Identity.Application/Features/Users/RequestPasswordReset/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RequestPasswordReset;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result<PasswordResetRequested?>>
{
    public async Task<Result<PasswordResetRequested?>> Handle(Command request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailed) return Result.Ok<PasswordResetRequested?>(null);

        var userResult = await repository.GetByEmailAsync(request.TenantId, emailResult.Value, cancellationToken);
        if (userResult.IsFailed) return Result.Ok<PasswordResetRequested?>(null);

        var user = userResult.Value;

        var (rawToken, tokenHash) = SecureTokenGenerator.Generate();
        var token = PasswordResetToken.Create(tokenHash, DateTimeOffset.UtcNow.AddHours(1));
        user.RequestPasswordReset(token);

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok<PasswordResetRequested?>(new PasswordResetRequested(rawToken));
    }
}
```

- [ ] **Step 4: Implement ResetPassword**

`src/WardenId.Identity.Application/Features/Users/ResetPassword/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.ResetPassword;

public sealed record Command(Guid TenantId, string Email, string RawToken, string NewPassword) : IRequest<Result>;
```

`src/WardenId.Identity.Application/Features/Users/ResetPassword/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.ResetPassword;

public sealed class Handler(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    IBreachedPasswordChecker breachedPasswordChecker,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailed) return Result.Fail(UserErrors.TokenInvalid());

        var userResult = await repository.GetByEmailAsync(request.TenantId, emailResult.Value, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(UserErrors.TokenInvalid());

        var user = userResult.Value;
        var tokenHash = SecureTokenGenerator.Hash(request.RawToken);

        var policyResult = PasswordPolicyFactory.Create(options.Value);
        if (policyResult.IsFailed) return Result.Fail(policyResult.Errors);
        var policy = policyResult.Value;

        var evaluateResult = await PasswordPolicyEvaluator.EvaluateAsync(
            policy, request.NewPassword, user.Credential, user.PasswordHistory, passwordHasher, breachedPasswordChecker);
        if (evaluateResult.IsFailed) return evaluateResult;

        var credential = PasswordCredential.Create(
            passwordHasher.Hash(request.NewPassword), "Argon2id", options.Value.CurrentPasswordAlgorithmVersion, DateTimeOffset.UtcNow);

        var resetResult = user.ResetPassword(tokenHash, credential, policy);
        if (resetResult.IsFailed) return resetResult;

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok();
    }
}
```

- [ ] **Step 5: Run the tests and confirm they pass**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~PasswordReset"`
Expected: 5 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Application/Features/Users/RequestPasswordReset \
  src/WardenId.Identity.Application/Features/Users/ResetPassword \
  tests/WardenId.Identity.Application.Tests/Features/Users/RequestPasswordReset \
  tests/WardenId.Identity.Application.Tests/Features/Users/ResetPassword
git commit -m "feat: add Identity password reset commands"
```

---

## Task 7: Unlock, Disable, and SoftDelete

**Files:**
- Create: `src/WardenId.Identity.Application/Features/Users/Unlock/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/Unlock/Handler.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/Disable/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/Disable/Handler.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/SoftDelete/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/SoftDelete/Handler.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/Unlock/HandlerTests.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/Disable/HandlerTests.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/SoftDelete/HandlerTests.cs`

**Interfaces:**
- Consumes: `TestUsers` (Task 3).
- Produces: `Unlock.Command(Guid TenantId, Guid UserId, Guid ActorUserId)`/`Handler`, `Disable.Command(Guid TenantId, Guid UserId, string Reason, Guid ActorUserId)`/`Handler`, `SoftDelete.Command(Guid TenantId, Guid UserId, Guid ActorUserId)`/`Handler` — all `Result`, no payload.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Application.Tests/Features/Users/Unlock/HandlerTests.cs`:

```csharp
using WardenId.Identity.Application.Features.Users.Unlock;

namespace WardenId.Identity.Application.Tests.Features.Users.Unlock;

public class HandlerTests
{
    [Fact]
    public async Task Handle_UnlocksUser_WhenCurrentlyLocked()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.RecordFailedLogin(1, TimeSpan.FromMinutes(15));
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserIsNotLocked()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
```

`tests/WardenId.Identity.Application.Tests/Features/Users/Disable/HandlerTests.cs`:

```csharp
using WardenId.Identity.Application.Features.Users.Disable;

namespace WardenId.Identity.Application.Tests.Features.Users.Disable;

public class HandlerTests
{
    [Fact]
    public async Task Handle_DisablesUser_WithReason()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, "policy violation", Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Disabled, user.Status);
    }

    [Fact]
    public async Task Handle_Fails_WhenReasonIsMissing()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, "", Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
```

`tests/WardenId.Identity.Application.Tests/Features/Users/SoftDelete/HandlerTests.cs`:

```csharp
using WardenId.Identity.Application.Features.Users.SoftDelete;

namespace WardenId.Identity.Application.Tests.Features.Users.SoftDelete;

public class HandlerTests
{
    [Fact]
    public async Task Handle_SoftDeletesUser()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.SoftDeleted, user.Status);
    }

    [Fact]
    public async Task Handle_Fails_WhenAlreadySoftDeleted()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.SoftDelete();
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail to compile**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~Unlock|FullyQualifiedName~Disable|FullyQualifiedName~SoftDelete"`
Expected: build error — none of the three features exist yet.

- [ ] **Step 3: Implement Unlock**

`src/WardenId.Identity.Application/Features/Users/Unlock/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Unlock;

public sealed record Command(Guid TenantId, Guid UserId, Guid ActorUserId) : IRequest<Result>;
```

`src/WardenId.Identity.Application/Features/Users/Unlock/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application.Features.Users.Unlock;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        var unlockResult = user.Unlock();
        if (unlockResult.IsFailed) return unlockResult;

        await repository.UpdateAsync(user, request.ActorUserId, cancellationToken);

        return Result.Ok();
    }
}
```

- [ ] **Step 4: Implement Disable**

`src/WardenId.Identity.Application/Features/Users/Disable/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Disable;

public sealed record Command(Guid TenantId, Guid UserId, string Reason, Guid ActorUserId) : IRequest<Result>;
```

`src/WardenId.Identity.Application/Features/Users/Disable/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application.Features.Users.Disable;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        var disableResult = user.Disable(request.Reason);
        if (disableResult.IsFailed) return disableResult;

        await repository.UpdateAsync(user, request.ActorUserId, cancellationToken);

        return Result.Ok();
    }
}
```

- [ ] **Step 5: Implement SoftDelete**

`src/WardenId.Identity.Application/Features/Users/SoftDelete/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.SoftDelete;

public sealed record Command(Guid TenantId, Guid UserId, Guid ActorUserId) : IRequest<Result>;
```

`src/WardenId.Identity.Application/Features/Users/SoftDelete/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application.Features.Users.SoftDelete;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        var softDeleteResult = user.SoftDelete();
        if (softDeleteResult.IsFailed) return softDeleteResult;

        await repository.SoftDeleteAsync(user, request.ActorUserId, cancellationToken);

        return Result.Ok();
    }
}
```

- [ ] **Step 6: Run the tests and confirm they pass**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~Unlock|FullyQualifiedName~Disable|FullyQualifiedName~SoftDelete"`
Expected: 6 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/WardenId.Identity.Application/Features/Users/Unlock \
  src/WardenId.Identity.Application/Features/Users/Disable \
  src/WardenId.Identity.Application/Features/Users/SoftDelete \
  tests/WardenId.Identity.Application.Tests/Features/Users/Unlock \
  tests/WardenId.Identity.Application.Tests/Features/Users/Disable \
  tests/WardenId.Identity.Application.Tests/Features/Users/SoftDelete
git commit -m "feat: add Identity Unlock, Disable, and SoftDelete commands"
```

---

## Task 8: EnrollMfaFactor

**Files:**
- Create: `src/WardenId.Identity.Application/Features/Users/EnrollMfaFactor/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/EnrollMfaFactor/MfaFactorEnrolled.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/EnrollMfaFactor/Handler.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/EnrollMfaFactor/HandlerTests.cs`

**Interfaces:**
- Consumes: `SecureTokenGenerator` (Task 2), `TestUsers` (Task 3).
- Produces: `EnrollMfaFactor.Command`/`Handler`/`MfaFactorEnrolled(Guid FactorId, IReadOnlyCollection<string> RawRecoveryCodes)`.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Application.Tests/Features/Users/EnrollMfaFactor/HandlerTests.cs`:

```csharp
using WardenId.Identity.Application.Features.Users.EnrollMfaFactor;

namespace WardenId.Identity.Application.Tests.Features.Users.EnrollMfaFactor;

public class HandlerTests
{
    [Fact]
    public async Task Handle_EnrollsFactor_AndReturnsTenRawRecoveryCodes()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, MfaFactorType.Totp, "opaque-blob"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.RawRecoveryCodes.Count);
        Assert.Equal(10, result.Value.RawRecoveryCodes.Distinct().Count());
        Assert.Single(user.MfaFactors);
        Assert.Equal(10, user.RecoveryCodes.Count);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserNotFound()
    {
        var handler = new Handler(new FakeUserRepository());

        var result = await handler.Handle(new Command(Guid.NewGuid(), Guid.NewGuid(), MfaFactorType.Totp, "opaque-blob"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail to compile**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~EnrollMfaFactor"`
Expected: build error — `Command`/`Handler`/`MfaFactorEnrolled` do not exist yet.

- [ ] **Step 3: Implement EnrollMfaFactor**

`src/WardenId.Identity.Application/Features/Users/EnrollMfaFactor/Command.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.EnrollMfaFactor;

public sealed record Command(
    Guid TenantId,
    Guid UserId,
    MfaFactorType FactorType,
    string CredentialData) : IRequest<Result<MfaFactorEnrolled>>;
```

`src/WardenId.Identity.Application/Features/Users/EnrollMfaFactor/MfaFactorEnrolled.cs`:

```csharp
namespace WardenId.Identity.Application.Features.Users.EnrollMfaFactor;

public sealed record MfaFactorEnrolled(Guid FactorId, IReadOnlyCollection<string> RawRecoveryCodes);
```

`src/WardenId.Identity.Application/Features/Users/EnrollMfaFactor/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.EnrollMfaFactor;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result<MfaFactorEnrolled>>
{
    private const int RecoveryCodeCount = 10;

    public async Task<Result<MfaFactorEnrolled>> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;
        var factor = MfaFactor.Create(request.FactorType, request.CredentialData);

        var rawCodes = new List<string>(RecoveryCodeCount);
        var recoveryCodes = new List<RecoveryCode>(RecoveryCodeCount);
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var (raw, hash) = SecureTokenGenerator.Generate();
            rawCodes.Add(raw);
            recoveryCodes.Add(RecoveryCode.Create(hash));
        }

        var enrollResult = user.EnrollMfaFactor(factor, recoveryCodes);
        if (enrollResult.IsFailed) return Result.Fail(enrollResult.Errors);

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok(new MfaFactorEnrolled(factor.Id, rawCodes));
    }
}
```

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~EnrollMfaFactor"`
Expected: 2 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.Identity.Application/Features/Users/EnrollMfaFactor \
  tests/WardenId.Identity.Application.Tests/Features/Users/EnrollMfaFactor
git commit -m "feat: add Identity EnrollMfaFactor command"
```

---

## Task 9: VerifyMfaFactor and RevokeMfaFactor

**Files:**
- Create: `src/WardenId.Identity.Application/Features/Users/VerifyMfaFactor/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/VerifyMfaFactor/Handler.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/RevokeMfaFactor/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/RevokeMfaFactor/Handler.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/VerifyMfaFactor/HandlerTests.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/RevokeMfaFactor/HandlerTests.cs`

**Interfaces:**
- Consumes: `TestUsers` (Task 3), `IdentityPolicyOptions.MfaPolicyMode` (Task 1).
- Produces: `VerifyMfaFactor.Command(Guid TenantId, Guid UserId, Guid FactorId)`/`Handler`, `RevokeMfaFactor.Command(Guid TenantId, Guid UserId, Guid FactorId, Guid ActorUserId, bool ActorIsAdmin)`/`Handler`.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Application.Tests/Features/Users/VerifyMfaFactor/HandlerTests.cs`:

```csharp
using WardenId.Identity.Application.Features.Users.VerifyMfaFactor;

namespace WardenId.Identity.Application.Tests.Features.Users.VerifyMfaFactor;

public class HandlerTests
{
    [Fact]
    public async Task Handle_VerifiesFactor_AndEnablesIt()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var factor = MfaFactor.Create(MfaFactorType.Totp, "opaque-blob");
        user.EnrollMfaFactor(factor, []);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(user.MfaFactors.Single().Enabled);
    }

    [Fact]
    public async Task Handle_Fails_WhenFactorNotFound()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
```

`tests/WardenId.Identity.Application.Tests/Features/Users/RevokeMfaFactor/HandlerTests.cs`:

```csharp
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Features.Users.RevokeMfaFactor;

namespace WardenId.Identity.Application.Tests.Features.Users.RevokeMfaFactor;

public class HandlerTests
{
    [Fact]
    public async Task Handle_RevokesFactor_WhenMfaIsOptional()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var factor = MfaFactor.Create(MfaFactorType.Totp, "opaque-blob");
        factor.Verify();
        user.EnrollMfaFactor(factor, []);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository, Options.Create(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.Optional }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, Guid.NewGuid(), ActorIsAdmin: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.MfaFactors);
    }

    [Fact]
    public async Task Handle_Fails_WhenRevokingLastFactorAndMfaIsRequired()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var factor = MfaFactor.Create(MfaFactorType.Totp, "opaque-blob");
        factor.Verify();
        user.EnrollMfaFactor(factor, []);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository, Options.Create(new IdentityPolicyOptions { MfaPolicyMode = MfaPolicyMode.Required }));

        var result = await handler.Handle(new Command(tenantId, user.Id, factor.Id, Guid.NewGuid(), ActorIsAdmin: false), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Single(user.MfaFactors);
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail to compile**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~VerifyMfaFactor|FullyQualifiedName~RevokeMfaFactor"`
Expected: build error — neither feature exists yet.

- [ ] **Step 3: Implement VerifyMfaFactor**

`src/WardenId.Identity.Application/Features/Users/VerifyMfaFactor/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.VerifyMfaFactor;

public sealed record Command(Guid TenantId, Guid UserId, Guid FactorId) : IRequest<Result>;
```

`src/WardenId.Identity.Application/Features/Users/VerifyMfaFactor/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application.Features.Users.VerifyMfaFactor;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        var verifyResult = user.VerifyMfaFactor(request.FactorId);
        if (verifyResult.IsFailed) return verifyResult;

        await repository.UpdateAsync(user, user.Id, cancellationToken);

        return Result.Ok();
    }
}
```

- [ ] **Step 4: Implement RevokeMfaFactor**

`src/WardenId.Identity.Application/Features/Users/RevokeMfaFactor/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.RevokeMfaFactor;

public sealed record Command(
    Guid TenantId,
    Guid UserId,
    Guid FactorId,
    Guid ActorUserId,
    bool ActorIsAdmin) : IRequest<Result>;
```

`src/WardenId.Identity.Application/Features/Users/RevokeMfaFactor/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RevokeMfaFactor;

public sealed class Handler(
    IUserRepository repository,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;
        var policy = MfaPolicy.Create(options.Value.MfaPolicyMode);

        var revokeResult = user.RevokeMfaFactor(request.FactorId, policy, request.ActorIsAdmin);
        if (revokeResult.IsFailed) return revokeResult;

        await repository.UpdateAsync(user, request.ActorUserId, cancellationToken);

        return Result.Ok();
    }
}
```

- [ ] **Step 5: Run the tests and confirm they pass**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~VerifyMfaFactor|FullyQualifiedName~RevokeMfaFactor"`
Expected: 4 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Application/Features/Users/VerifyMfaFactor \
  src/WardenId.Identity.Application/Features/Users/RevokeMfaFactor \
  tests/WardenId.Identity.Application.Tests/Features/Users/VerifyMfaFactor \
  tests/WardenId.Identity.Application.Tests/Features/Users/RevokeMfaFactor
git commit -m "feat: add Identity VerifyMfaFactor and RevokeMfaFactor commands"
```

---

## Task 10: SetAttribute and RequiredActions

**Files:**
- Create: `src/WardenId.Identity.Application/Features/Users/SetAttribute/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/SetAttribute/Handler.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/RequiredActions/Add/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/RequiredActions/Add/Handler.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/RequiredActions/Clear/Command.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/RequiredActions/Clear/Handler.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/SetAttribute/HandlerTests.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/RequiredActions/Add/HandlerTests.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/RequiredActions/Clear/HandlerTests.cs`

**Interfaces:**
- Consumes: `TestUsers` (Task 3).
- Produces: `SetAttribute.Command(Guid TenantId, Guid UserId, string Key, string Value, UserAttributeValueType ValueType, Guid ActorUserId)`/`Handler`, `RequiredActions.Add.Command(Guid TenantId, Guid UserId, RequiredAction Action, Guid ActorUserId)`/`Handler`, `RequiredActions.Clear.Command(Guid TenantId, Guid UserId, RequiredAction Action, Guid ActorUserId)`/`Handler`.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Application.Tests/Features/Users/SetAttribute/HandlerTests.cs`:

```csharp
using WardenId.Identity.Application.Features.Users.SetAttribute;

namespace WardenId.Identity.Application.Tests.Features.Users.SetAttribute;

public class HandlerTests
{
    [Fact]
    public async Task Handle_AddsAttribute_WhenKeyIsNew()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(
            new Command(tenantId, user.Id, "department", "engineering", UserAttributeValueType.String, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("engineering", user.Attributes.Single(a => a.Key == "department").Value);
    }

    [Fact]
    public async Task Handle_UpdatesExistingAttribute_WhenKeyAlreadyExists()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        user.SetAttribute("department", "sales", UserAttributeValueType.String);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(
            new Command(tenantId, user.Id, "department", "engineering", UserAttributeValueType.String, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(user.Attributes);
        Assert.Equal("engineering", user.Attributes.Single().Value);
    }
}
```

`tests/WardenId.Identity.Application.Tests/Features/Users/RequiredActions/Add/HandlerTests.cs`:

```csharp
using WardenId.Identity.Application.Features.Users.RequiredActions.Add;

namespace WardenId.Identity.Application.Tests.Features.Users.RequiredActions.Add;

public class HandlerTests
{
    [Fact]
    public async Task Handle_AddsRequiredAction()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, RequiredAction.AcceptTos, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(RequiredAction.AcceptTos, user.PendingActions);
    }
}
```

`tests/WardenId.Identity.Application.Tests/Features/Users/RequiredActions/Clear/HandlerTests.cs`:

```csharp
using WardenId.Identity.Application.Features.Users.RequiredActions.Clear;

namespace WardenId.Identity.Application.Tests.Features.Users.RequiredActions.Clear;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ClearsRequiredAction()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var repository = new FakeUserRepository();
        await repository.CreateAsync(user, user.Id, CancellationToken.None);
        var handler = new Handler(repository);

        var result = await handler.Handle(new Command(tenantId, user.Id, RequiredAction.VerifyEmail, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(RequiredAction.VerifyEmail, user.PendingActions);
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail to compile**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~SetAttribute|FullyQualifiedName~RequiredActions"`
Expected: build error — none of the three features exist yet.

- [ ] **Step 3: Implement SetAttribute**

`src/WardenId.Identity.Application/Features/Users/SetAttribute/Command.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.SetAttribute;

public sealed record Command(
    Guid TenantId,
    Guid UserId,
    string Key,
    string Value,
    UserAttributeValueType ValueType,
    Guid ActorUserId) : IRequest<Result>;
```

`src/WardenId.Identity.Application/Features/Users/SetAttribute/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application.Features.Users.SetAttribute;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;

        var setAttributeResult = user.SetAttribute(request.Key, request.Value, request.ValueType);
        if (setAttributeResult.IsFailed) return setAttributeResult;

        await repository.UpdateAsync(user, request.ActorUserId, cancellationToken);

        return Result.Ok();
    }
}
```

- [ ] **Step 4: Implement RequiredActions/Add and RequiredActions/Clear**

`src/WardenId.Identity.Application/Features/Users/RequiredActions/Add/Command.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RequiredActions.Add;

public sealed record Command(Guid TenantId, Guid UserId, RequiredAction Action, Guid ActorUserId) : IRequest<Result>;
```

`src/WardenId.Identity.Application/Features/Users/RequiredActions/Add/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application.Features.Users.RequiredActions.Add;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;
        user.AddRequiredAction(request.Action);

        await repository.UpdateAsync(user, request.ActorUserId, cancellationToken);

        return Result.Ok();
    }
}
```

`src/WardenId.Identity.Application/Features/Users/RequiredActions/Clear/Command.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.RequiredActions.Clear;

public sealed record Command(Guid TenantId, Guid UserId, RequiredAction Action, Guid ActorUserId) : IRequest<Result>;
```

`src/WardenId.Identity.Application/Features/Users/RequiredActions/Clear/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;

namespace WardenId.Identity.Application.Features.Users.RequiredActions.Clear;

public sealed class Handler(IUserRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var userResult = await repository.GetByIdAsync(request.TenantId, request.UserId, cancellationToken);
        if (userResult.IsFailed) return Result.Fail(userResult.Errors);

        var user = userResult.Value;
        user.ClearRequiredAction(request.Action);

        await repository.UpdateAsync(user, request.ActorUserId, cancellationToken);

        return Result.Ok();
    }
}
```

- [ ] **Step 5: Run the tests and confirm they pass**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~SetAttribute|FullyQualifiedName~RequiredActions"`
Expected: 4 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Application/Features/Users/SetAttribute \
  src/WardenId.Identity.Application/Features/Users/RequiredActions \
  tests/WardenId.Identity.Application.Tests/Features/Users/SetAttribute \
  tests/WardenId.Identity.Application.Tests/Features/Users/RequiredActions
git commit -m "feat: add Identity SetAttribute and RequiredActions commands"
```

---

## Task 11: Get query

**Files:**
- Create: `src/WardenId.Identity.Application/Features/Users/Get/Query.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/Get/UserDetail.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/Get/Handler.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Fakes/TestIdentityDbContext.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/Get/HandlerTests.cs`

**Interfaces:**
- Consumes: `IIdentityDbContext` (Task 1).
- Produces: `TestIdentityDbContext` (test-only EF Core InMemory context implementing `IIdentityDbContext`, mapping verified by hand during planning — see the Global Constraints note) — reused by Task 12. `Get.Query(Guid TenantId, Guid UserId)`/`Handler`/`UserDetail`.

- [ ] **Step 1: Add the test-only EF InMemory DbContext**

This exact mapping was hand-verified against `Microsoft.EntityFrameworkCore.InMemory` 10.0.10 during planning (constructing a `User`, seeding, and querying through a fresh context instance all worked) — implement it verbatim.

`tests/WardenId.Identity.Application.Tests/Fakes/TestIdentityDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Tests.Fakes;

public sealed class TestIdentityDbContext(DbContextOptions<TestIdentityDbContext> options)
    : DbContext(options), IIdentityDbContext
{
    public DbSet<User> Users => Set<User>();

    public Task<int> SaveChangesAsync(Guid userId, CancellationToken cancellationToken) =>
        SaveChangesAsync(cancellationToken);

    public Task BeginTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CommitTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RollbackTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.Ignore(u => u.DomainEvents);

            builder.Property(u => u.Email)
                .HasConversion(e => e.Value, v => Email.FromTrustedSource(v));
            builder.Property(u => u.Username)
                .HasConversion(u2 => u2.Value, v => Username.FromTrustedSource(v));

            builder.OwnsOne(u => u.Lockout, lockout =>
            {
                lockout.Property(l => l.FailedAttemptCount);
                lockout.Property(l => l.LockedUntilUtc);
            });

            builder.OwnsOne(u => u.Credential, credential =>
            {
                credential.Property(c => c.Hash);
                credential.Property(c => c.Algorithm);
                credential.Property(c => c.AlgorithmVersion);
                credential.Property(c => c.SetAtUtc);
            });

            builder.HasMany(u => u.MfaFactors).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(u => u.MfaFactors).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(u => u.RecoveryCodes).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(u => u.RecoveryCodes).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(u => u.Attributes).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(u => u.Attributes).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(u => u.PasswordHistory).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(u => u.PasswordHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(u => u.EmailVerificationTokens).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(u => u.EmailVerificationTokens).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(u => u.PasswordResetTokens).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(u => u.PasswordResetTokens).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    public static TestIdentityDbContext CreateNew(string databaseName) =>
        new(new DbContextOptionsBuilder<TestIdentityDbContext>().UseInMemoryDatabase(databaseName).Options);
}
```

- [ ] **Step 2: Write the failing test**

`tests/WardenId.Identity.Application.Tests/Features/Users/Get/HandlerTests.cs`:

```csharp
using WardenId.Identity.Application.Features.Users.Get;

namespace WardenId.Identity.Application.Tests.Features.Users.Get;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsUserDetail_WhenUserExists()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId, "someone@example.com", "someone");
        user.MarkAsCreated(Guid.NewGuid());

        using (var seedContext = TestIdentityDbContext.CreateNew(databaseName))
        {
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("someone@example.com", result.Value.Email);
        Assert.Equal("someone", result.Value.Username);
        Assert.Equal(UserStatus.PendingVerification, result.Value.Status);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserDoesNotExist()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenUserBelongsToADifferentTenant()
    {
        var databaseName = Guid.NewGuid().ToString();
        var user = TestUsers.CreatePendingVerification(Guid.NewGuid());
        using (var seedContext = TestIdentityDbContext.CreateNew(databaseName))
        {
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(Guid.NewGuid(), user.Id), CancellationToken.None);

        Assert.True(result.IsFailed);
    }
}
```

- [ ] **Step 3: Run the test and confirm it fails to compile**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~Features.Users.Get"`
Expected: build error — `Query`/`Handler`/`UserDetail` do not exist yet.

- [ ] **Step 4: Implement Get**

`src/WardenId.Identity.Application/Features/Users/Get/Query.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.Identity.Application.Features.Users.Get;

public sealed record Query(Guid TenantId, Guid UserId) : IRequest<Result<UserDetail>>;
```

`src/WardenId.Identity.Application/Features/Users/Get/UserDetail.cs`:

```csharp
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Get;

public sealed record UserAttributeSummary(string Key, string Value, UserAttributeValueType ValueType);

public sealed record MfaFactorSummary(Guid Id, MfaFactorType FactorType, bool Enabled);

public sealed record UserDetail(
    Guid Id,
    string Email,
    string Username,
    UserStatus Status,
    int FailedLoginAttemptCount,
    DateTimeOffset? LockedUntilUtc,
    IReadOnlyCollection<RequiredAction> PendingActions,
    IReadOnlyCollection<UserAttributeSummary> Attributes,
    IReadOnlyCollection<MfaFactorSummary> MfaFactors,
    DateTimeOffset CreatedAt);
```

`src/WardenId.Identity.Application/Features/Users/Get/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Get;

public sealed class Handler(IIdentityDbContext dbContext) : IRequestHandler<Query, Result<UserDetail>>
{
    public async Task<Result<UserDetail>> Handle(Query request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Where(u => u.Id == request.UserId && u.TenantId == request.TenantId && !u.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null) return Result.Fail(UserErrors.NotFound);

        var detail = new UserDetail(
            user.Id,
            user.Email.Value,
            user.Username.Value,
            user.Status,
            user.Lockout.FailedAttemptCount,
            user.Lockout.LockedUntilUtc,
            user.PendingActions.ToArray(),
            user.Attributes.Select(a => new UserAttributeSummary(a.Key, a.Value, a.ValueType)).ToArray(),
            user.MfaFactors.Select(f => new MfaFactorSummary(f.Id, f.FactorType, f.Enabled)).ToArray(),
            user.CreatedAt);

        return Result.Ok(detail);
    }
}
```

- [ ] **Step 5: Run the tests and confirm they pass**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~Features.Users.Get"`
Expected: 3 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Application/Features/Users/Get \
  tests/WardenId.Identity.Application.Tests/Fakes/TestIdentityDbContext.cs \
  tests/WardenId.Identity.Application.Tests/Features/Users/Get
git commit -m "feat: add Identity Get query"
```

---

## Task 12: List query

**Files:**
- Create: `src/WardenId.Identity.Application/Features/Users/List/Query.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/List/UserListItem.cs`
- Create: `src/WardenId.Identity.Application/Features/Users/List/Handler.cs`
- Create: `tests/WardenId.Identity.Application.Tests/Features/Users/List/HandlerTests.cs`

**Interfaces:**
- Consumes: `TestIdentityDbContext` (Task 11).
- Produces: `List.Query(Guid TenantId, int PageNumber, int PageSize, UserStatus? Status)`/`Handler`/`UserListItem`.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.Identity.Application.Tests/Features/Users/List/HandlerTests.cs`:

```csharp
using WardenId.Identity.Application.Features.Users.List;

namespace WardenId.Identity.Application.Tests.Features.Users.List;

public class HandlerTests
{
    private static async Task<(string DatabaseName, Guid TenantId)> SeedThreeUsersAsync()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        using var seedContext = TestIdentityDbContext.CreateNew(databaseName);
        for (var i = 0; i < 3; i++)
        {
            var user = TestUsers.CreatePendingVerification(tenantId, $"someone{i}@example.com", $"someone{i}");
            user.MarkAsCreated(Guid.NewGuid());
            seedContext.Users.Add(user);
        }
        await seedContext.SaveChangesAsync(CancellationToken.None);

        return (databaseName, tenantId);
    }

    [Fact]
    public async Task Handle_ReturnsTotalCountAndRequestedPage()
    {
        var (databaseName, tenantId) = await SeedThreeUsersAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, PageNumber: 1, PageSize: 2, Status: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.RecordsTotal);
        Assert.Equal(2, result.Value.Data.Count());
    }

    [Fact]
    public async Task Handle_ReturnsSecondPage()
    {
        var (databaseName, tenantId) = await SeedThreeUsersAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, PageNumber: 2, PageSize: 2, Status: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Data);
    }

    [Fact]
    public async Task Handle_FiltersByStatus()
    {
        var (databaseName, tenantId) = await SeedThreeUsersAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(tenantId, PageNumber: 1, PageSize: 10, Status: UserStatus.Active), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.RecordsTotal);
    }

    [Fact]
    public async Task Handle_ExcludesOtherTenants()
    {
        var (databaseName, _) = await SeedThreeUsersAsync();
        using var queryContext = TestIdentityDbContext.CreateNew(databaseName);
        var handler = new Handler(queryContext);

        var result = await handler.Handle(new Query(Guid.NewGuid(), PageNumber: 1, PageSize: 10, Status: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.RecordsTotal);
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail to compile**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~Features.Users.List"`
Expected: build error — `Query`/`Handler`/`UserListItem` do not exist yet.

- [ ] **Step 3: Implement List**

`src/WardenId.Identity.Application/Features/Users/List/Query.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.Identity.Domain;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Identity.Application.Features.Users.List;

public sealed record Query(
    Guid TenantId,
    int PageNumber,
    int PageSize,
    UserStatus? Status) : IRequest<Result<PaginationListResponse<UserListItem>>>;
```

`src/WardenId.Identity.Application/Features/Users/List/UserListItem.cs`:

```csharp
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.List;

public sealed record UserListItem(
    Guid Id,
    string Email,
    string Username,
    UserStatus Status,
    DateTimeOffset CreatedAt);
```

`src/WardenId.Identity.Application/Features/Users/List/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.Identity.Application.Abstractions;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Identity.Application.Features.Users.List;

public sealed class Handler(IIdentityDbContext dbContext) : IRequestHandler<Query, Result<PaginationListResponse<UserListItem>>>
{
    public async Task<Result<PaginationListResponse<UserListItem>>> Handle(Query request, CancellationToken cancellationToken)
    {
        var query = dbContext.Users.Where(u => u.TenantId == request.TenantId && !u.IsDeleted);

        if (request.Status is not null)
            query = query.Where(u => u.Status == request.Status);

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserListItem(u.Id, u.Email.Value, u.Username.Value, u.Status, u.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Ok(new PaginationListResponse<UserListItem>(totalCount, items));
    }
}
```

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `dotnet test tests/WardenId.Identity.Application.Tests --filter "FullyQualifiedName~Features.Users.List"`
Expected: 4 tests pass.

- [ ] **Step 5: Run the full test suite and build**

Run: `dotnet build WardenId.slnx && dotnet test WardenId.slnx`
Expected: solution builds cleanly and every test project (including `Identity.Domain.Tests` and `ClientManagement.Domain.Tests`) passes — confirms nothing in this pass broke existing modules.

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.Identity.Application/Features/Users/List \
  tests/WardenId.Identity.Application.Tests/Features/Users/List
git commit -m "feat: add Identity List query"
```

---

## Self-Review Notes

**Spec coverage:** All 12 `User` behaviors have a task (Register→Task 2, VerifyEmail→Task 3, SetPassword→Tasks 2/5/6, RecordFailedLogin/RecordSuccessfulLogin→Task 4, Unlock/Disable/SoftDelete→Task 7, EnrollMfaFactor→Task 8, VerifyMfaFactor/RevokeMfaFactor→Task 9, RequestPasswordReset/ResetPassword→Task 6, SetAttribute→Task 10, AddRequiredAction/ClearRequiredAction→Task 10) plus `Authenticate` (Task 4) and both queries (Tasks 11–12). `IUserRepository`/`IIdentityDbContext`/`IdentityPolicyOptions`/`ConfigurationKeys`/`DependencyInjection` are all in Task 1.

**Type consistency verified:** `IUserRepository` methods (Task 1) are called with matching signatures throughout Tasks 2–12. `SecureTokenGenerator.Generate()`/`.Hash()` (Task 2, made public in Task 3) are used identically in Tasks 3, 6, and 8. `PasswordPolicyFactory.Create()` (Task 2) is reused unchanged in Tasks 5 and 6. `TestUsers.CreatePendingVerification` (Task 3) is reused unchanged in Tasks 4–10. `TestIdentityDbContext` (Task 11) is reused unchanged in Task 12.

**No placeholders:** every step contains complete, compilable code — the EF Core InMemory mapping in Task 11 was executed against the real `WardenId.Identity.Domain` project during planning (not guessed) to confirm it builds and round-trips correctly through a fresh `DbContext` instance.
