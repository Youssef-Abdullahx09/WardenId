# ClientManagement CRUD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `WardenId.ClientManagement.Application`, `.Infrastructure`, `.Presentation` from scratch and wire them into the Gateway, delivering the 5 core CRUD admin endpoints (Create/List/Get/Update/Delete a `Client`) per `docs/superpowers/specs/2026-07-25-client-management-crud-design.md`.

**Architecture:** Mirror `WardenId.Tenancy.*` conventions exactly — MediatR commands/queries under `Features/Clients/<Verb>/`, `IEndpoint`-based minimal APIs, `Result`/`FluentResults` error handling, EF Core + Npgsql persistence with a per-module schema. Two novel pieces with no direct precedent in `Tenancy`: a paginated List query (first in the repo) and `OwnsMany`-mapped value-object collections (`RedirectUri`/`CorsOrigin`).

**Tech Stack:** .NET 10, MediatR 12.5.0, FluentResults 4.0.0, EF Core 10.0.10 + Npgsql.EntityFrameworkCore.PostgreSQL, Asp.Versioning.Http 10.0.0.

## Global Constraints

- Target framework `net10.0`, nullable + implicit usings enabled (inherited from root `Directory.Build.props` — never redeclare in a new `.csproj`).
- `RestorePackagesWithLockFile=true` repo-wide — every new project needs `dotnet restore` once to generate its `packages.lock.json`.
- Package versions go in `Directory.Packages.props` only; project `.csproj` files reference packages by name, no `Version=` attribute. All package versions needed here already exist in `Directory.Packages.props` (verified: `MediatR`, `FluentResults`, `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Asp.Versioning.Http`) — no edits to that file in this plan.
- Application layer: no FluentValidation anywhere in this repo — validation happens in Domain static factories/behaviors returning `Result`; Handlers just propagate `Result.Fail(...)`.
- RBAC/JWT auth is out of scope this pass — every endpoint uses bare `.RequireAuthorization()`, matching the one existing precedent (`Tenants/Create/Endpoint.cs:35`).
- Tenant resolution is route-based, not `ICurrentTenant` — every Endpoint binds `[FromRoute] Guid tenantId` explicitly and passes it into the Command/Query.
- No test project exists for any `*.Application`, `*.Infrastructure`, or `*.Presentation` layer in this repo — this plan does not introduce one, matching precedent. Verification for those layers is `dotnet build` (and, for the Infrastructure/migration task, `dotnet ef migrations add` succeeding). Only the two new `Client` domain-layer setters get real unit tests, in the existing `WardenId.ClientManagement.Domain.Tests` project.
- `Client.Id` (the aggregate's `Guid`, from `AggregateRoot`/`Entity`) is distinct from `Client.ClientId` (the `ClientId` value object, the OAuth `client_id` string). Every `{clientId}` route segment and every `ClientId`-named Command/Query parameter in this plan refers to the aggregate's `Guid Id`, never the VO — this matches how `Tenant.Create`'s handler returns `tenant.Id`, and it's what lets `GetByIdAsync`/`Update`/`Delete` look up a row by primary key.

---

## File Structure

**New — `WardenId.ClientManagement.Application`:**
- `src/WardenId.ClientManagement.Application/WardenId.ClientManagement.Application.csproj`
- `src/WardenId.ClientManagement.Application/DependencyInjection.cs`
- `src/WardenId.ClientManagement.Application/Abstractions/IClientManagementDbContext.cs`
- `src/WardenId.ClientManagement.Application/Abstractions/IClientRepository.cs`
- `src/WardenId.ClientManagement.Application/Features/Clients/Create/Command.cs`
- `src/WardenId.ClientManagement.Application/Features/Clients/Create/Handler.cs`
- `src/WardenId.ClientManagement.Application/Features/Clients/List/Query.cs`
- `src/WardenId.ClientManagement.Application/Features/Clients/List/ClientListItem.cs`
- `src/WardenId.ClientManagement.Application/Features/Clients/List/Handler.cs`
- `src/WardenId.ClientManagement.Application/Features/Clients/Get/Query.cs`
- `src/WardenId.ClientManagement.Application/Features/Clients/Get/ClientDetail.cs`
- `src/WardenId.ClientManagement.Application/Features/Clients/Get/Handler.cs`
- `src/WardenId.ClientManagement.Application/Features/Clients/Update/Command.cs`
- `src/WardenId.ClientManagement.Application/Features/Clients/Update/Handler.cs`
- `src/WardenId.ClientManagement.Application/Features/Clients/Delete/Command.cs`
- `src/WardenId.ClientManagement.Application/Features/Clients/Delete/Handler.cs`

**New — `WardenId.ClientManagement.Infrastructure`:**
- `src/WardenId.ClientManagement.Infrastructure/WardenId.ClientManagement.Infrastructure.csproj`
- `src/WardenId.ClientManagement.Infrastructure/DependencyInjection.cs`
- `src/WardenId.ClientManagement.Infrastructure/Persistence/SchemaConsts.cs`
- `src/WardenId.ClientManagement.Infrastructure/Persistence/ClientManagementDbContext.cs`
- `src/WardenId.ClientManagement.Infrastructure/Persistence/ClientManagementDbContextDesignTimeFactory.cs`
- `src/WardenId.ClientManagement.Infrastructure/Persistence/ClientRepository.cs`
- `src/WardenId.ClientManagement.Infrastructure/Persistence/Configurations/ClientConfiguration.cs`
- `src/WardenId.ClientManagement.Infrastructure/Persistence/Configurations/ClientSecretConfiguration.cs`
- `src/WardenId.ClientManagement.Infrastructure/Persistence/Configurations/ClientSigningKeyConfiguration.cs`
- `src/WardenId.ClientManagement.Infrastructure/Persistence/Migrations/*` (generated)

**New — `WardenId.ClientManagement.Presentation`:**
- `src/WardenId.ClientManagement.Presentation/WardenId.ClientManagement.Presentation.csproj`
- `src/WardenId.ClientManagement.Presentation/DependencyInjection.cs`
- `src/WardenId.ClientManagement.Presentation/Features/Clients/Create/Endpoint.cs`
- `src/WardenId.ClientManagement.Presentation/Features/Clients/Create/Request.cs`
- `src/WardenId.ClientManagement.Presentation/Features/Clients/List/Endpoint.cs`
- `src/WardenId.ClientManagement.Presentation/Features/Clients/List/Request.cs`
- `src/WardenId.ClientManagement.Presentation/Features/Clients/Get/Endpoint.cs`
- `src/WardenId.ClientManagement.Presentation/Features/Clients/Update/Endpoint.cs`
- `src/WardenId.ClientManagement.Presentation/Features/Clients/Update/Request.cs`
- `src/WardenId.ClientManagement.Presentation/Features/Clients/Delete/Endpoint.cs`

**Modify:**
- `src/WardenId.SharedKernel.Persistence/DbContextBase.cs` — soft-delete interceptor fix.
- `src/WardenId.ClientManagement.Domain/Client.cs` — add `UpdateDisplayName`, `UpdateConsentRequired`.
- `WardenId.slnx` — register the 3 new projects.
- `WardenId.Gateway/Program.cs` — add `AddClientManagementModule` call.

**Create — Gateway:**
- `WardenId.Gateway/DependencyInjection/ClientManagementDependencyInjection.cs`

---

### Task 1: Soft-delete interceptor fix

**Files:**
- Modify: `src/WardenId.SharedKernel.Persistence/DbContextBase.cs:36-63`

**Interfaces:**
- Consumes: nothing new.
- Produces: `dbContext.Remove(entity)` on any `Entity`/`AggregateRoot` now performs a soft delete (row stays, `IsDeleted`/`DeletedById`/`DeletedAt` populated, EF issues `UPDATE` not `DELETE`) instead of a physical delete. `Task 6`'s `ClientRepository.DeleteAsync` relies on this.

No automated test — no test project exists for `SharedKernel.Persistence` (see Global Constraints), and this fix has no observable effect until exercised end-to-end by `Task 6`/`Task 13`, where it's verified via `dotnet build` + the manual DB check in `Task 14`.

- [ ] **Step 1: Apply the fix**

In `src/WardenId.SharedKernel.Persistence/DbContextBase.cs`, replace the `CheckAndUpdateEntities` method body (currently lines 36-63) with:

```csharp
    private void CheckAndUpdateEntities(Guid userId)
    {
        ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.State == EntityState.Added)
            .ToList().ForEach(entry => entry.Entity.MarkAsCreated(userId));
        ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.State == EntityState.Modified)
            .ToList().ForEach(entry => entry.Entity.MarkAsModified(userId));
        ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.State == EntityState.Deleted)
            .ToList().ForEach(entry =>
            {
                entry.Entity.MarkAsDeleted(userId);
                entry.State = EntityState.Modified;
            });

        ChangeTracker
            .Entries<Entity>()
            .Where(e => e.State == EntityState.Added)
            .ToList().ForEach(entry => entry.Entity.MarkAsCreated(userId));
        ChangeTracker
            .Entries<Entity>()
            .Where(e => e.State == EntityState.Modified)
            .ToList().ForEach(entry => entry.Entity.MarkAsModified(userId));
        ChangeTracker
            .Entries<Entity>()
            .Where(e => e.State == EntityState.Deleted)
            .ToList().ForEach(entry =>
            {
                entry.Entity.MarkAsDeleted(userId);
                entry.State = EntityState.Modified;
            });
    }
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/WardenId.SharedKernel.Persistence/WardenId.SharedKernel.Persistence.csproj`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/WardenId.SharedKernel.Persistence/DbContextBase.cs
git commit -m "fix: convert EF Remove() into a soft delete instead of a physical delete"
```

---

### Task 2: `Client.UpdateDisplayName` / `Client.UpdateConsentRequired`

**Files:**
- Modify: `src/WardenId.ClientManagement.Domain/Client.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/ClientUpdateDisplayNameTests.cs`
- Test: `tests/WardenId.ClientManagement.Domain.Tests/ClientUpdateConsentRequiredTests.cs`

**Interfaces:**
- Consumes: `ClientErrors.ClientDisplayNameRequired()`, `ClientErrors.ClientDisplayNameTooLong()` (already exist in `src/WardenId.ClientManagement.Domain/ClientErrors.cs`).
- Produces: `Client.UpdateDisplayName(string displayName) -> Result`, `Client.UpdateConsentRequired(bool consentRequired) -> void`. Consumed by `Task 12`'s `Update.Handler`.

- [ ] **Step 1: Write the failing tests**

`tests/WardenId.ClientManagement.Domain.Tests/ClientUpdateDisplayNameTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class ClientUpdateDisplayNameTests
{
    private static Client CreateClient() =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Original name", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void UpdateDisplayName_FailsOnEmpty()
    {
        var client = CreateClient();

        var result = client.UpdateDisplayName("");

        Assert.True(result.IsFailed);
        Assert.Equal("Original name", client.DisplayName);
    }

    [Fact]
    public void UpdateDisplayName_FailsWhenTooLong()
    {
        var client = CreateClient();

        var result = client.UpdateDisplayName(new string('a', 201));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void UpdateDisplayName_SucceedsAndTrimsWhitespace()
    {
        var client = CreateClient();

        var result = client.UpdateDisplayName("  New name  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("New name", client.DisplayName);
    }
}
```

`tests/WardenId.ClientManagement.Domain.Tests/ClientUpdateConsentRequiredTests.cs`:

```csharp
namespace WardenId.ClientManagement.Domain.Tests;

public class ClientUpdateConsentRequiredTests
{
    private static Client CreateClient() =>
        Client.Create(
            Guid.NewGuid(), ClientType.Confidential, "Test client", GrantType.AuthorizationCode,
            ClientAuthenticationMethod.SharedSecret, isFirstParty: false, consentRequired: true).Value;

    [Fact]
    public void UpdateConsentRequired_SetsTheFlag()
    {
        var client = CreateClient();

        client.UpdateConsentRequired(false);

        Assert.False(client.ConsentRequired);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter "ClientUpdateDisplayNameTests|ClientUpdateConsentRequiredTests" -v n`
Expected: FAIL — build error, `Client.UpdateDisplayName`/`Client.UpdateConsentRequired` do not exist.

- [ ] **Step 3: Implement the two methods**

In `src/WardenId.ClientManagement.Domain/Client.cs`, add after `UpdateTokenLifetimeOverrides`:

```csharp
    public Result UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return Result.Fail(ClientErrors.ClientDisplayNameRequired());

        var trimmed = displayName.Trim();

        if (trimmed.Length > 200) return Result.Fail(ClientErrors.ClientDisplayNameTooLong());

        DisplayName = trimmed;
        return Result.Ok();
    }

    public void UpdateConsentRequired(bool consentRequired) => ConsentRequired = consentRequired;
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests --filter "ClientUpdateDisplayNameTests|ClientUpdateConsentRequiredTests" -v n`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.ClientManagement.Domain/Client.cs tests/WardenId.ClientManagement.Domain.Tests/ClientUpdateDisplayNameTests.cs tests/WardenId.ClientManagement.Domain.Tests/ClientUpdateConsentRequiredTests.cs
git commit -m "feat: add Client.UpdateDisplayName/UpdateConsentRequired behaviors"
```

---

### Task 3: Scaffold `WardenId.ClientManagement.Application`

**Files:**
- Create: `src/WardenId.ClientManagement.Application/WardenId.ClientManagement.Application.csproj`
- Create: `src/WardenId.ClientManagement.Application/DependencyInjection.cs`
- Create: `src/WardenId.ClientManagement.Application/Abstractions/IClientManagementDbContext.cs`
- Create: `src/WardenId.ClientManagement.Application/Abstractions/IClientRepository.cs`
- Modify: `WardenId.slnx`

**Interfaces:**
- Consumes: `WardenId.SharedKernel.Application.Abstractions.IDbContext` (`SaveChangesAsync(Guid, CancellationToken)`, `BeginTransactionAsync`/`CommitTransactionAsync`/`RollbackTransactionAsync`), `WardenId.ClientManagement.Domain.Client`.
- Produces: `IClientManagementDbContext { DbSet<Client> Clients { get; } }` and `IClientRepository` — every later Application task (4 CQRS features onward) depends on one or both of these.

- [ ] **Step 1: Create the project file**

`src/WardenId.ClientManagement.Application/WardenId.ClientManagement.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
      <ProjectReference Include="..\WardenId.SharedKernel.Application\WardenId.SharedKernel.Application.csproj" />
      <ProjectReference Include="..\WardenId.ClientManagement.Domain\WardenId.ClientManagement.Domain.csproj" />
    </ItemGroup>

    <ItemGroup>
      <PackageReference Include="MediatR" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: Create the DbContext abstraction**

`src/WardenId.ClientManagement.Application/Abstractions/IClientManagementDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Domain;
using WardenId.SharedKernel.Application.Abstractions;

namespace WardenId.ClientManagement.Application.Abstractions;

public interface IClientManagementDbContext : IDbContext
{
    DbSet<Client> Clients { get; }
}
```

- [ ] **Step 3: Create the repository abstraction**

`src/WardenId.ClientManagement.Application/Abstractions/IClientRepository.cs`:

```csharp
using FluentResults;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Abstractions;

public interface IClientRepository
{
    Task CreateAsync(
        Client client,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<Client>> GetByIdAsync(
        Guid tenantId,
        Guid clientId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Client client,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Client client,
        Guid userId,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Create the DI entry point**

`src/WardenId.ClientManagement.Application/DependencyInjection.cs`:

```csharp
using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace WardenId.ClientManagement.Application;

public static class DependencyInjection
{
    public static void AddClientManagementApplication(
        this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly()));
    }
}
```

- [ ] **Step 5: Register the project in `WardenId.slnx`**

In `WardenId.slnx`, replace:

```xml
  <Folder Name="/src/WardenId.ClientManagement/">
    <Project Path="src/WardenId.ClientManagement.Domain/WardenId.ClientManagement.Domain.csproj" />
  </Folder>
```

With:

```xml
  <Folder Name="/src/WardenId.ClientManagement/">
    <Project Path="src/WardenId.ClientManagement.Application/WardenId.ClientManagement.Application.csproj" />
    <Project Path="src/WardenId.ClientManagement.Domain/WardenId.ClientManagement.Domain.csproj" />
  </Folder>
```

- [ ] **Step 6: Restore and build to verify**

Run: `dotnet restore src/WardenId.ClientManagement.Application/WardenId.ClientManagement.Application.csproj`
Run: `dotnet build src/WardenId.ClientManagement.Application/WardenId.ClientManagement.Application.csproj`
Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add src/WardenId.ClientManagement.Application WardenId.slnx
git commit -m "feat: scaffold WardenId.ClientManagement.Application"
```

---

### Task 4: Scaffold `WardenId.ClientManagement.Infrastructure` (DbContext shell)

**Files:**
- Create: `src/WardenId.ClientManagement.Infrastructure/WardenId.ClientManagement.Infrastructure.csproj`
- Create: `src/WardenId.ClientManagement.Infrastructure/Persistence/SchemaConsts.cs`
- Create: `src/WardenId.ClientManagement.Infrastructure/Persistence/ClientManagementDbContext.cs`
- Create: `src/WardenId.ClientManagement.Infrastructure/Persistence/ClientManagementDbContextDesignTimeFactory.cs`
- Modify: `WardenId.slnx`

**Interfaces:**
- Consumes: `IClientManagementDbContext` (Task 3), `WardenId.SharedKernel.Persistence.DbContextBase`, `AppConfigurationConstants` (`WardenId.SharedKernel.Persistence`).
- Produces: `ClientManagementDbContext` (no entity configurations yet — added in Task 5) — `Task 5` adds configs to the same `OnModelCreating`, `Task 6` adds the repository, `Task 8` wires DI.

- [ ] **Step 1: Create the project file**

`src/WardenId.ClientManagement.Infrastructure/WardenId.ClientManagement.Infrastructure.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <ItemGroup>
      <ProjectReference Include="..\WardenId.SharedKernel.Persistence\WardenId.SharedKernel.Persistence.csproj" />
      <ProjectReference Include="..\WardenId.ClientManagement.Application\WardenId.ClientManagement.Application.csproj" />
    </ItemGroup>

    <ItemGroup>
      <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
      <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: Create `SchemaConsts`**

`src/WardenId.ClientManagement.Infrastructure/Persistence/SchemaConsts.cs`:

```csharp
namespace WardenId.ClientManagement.Infrastructure.Persistence;

public static class SchemaConsts
{
    public const string ClientsTable = "Clients";
    public const string ClientSecretsTable = "ClientSecrets";
    public const string ClientSigningKeysTable = "ClientSigningKeys";
    public const string ClientRedirectUrisTable = "ClientRedirectUris";
    public const string ClientPostLogoutRedirectUrisTable = "ClientPostLogoutRedirectUris";
    public const string ClientCorsOriginsTable = "ClientCorsOrigins";
}
```

- [ ] **Step 3: Create `ClientManagementDbContext`**

`src/WardenId.ClientManagement.Infrastructure/Persistence/ClientManagementDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;
using WardenId.SharedKernel.Persistence;

namespace WardenId.ClientManagement.Infrastructure.Persistence;

public sealed class ClientManagementDbContext(
    DbContextOptions<ClientManagementDbContext> options) : DbContextBase(options), IClientManagementDbContext
{
    public const string Schema = "ClientManagement";

    public DbSet<Client> Clients => Set<Client>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClientManagementDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Step 4: Create the design-time factory**

`src/WardenId.ClientManagement.Infrastructure/Persistence/ClientManagementDbContextDesignTimeFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using WardenId.SharedKernel.Persistence;

namespace WardenId.ClientManagement.Infrastructure.Persistence;

// Used by `dotnet ef` tooling only.
internal sealed class ClientManagementDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ClientManagementDbContext>
{
    public ClientManagementDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), AppConfigurationConstants.PathToGateway))
            .AddJsonFile(AppConfigurationConstants.AppSettingFileName)
            .Build();

        var connectionString = configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey)
            ?? throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage);

        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ClientManagementDbContext(options);
    }
}
```

- [ ] **Step 5: Register the project in `WardenId.slnx`**

Replace the `/src/WardenId.ClientManagement/` folder block again:

```xml
  <Folder Name="/src/WardenId.ClientManagement/">
    <Project Path="src/WardenId.ClientManagement.Application/WardenId.ClientManagement.Application.csproj" />
    <Project Path="src/WardenId.ClientManagement.Domain/WardenId.ClientManagement.Domain.csproj" />
    <Project Path="src/WardenId.ClientManagement.Infrastructure/WardenId.ClientManagement.Infrastructure.csproj" />
  </Folder>
```

- [ ] **Step 6: Restore and build to verify**

Run: `dotnet restore src/WardenId.ClientManagement.Infrastructure/WardenId.ClientManagement.Infrastructure.csproj`
Run: `dotnet build src/WardenId.ClientManagement.Infrastructure/WardenId.ClientManagement.Infrastructure.csproj`
Expected: `Build succeeded.` (The model has no entity configurations yet, but EF doesn't validate the model at build time — only when first used — so this compiles fine.)

- [ ] **Step 7: Commit**

```bash
git add src/WardenId.ClientManagement.Infrastructure WardenId.slnx
git commit -m "feat: scaffold WardenId.ClientManagement.Infrastructure DbContext shell"
```

---

### Task 5: EF configurations + first migration

**Files:**
- Create: `src/WardenId.ClientManagement.Infrastructure/Persistence/Configurations/ClientConfiguration.cs`
- Create: `src/WardenId.ClientManagement.Infrastructure/Persistence/Configurations/ClientSecretConfiguration.cs`
- Create: `src/WardenId.ClientManagement.Infrastructure/Persistence/Configurations/ClientSigningKeyConfiguration.cs`
- Create: `src/WardenId.ClientManagement.Infrastructure/Persistence/Migrations/*` (generated by `dotnet ef`)

**Interfaces:**
- Consumes: `Client`, `ClientId`, `RedirectUri`, `CorsOrigin`, `TokenLifetimeOverrides`, `ClientSecret`, `ClientSigningKey`, `GrantType`, `ClientType`, `ClientAuthenticationMethod` (all in `WardenId.ClientManagement.Domain`), `SchemaConsts` (Task 4).
- Produces: a buildable EF model for `ClientManagementDbContext` and the first migration. `Task 6`'s `ClientRepository` and every List/Get query in Tasks 9-13 depend on this model existing.

- [ ] **Step 1: Write `ClientConfiguration`**

`src/WardenId.ClientManagement.Infrastructure/Persistence/Configurations/ClientConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Infrastructure.Persistence.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable(SchemaConsts.ClientsTable);

        builder.Property(c => c.TenantId).IsRequired();
        builder.HasIndex(c => c.TenantId);

        builder.Property(c => c.ClientId)
            .HasConversion(
                clientId => clientId.Value,
                value => ClientId.FromTrustedSource(value))
            .HasMaxLength(ClientId.Length)
            .IsRequired();
        builder.HasIndex(c => new { c.TenantId, c.ClientId }).IsUnique();

        builder.Property(c => c.ClientType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.ClientAuthenticationMethod)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.GrantType)
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.Property(c => c.Enabled).IsRequired();
        builder.Property(c => c.IsFirstParty).IsRequired();
        builder.Property(c => c.RequirePkce).IsRequired();
        builder.Property(c => c.ConsentRequired).IsRequired();

        builder.OwnsOne(c => c.TokenLifetimeOverrides, o =>
        {
            o.Property(t => t.AccessTokenSeconds).HasColumnName("AccessTokenSeconds");
            o.Property(t => t.IdTokenSeconds).HasColumnName("IdTokenSeconds");
            o.Property(t => t.RefreshTokenSeconds).HasColumnName("RefreshTokenSeconds");
        });

        builder.OwnsMany(c => c.RedirectUris, b =>
        {
            b.ToTable(SchemaConsts.ClientRedirectUrisTable);
            b.WithOwner().HasForeignKey("ClientId");
            b.Property(r => r.Value).HasColumnName("Value").HasMaxLength(2000).IsRequired();
            b.HasKey("ClientId", "Value");
        });
        builder.Navigation(c => c.RedirectUris).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(c => c.PostLogoutRedirectUris, b =>
        {
            b.ToTable(SchemaConsts.ClientPostLogoutRedirectUrisTable);
            b.WithOwner().HasForeignKey("ClientId");
            b.Property(r => r.Value).HasColumnName("Value").HasMaxLength(2000).IsRequired();
            b.HasKey("ClientId", "Value");
        });
        builder.Navigation(c => c.PostLogoutRedirectUris).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(c => c.CorsOrigins, b =>
        {
            b.ToTable(SchemaConsts.ClientCorsOriginsTable);
            b.WithOwner().HasForeignKey("ClientId");
            b.Property(o => o.Value).HasColumnName("Value").HasMaxLength(255).IsRequired();
            b.HasKey("ClientId", "Value");
        });
        builder.Navigation(c => c.CorsOrigins).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(c => c.AssignedScopeIds)
            .HasColumnName("AssignedScopeIds");

        builder.HasMany(c => c.Secrets)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Secrets).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => c.SigningKeys)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.SigningKeys).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.DomainEvents);
    }
}
```

`AssignedScopeIds` is `IReadOnlyCollection<Guid>` backed by `List<Guid>` — EF Core 10 maps a `List<Guid>` property to a Postgres `uuid[]` column natively via Npgsql, no converter needed; keeping it a plain `Property(...)` call (not `OwnsMany`) is correct since these are bare scalars, not owned entities.

- [ ] **Step 2: Write `ClientSecretConfiguration`**

`src/WardenId.ClientManagement.Infrastructure/Persistence/Configurations/ClientSecretConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Infrastructure.Persistence.Configurations;

public sealed class ClientSecretConfiguration : IEntityTypeConfiguration<ClientSecret>
{
    public void Configure(EntityTypeBuilder<ClientSecret> builder)
    {
        builder.ToTable(SchemaConsts.ClientSecretsTable);

        builder.Property(s => s.HashedValue).IsRequired();
        builder.Property(s => s.Label).HasMaxLength(200).IsRequired();
        builder.Property(s => s.ExpiresAt);

        builder.HasIndex("ClientId").HasDatabaseName("IX_ClientSecrets_ClientId");
    }
}
```

- [ ] **Step 3: Write `ClientSigningKeyConfiguration`**

`src/WardenId.ClientManagement.Infrastructure/Persistence/Configurations/ClientSigningKeyConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Infrastructure.Persistence.Configurations;

public sealed class ClientSigningKeyConfiguration : IEntityTypeConfiguration<ClientSigningKey>
{
    public void Configure(EntityTypeBuilder<ClientSigningKey> builder)
    {
        builder.ToTable(SchemaConsts.ClientSigningKeysTable);

        builder.Property(k => k.Kid).HasMaxLength(100).IsRequired();
        builder.Property(k => k.PublicKeyJwk).HasColumnType("jsonb").IsRequired();
        builder.Property(k => k.Algorithm).HasMaxLength(20).IsRequired();
        builder.Property(k => k.ExpiresAt);

        builder.HasIndex("ClientId").HasDatabaseName("IX_ClientSigningKeys_ClientId");
        builder.HasIndex("Kid").IsUnique().HasDatabaseName("IX_ClientSigningKeys_Kid_Unique");
    }
}
```

- [ ] **Step 4: Generate the migration**

Run:
```bash
dotnet ef migrations add AddClientManagementSchema \
  --project src/WardenId.ClientManagement.Infrastructure \
  --startup-project WardenId.Gateway \
  --context ClientManagementDbContext
```
Expected: `Done.` and a new `src/WardenId.ClientManagement.Infrastructure/Persistence/Migrations/<timestamp>_AddClientManagementSchema.cs` + `.Designer.cs` + updated `ClientManagementDbContextModelSnapshot.cs` appear. If it fails with a constructor-binding error on `RedirectUri`/`CorsOrigin`, add a private parameterless constructor to each (mirroring `ClientSecret`'s `// EF Core materialization only` pattern) and retry — do not change any public API.

- [ ] **Step 5: Build to verify**

Run: `dotnet build src/WardenId.ClientManagement.Infrastructure/WardenId.ClientManagement.Infrastructure.csproj`
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.ClientManagement.Infrastructure/Persistence/Configurations src/WardenId.ClientManagement.Infrastructure/Persistence/Migrations
git commit -m "feat: add EF configurations and initial migration for ClientManagement schema"
```

---

### Task 6: `ClientRepository` + Infrastructure DI

**Files:**
- Create: `src/WardenId.ClientManagement.Infrastructure/Persistence/ClientRepository.cs`
- Create: `src/WardenId.ClientManagement.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `IClientRepository` (Task 3), `ClientManagementDbContext` (Task 4/5), `DomainEventsInterceptor` (`WardenId.SharedKernel.Persistence`, already registered by `AddSharedKernelModule`).
- Produces: `AddClientManagementInfrastructure(IServiceCollection, IConfiguration)` — consumed by `Task 8`'s Gateway wiring.

- [ ] **Step 1: Implement `ClientRepository`**

`src/WardenId.ClientManagement.Infrastructure/Persistence/ClientRepository.cs`:

```csharp
using FluentResults;
using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Infrastructure.Persistence;

public sealed class ClientRepository(
    IClientManagementDbContext dbContext) : IClientRepository
{
    public async Task CreateAsync(
        Client client,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Clients.AddAsync(client, cancellationToken);
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }

    public async Task<Result<Client>> GetByIdAsync(
        Guid tenantId,
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        var client = await dbContext.Clients
            .Include(c => c.Secrets)
            .Include(c => c.SigningKeys)
            .Where(c => c.Id == clientId && c.TenantId == tenantId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        return client is null ? Result.Fail(ClientErrors.NotFound) : client;
    }

    public async Task UpdateAsync(
        Client client,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }

    public async Task DeleteAsync(
        Client client,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        dbContext.Clients.Remove(client);
        await dbContext.SaveChangesAsync(userId, cancellationToken);
    }
}
```

`UpdateAsync` just calls `SaveChangesAsync` — `client` is already tracked from the `GetByIdAsync` that loaded it earlier in the same request/handler, so EF's change tracker already sees the mutations made via the aggregate's behaviors; no explicit `Update()` call is needed (standard EF Core tracked-entity semantics, same assumption `TenantRepository` doesn't need to make since Tenancy has no update feature yet).

- [ ] **Step 2: Implement Infrastructure DI**

`src/WardenId.ClientManagement.Infrastructure/DependencyInjection.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Infrastructure.Persistence;
using WardenId.SharedKernel.Persistence;

namespace WardenId.ClientManagement.Infrastructure;

public static class DependencyInjection
{
    public static void AddClientManagementInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<IClientManagementDbContext, ClientManagementDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey) ??
                throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage),
                postgresSql =>
                    postgresSql.MigrationsHistoryTable("__EFMigrationsHistory", ClientManagementDbContext.Schema));
            options.AddInterceptors(serviceProvider.GetRequiredService<DomainEventsInterceptor>());
        });

        services.AddScoped<IClientRepository, ClientRepository>();
        MigrateAsync(services).GetAwaiter().GetResult();
    }

    private static async Task MigrateAsync(IServiceCollection services)
    {
        var context = services.BuildServiceProvider().GetRequiredService<ClientManagementDbContext>();
        await context.Database.MigrateAsync(CancellationToken.None);
    }
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/WardenId.ClientManagement.Infrastructure/WardenId.ClientManagement.Infrastructure.csproj`
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/WardenId.ClientManagement.Infrastructure/Persistence/ClientRepository.cs src/WardenId.ClientManagement.Infrastructure/DependencyInjection.cs
git commit -m "feat: add ClientRepository and ClientManagement.Infrastructure DI"
```

---

### Task 7: Scaffold `WardenId.ClientManagement.Presentation`

**Files:**
- Create: `src/WardenId.ClientManagement.Presentation/WardenId.ClientManagement.Presentation.csproj`
- Create: `src/WardenId.ClientManagement.Presentation/DependencyInjection.cs`
- Modify: `WardenId.slnx`

**Interfaces:**
- Consumes: `AddClientManagementApplication()` (Task 3).
- Produces: `AddClientManagementPresentation()` — consumed by `Task 8`.

- [ ] **Step 1: Create the project file**

`src/WardenId.ClientManagement.Presentation/WardenId.ClientManagement.Presentation.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Asp.Versioning.Http" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\WardenId.SharedKernel.Presentation\WardenId.SharedKernel.Presentation.csproj" />
    <ProjectReference Include="..\WardenId.ClientManagement.Application\WardenId.ClientManagement.Application.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create the DI entry point**

`src/WardenId.ClientManagement.Presentation/DependencyInjection.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using WardenId.ClientManagement.Application;

namespace WardenId.ClientManagement.Presentation;

public static class DependencyInjection
{
    public static void AddClientManagementPresentation(
        this IServiceCollection services)
    {
        services.AddClientManagementApplication();
    }
}
```

- [ ] **Step 3: Register the project in `WardenId.slnx`**

```xml
  <Folder Name="/src/WardenId.ClientManagement/">
    <Project Path="src/WardenId.ClientManagement.Application/WardenId.ClientManagement.Application.csproj" />
    <Project Path="src/WardenId.ClientManagement.Domain/WardenId.ClientManagement.Domain.csproj" />
    <Project Path="src/WardenId.ClientManagement.Infrastructure/WardenId.ClientManagement.Infrastructure.csproj" />
    <Project Path="src/WardenId.ClientManagement.Presentation/WardenId.ClientManagement.Presentation.csproj" />
  </Folder>
```

- [ ] **Step 4: Restore and build to verify**

Run: `dotnet restore src/WardenId.ClientManagement.Presentation/WardenId.ClientManagement.Presentation.csproj`
Run: `dotnet build src/WardenId.ClientManagement.Presentation/WardenId.ClientManagement.Presentation.csproj`
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.ClientManagement.Presentation WardenId.slnx
git commit -m "feat: scaffold WardenId.ClientManagement.Presentation"
```

---

### Task 8: Gateway wiring

**Files:**
- Create: `WardenId.Gateway/DependencyInjection/ClientManagementDependencyInjection.cs`
- Modify: `WardenId.Gateway/Program.cs`
- Modify: `WardenId.Gateway/WardenId.Gateway.csproj` (project references, if not already present)

**Interfaces:**
- Consumes: `AddClientManagementPresentation()` (Task 7), `AddClientManagementInfrastructure(IConfiguration)` (Task 6).
- Produces: `AddClientManagementModule(IConfiguration)` — the full module is now live in the running Gateway; every Endpoint added in Tasks 9-13 becomes reachable once this task lands.

- [ ] **Step 1: Check Gateway project references**

Run: `grep -c ClientManagement WardenId.Gateway/WardenId.Gateway.csproj`
If it prints `0`, add references to `WardenId.Gateway/WardenId.Gateway.csproj`'s existing `<ItemGroup>` of `ProjectReference`s (alongside the existing Tenancy ones):

```xml
    <ProjectReference Include="..\src\WardenId.ClientManagement.Infrastructure\WardenId.ClientManagement.Infrastructure.csproj" />
    <ProjectReference Include="..\src\WardenId.ClientManagement.Presentation\WardenId.ClientManagement.Presentation.csproj" />
```

- [ ] **Step 2: Create `ClientManagementDependencyInjection`**

`WardenId.Gateway/DependencyInjection/ClientManagementDependencyInjection.cs`:

```csharp
using WardenId.ClientManagement.Infrastructure;
using WardenId.ClientManagement.Presentation;

namespace WardenId.Gateway.DependencyInjection;

public static class ClientManagementDependencyInjection
{
    public static void AddClientManagementModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddClientManagementPresentation();
        services.AddClientManagementInfrastructure(configuration);
    }
}
```

- [ ] **Step 3: Wire it into `Program.cs`**

In `WardenId.Gateway/Program.cs`, change:

```csharp
builder.Services.AddSharedKernelModule(builder.Configuration);
builder.Services.AddTenancyModule(builder.Configuration);
builder.Services.AddGateway(builder.Configuration);
```

To:

```csharp
builder.Services.AddSharedKernelModule(builder.Configuration);
builder.Services.AddTenancyModule(builder.Configuration);
builder.Services.AddClientManagementModule(builder.Configuration);
builder.Services.AddGateway(builder.Configuration);
```

- [ ] **Step 4: Build the whole solution to verify**

Run: `dotnet build WardenId.slnx`
Expected: `Build succeeded.` (0 endpoints from this module exist yet, but the module is fully wired and the DbContext's `MigrateAsync` will run at startup once a DB is reachable.)

- [ ] **Step 5: Commit**

```bash
git add WardenId.Gateway/DependencyInjection/ClientManagementDependencyInjection.cs WardenId.Gateway/Program.cs WardenId.Gateway/WardenId.Gateway.csproj
git commit -m "feat: wire ClientManagement module into the Gateway"
```

---

### Task 9: Create feature (`POST /`)

**Files:**
- Create: `src/WardenId.ClientManagement.Application/Features/Clients/Create/Command.cs`
- Create: `src/WardenId.ClientManagement.Application/Features/Clients/Create/Handler.cs`
- Create: `src/WardenId.ClientManagement.Presentation/Features/Clients/Create/Request.cs`
- Create: `src/WardenId.ClientManagement.Presentation/Features/Clients/Create/Endpoint.cs`

**Interfaces:**
- Consumes: `Client.Create(...)` (Domain), `IClientRepository.CreateAsync` (Task 6), `ICurrentUser.UserId` (`WardenId.SharedKernel.Presentation`), `IEndpoint` (`WardenId.SharedKernel.Presentation`), `HandleApiFailure()` (`WardenId.SharedKernel.Presentation`).
- Produces: `POST /api/v{version:apiVersion}/admin/tenants/{tenantId}/clients` — no later task depends on this feature's types directly.

- [ ] **Step 1: Write the `Command`**

`src/WardenId.ClientManagement.Application/Features/Clients/Create/Command.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.Create;

public sealed record Command(
    Guid TenantId,
    ClientType ClientType,
    string DisplayName,
    GrantType GrantType,
    ClientAuthenticationMethod ClientAuthenticationMethod,
    bool IsFirstParty,
    bool ConsentRequired,
    string? Description) : IRequest<Result<Guid>>;
```

- [ ] **Step 2: Write the `Handler`**

`src/WardenId.ClientManagement.Application/Features/Clients/Create/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.Create;

public sealed class Handler(
    IClientRepository repository) : IRequestHandler<Command, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        var clientResult = Client.Create(
            request.TenantId,
            request.ClientType,
            request.DisplayName,
            request.GrantType,
            request.ClientAuthenticationMethod,
            request.IsFirstParty,
            request.ConsentRequired,
            request.Description);

        if (clientResult.IsFailed) return Result.Fail(clientResult.Errors);

        var client = clientResult.Value;

        await repository.CreateAsync(client, request.TenantId, cancellationToken);

        return Result.Ok(client.Id);
    }
}
```

`CreateAsync`'s `userId` parameter is passed `request.TenantId` here — there's no authenticated-user identity available in this pass (no JWT auth middleware, per Global Constraints), so every write in this module attributes audit fields to the tenant being administered rather than a real operator identity. This is a known, accepted gap until real auth lands, not a bug — flagging it in code isn't needed since the constraint is already documented in the spec/plan header.

- [ ] **Step 3: Write the `Request`**

`src/WardenId.ClientManagement.Presentation/Features/Clients/Create/Request.cs`:

```csharp
using WardenId.ClientManagement.Application.Features.Clients.Create;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Create;

public sealed record Request(
    ClientType ClientType,
    string DisplayName,
    GrantType GrantType,
    ClientAuthenticationMethod ClientAuthenticationMethod,
    bool IsFirstParty,
    bool ConsentRequired,
    string? Description)
{
    public Command ToCommand(Guid tenantId) => new(
        tenantId, ClientType, DisplayName, GrantType, ClientAuthenticationMethod,
        IsFirstParty, ConsentRequired, Description);
}
```

- [ ] **Step 4: Write the `Endpoint`**

`src/WardenId.ClientManagement.Presentation/Features/Clients/Create/Endpoint.cs`:

```csharp
using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Create;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Clients")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/clients", async (
                [FromRoute] Guid tenantId,
                [FromBody] Request request,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToCommand(tenantId), cancellationToken);

                return result.IsSuccess
                    ? Results.Created($"/api/v1/admin/tenants/{tenantId}/clients/{result.Value}", new { id = result.Value })
                    : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .RequireAuthorization()
            .WithTags("ClientManagement - Clients")
            .WithSummary("Create a Client")
            .WithDescription("Creates an OAuth client for the given tenant.")
            .Produces(StatusCodes.Status201Created, typeof(Guid))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
```

- [ ] **Step 5: Build to verify**

Run: `dotnet build WardenId.slnx`
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.ClientManagement.Application/Features/Clients/Create src/WardenId.ClientManagement.Presentation/Features/Clients/Create
git commit -m "feat: add Create client endpoint"
```

---

### Task 10: List feature (`GET /`)

**Files:**
- Create: `src/WardenId.ClientManagement.Application/Features/Clients/List/Query.cs`
- Create: `src/WardenId.ClientManagement.Application/Features/Clients/List/ClientListItem.cs`
- Create: `src/WardenId.ClientManagement.Application/Features/Clients/List/Handler.cs`
- Create: `src/WardenId.ClientManagement.Presentation/Features/Clients/List/Request.cs`
- Create: `src/WardenId.ClientManagement.Presentation/Features/Clients/List/Endpoint.cs`

**Interfaces:**
- Consumes: `IClientManagementDbContext.Clients` (Task 3), `PaginationListResponse<T>` (`WardenId.SharedKernel.Query.Contracts`, existing).
- Produces: `GET /api/v{version:apiVersion}/admin/tenants/{tenantId}/clients` — first paginated-List feature in the repo; no later task in this plan depends on its types.

- [ ] **Step 1: Write `ClientListItem`**

`src/WardenId.ClientManagement.Application/Features/Clients/List/ClientListItem.cs`:

```csharp
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.List;

public sealed record ClientListItem(
    Guid Id,
    string ClientId,
    string DisplayName,
    ClientType ClientType,
    bool Enabled,
    bool IsFirstParty,
    DateTimeOffset CreatedAt);
```

- [ ] **Step 2: Write the `Query`**

`src/WardenId.ClientManagement.Application/Features/Clients/List/Query.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.ClientManagement.Domain;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.ClientManagement.Application.Features.Clients.List;

public sealed record Query(
    Guid TenantId,
    int PageNumber,
    int PageSize,
    bool? Enabled,
    ClientType? ClientType) : IRequest<Result<PaginationListResponse<ClientListItem>>>;
```

- [ ] **Step 3: Write the `Handler`**

`src/WardenId.ClientManagement.Application/Features/Clients/List/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.ClientManagement.Application.Features.Clients.List;

public sealed class Handler(
    IClientManagementDbContext dbContext) : IRequestHandler<Query, Result<PaginationListResponse<ClientListItem>>>
{
    public async Task<Result<PaginationListResponse<ClientListItem>>> Handle(
        Query request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Clients
            .Where(c => c.TenantId == request.TenantId && !c.IsDeleted);

        if (request.Enabled is not null)
            query = query.Where(c => c.Enabled == request.Enabled);

        if (request.ClientType is not null)
            query = query.Where(c => c.ClientType == request.ClientType);

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new ClientListItem(
                c.Id, c.ClientId.Value, c.DisplayName, c.ClientType, c.Enabled, c.IsFirstParty, c.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Ok(new PaginationListResponse<ClientListItem>(totalCount, items));
    }
}
```

- [ ] **Step 4: Write the `Request`**

`src/WardenId.ClientManagement.Presentation/Features/Clients/List/Request.cs`:

```csharp
using WardenId.ClientManagement.Application.Features.Clients.List;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Presentation.Features.Clients.List;

public sealed record Request(
    int PageNumber,
    int PageSize,
    bool? Enabled,
    ClientType? ClientType)
{
    public Query ToQuery(Guid tenantId) => new(tenantId, PageNumber, PageSize, Enabled, ClientType);
}
```

- [ ] **Step 5: Write the `Endpoint`**

`src/WardenId.ClientManagement.Presentation/Features/Clients/List/Endpoint.cs`:

```csharp
using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.ClientManagement.Domain;
using WardenId.SharedKernel.Presentation;

namespace WardenId.ClientManagement.Presentation.Features.Clients.List;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Clients")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/clients", async (
                [FromRoute] Guid tenantId,
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                [FromQuery] bool? enabled,
                [FromQuery] ClientType? clientType,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var request = new Request(pageNumber, pageSize, enabled, clientType);
                var result = await sender.Send(request.ToQuery(tenantId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .RequireAuthorization()
            .WithTags("ClientManagement - Clients")
            .WithSummary("List Clients")
            .WithDescription("Lists clients for the given tenant, paginated and optionally filtered by enabled/clientType.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
```

- [ ] **Step 6: Build to verify**

Run: `dotnet build WardenId.slnx`
Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add src/WardenId.ClientManagement.Application/Features/Clients/List src/WardenId.ClientManagement.Presentation/Features/Clients/List
git commit -m "feat: add List clients endpoint"
```

---

### Task 11: Get feature (`GET /{clientId}`)

**Files:**
- Create: `src/WardenId.ClientManagement.Application/Features/Clients/Get/Query.cs`
- Create: `src/WardenId.ClientManagement.Application/Features/Clients/Get/ClientDetail.cs`
- Create: `src/WardenId.ClientManagement.Application/Features/Clients/Get/Handler.cs`
- Create: `src/WardenId.ClientManagement.Presentation/Features/Clients/Get/Endpoint.cs`

**Interfaces:**
- Consumes: `IClientManagementDbContext.Clients` (Task 3), `ClientErrors.NotFound` (Domain).
- Produces: `GET /api/v{version:apiVersion}/admin/tenants/{tenantId}/clients/{clientId}` — no later task depends on this feature's types.

- [ ] **Step 1: Write `ClientDetail`**

`src/WardenId.ClientManagement.Application/Features/Clients/Get/ClientDetail.cs`:

```csharp
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.Get;

public sealed record ClientSecretSummary(Guid Id, string Label, DateTimeOffset? ExpiresAt);

public sealed record ClientSigningKeySummary(Guid Id, string Kid, string Algorithm, DateTimeOffset? ExpiresAt);

public sealed record ClientDetail(
    Guid Id,
    string ClientId,
    Guid TenantId,
    ClientType ClientType,
    string DisplayName,
    string? Description,
    bool Enabled,
    bool IsFirstParty,
    GrantType GrantType,
    bool RequirePkce,
    ClientAuthenticationMethod ClientAuthenticationMethod,
    bool ConsentRequired,
    int? AccessTokenSeconds,
    int? IdTokenSeconds,
    int? RefreshTokenSeconds,
    string[] RedirectUris,
    string[] PostLogoutRedirectUris,
    string[] CorsOrigins,
    ClientSecretSummary[] Secrets,
    ClientSigningKeySummary[] SigningKeys,
    Guid[] AssignedScopeIds);
```

- [ ] **Step 2: Write the `Query`**

`src/WardenId.ClientManagement.Application/Features/Clients/Get/Query.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Application.Features.Clients.Get;

public sealed record Query(
    Guid TenantId,
    Guid ClientId) : IRequest<Result<ClientDetail>>;
```

- [ ] **Step 3: Write the `Handler`**

`src/WardenId.ClientManagement.Application/Features/Clients/Get/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.Get;

public sealed class Handler(
    IClientManagementDbContext dbContext) : IRequestHandler<Query, Result<ClientDetail>>
{
    public async Task<Result<ClientDetail>> Handle(
        Query request,
        CancellationToken cancellationToken)
    {
        var client = await dbContext.Clients
            .Include(c => c.Secrets)
            .Include(c => c.SigningKeys)
            .Where(c => c.Id == request.ClientId && c.TenantId == request.TenantId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (client is null) return Result.Fail(ClientErrors.NotFound);

        var detail = new ClientDetail(
            client.Id,
            client.ClientId.Value,
            client.TenantId,
            client.ClientType,
            client.DisplayName,
            client.Description,
            client.Enabled,
            client.IsFirstParty,
            client.GrantType,
            client.RequirePkce,
            client.ClientAuthenticationMethod,
            client.ConsentRequired,
            client.TokenLifetimeOverrides?.AccessTokenSeconds,
            client.TokenLifetimeOverrides?.IdTokenSeconds,
            client.TokenLifetimeOverrides?.RefreshTokenSeconds,
            client.RedirectUris.Select(r => r.Value).ToArray(),
            client.PostLogoutRedirectUris.Select(r => r.Value).ToArray(),
            client.CorsOrigins.Select(o => o.Value).ToArray(),
            client.Secrets.Select(s => new ClientSecretSummary(s.Id, s.Label, s.ExpiresAt)).ToArray(),
            client.SigningKeys.Select(k => new ClientSigningKeySummary(k.Id, k.Kid, k.Algorithm, k.ExpiresAt)).ToArray(),
            client.AssignedScopeIds.ToArray());

        return Result.Ok(detail);
    }
}
```

- [ ] **Step 4: Write the `Endpoint`**

`src/WardenId.ClientManagement.Presentation/Features/Clients/Get/Endpoint.cs`:

```csharp
using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.ClientManagement.Application.Features.Clients.Get;
using WardenId.SharedKernel.Presentation;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Get;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Clients")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/clients/{clientId:guid}", async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid clientId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Query(tenantId, clientId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .RequireAuthorization()
            .WithTags("ClientManagement - Clients")
            .WithSummary("Get a Client")
            .WithDescription("Returns full client detail, excluding secret hashed values.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
```

- [ ] **Step 5: Build to verify**

Run: `dotnet build WardenId.slnx`
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.ClientManagement.Application/Features/Clients/Get src/WardenId.ClientManagement.Presentation/Features/Clients/Get
git commit -m "feat: add Get client endpoint"
```

---

### Task 12: Update feature (`PATCH /{clientId}`)

**Files:**
- Create: `src/WardenId.ClientManagement.Application/Features/Clients/Update/Command.cs`
- Create: `src/WardenId.ClientManagement.Application/Features/Clients/Update/Handler.cs`
- Create: `src/WardenId.ClientManagement.Presentation/Features/Clients/Update/Request.cs`
- Create: `src/WardenId.ClientManagement.Presentation/Features/Clients/Update/Endpoint.cs`

**Interfaces:**
- Consumes: `IClientRepository.GetByIdAsync`/`UpdateAsync` (Task 6), `Client.UpdateDisplayName`/`UpdateConsentRequired` (Task 2), `Client.Enable`/`Disable`/`UpdateTokenLifetimeOverrides` (existing Domain), `TokenLifetimeOverrides.Create` (existing Domain).
- Produces: `PATCH /api/v{version:apiVersion}/admin/tenants/{tenantId}/clients/{clientId}` — no later task depends on this feature's types.

- [ ] **Step 1: Write the `Command`**

`src/WardenId.ClientManagement.Application/Features/Clients/Update/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Application.Features.Clients.Update;

public sealed record Command(
    Guid TenantId,
    Guid ClientId,
    string? DisplayName,
    bool? Enabled,
    bool? ConsentRequired,
    int? AccessTokenSeconds,
    int? IdTokenSeconds,
    int? RefreshTokenSeconds,
    bool ClearTokenLifetimeOverrides) : IRequest<Result>;
```

- [ ] **Step 2: Write the `Handler`**

`src/WardenId.ClientManagement.Application/Features/Clients/Update/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.Update;

public sealed class Handler(
    IClientRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        var clientResult = await repository.GetByIdAsync(request.TenantId, request.ClientId, cancellationToken);
        if (clientResult.IsFailed) return Result.Fail(clientResult.Errors);

        var client = clientResult.Value;

        if (request.DisplayName is not null)
        {
            var displayNameResult = client.UpdateDisplayName(request.DisplayName);
            if (displayNameResult.IsFailed) return Result.Fail(displayNameResult.Errors);
        }

        if (request.Enabled is true) client.Enable();
        if (request.Enabled is false) client.Disable();

        if (request.ConsentRequired is not null) client.UpdateConsentRequired(request.ConsentRequired.Value);

        if (request.ClearTokenLifetimeOverrides)
        {
            client.UpdateTokenLifetimeOverrides(null);
        }
        else if (request.AccessTokenSeconds is not null
            || request.IdTokenSeconds is not null
            || request.RefreshTokenSeconds is not null)
        {
            var overridesResult = TokenLifetimeOverrides.Create(
                request.AccessTokenSeconds, request.IdTokenSeconds, request.RefreshTokenSeconds);
            if (overridesResult.IsFailed) return Result.Fail(overridesResult.Errors);

            client.UpdateTokenLifetimeOverrides(overridesResult.Value);
        }

        await repository.UpdateAsync(client, request.TenantId, cancellationToken);

        return Result.Ok();
    }
}
```

- [ ] **Step 3: Write the `Request`**

`src/WardenId.ClientManagement.Presentation/Features/Clients/Update/Request.cs`:

```csharp
using WardenId.ClientManagement.Application.Features.Clients.Update;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Update;

public sealed record Request(
    string? DisplayName,
    bool? Enabled,
    bool? ConsentRequired,
    int? AccessTokenSeconds,
    int? IdTokenSeconds,
    int? RefreshTokenSeconds,
    bool ClearTokenLifetimeOverrides)
{
    public Command ToCommand(Guid tenantId, Guid clientId) => new(
        tenantId, clientId, DisplayName, Enabled, ConsentRequired,
        AccessTokenSeconds, IdTokenSeconds, RefreshTokenSeconds, ClearTokenLifetimeOverrides);
}
```

- [ ] **Step 4: Write the `Endpoint`**

`src/WardenId.ClientManagement.Presentation/Features/Clients/Update/Endpoint.cs`:

```csharp
using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Update;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Clients")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPatch("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/clients/{clientId:guid}", async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid clientId,
                [FromBody] Request request,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToCommand(tenantId, clientId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .RequireAuthorization()
            .WithTags("ClientManagement - Clients")
            .WithSummary("Update a Client")
            .WithDescription("Updates display name, enabled, consent, and token lifetime overrides.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
```

- [ ] **Step 5: Build to verify**

Run: `dotnet build WardenId.slnx`
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/WardenId.ClientManagement.Application/Features/Clients/Update src/WardenId.ClientManagement.Presentation/Features/Clients/Update
git commit -m "feat: add Update client endpoint"
```

---

### Task 13: Delete feature (`DELETE /{clientId}`)

**Files:**
- Create: `src/WardenId.ClientManagement.Application/Features/Clients/Delete/Command.cs`
- Create: `src/WardenId.ClientManagement.Application/Features/Clients/Delete/Handler.cs`
- Create: `src/WardenId.ClientManagement.Presentation/Features/Clients/Delete/Endpoint.cs`

**Interfaces:**
- Consumes: `IClientRepository.GetByIdAsync`/`DeleteAsync` (Task 6), the soft-delete interceptor fix (Task 1).
- Produces: `DELETE /api/v{version:apiVersion}/admin/tenants/{tenantId}/clients/{clientId}` — no later task depends on this feature's types.

- [ ] **Step 1: Write the `Command`**

`src/WardenId.ClientManagement.Application/Features/Clients/Delete/Command.cs`:

```csharp
using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Application.Features.Clients.Delete;

public sealed record Command(
    Guid TenantId,
    Guid ClientId) : IRequest<Result>;
```

- [ ] **Step 2: Write the `Handler`**

`src/WardenId.ClientManagement.Application/Features/Clients/Delete/Handler.cs`:

```csharp
using FluentResults;
using MediatR;
using WardenId.ClientManagement.Application.Abstractions;

namespace WardenId.ClientManagement.Application.Features.Clients.Delete;

public sealed class Handler(
    IClientRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        var clientResult = await repository.GetByIdAsync(request.TenantId, request.ClientId, cancellationToken);
        if (clientResult.IsFailed) return Result.Fail(clientResult.Errors);

        await repository.DeleteAsync(clientResult.Value, request.TenantId, cancellationToken);

        return Result.Ok();
    }
}
```

- [ ] **Step 3: Write the `Endpoint`**

`src/WardenId.ClientManagement.Presentation/Features/Clients/Delete/Endpoint.cs`:

```csharp
using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.ClientManagement.Application.Features.Clients.Delete;
using WardenId.SharedKernel.Presentation;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Delete;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Clients")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapDelete("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/clients/{clientId:guid}", async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid clientId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Command(tenantId, clientId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .RequireAuthorization()
            .WithTags("ClientManagement - Clients")
            .WithSummary("Delete a Client")
            .WithDescription("Soft-deletes a client.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build WardenId.slnx`
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/WardenId.ClientManagement.Application/Features/Clients/Delete src/WardenId.ClientManagement.Presentation/Features/Clients/Delete
git commit -m "feat: add Delete client endpoint"
```

---

### Task 14: Solution-wide verification

**Files:** none (verification only).

- [ ] **Step 1: Full solution build**

Run: `dotnet build WardenId.slnx`
Expected: `Build succeeded.`, 0 errors.

- [ ] **Step 2: Full domain test suite**

Run: `dotnet test tests/WardenId.ClientManagement.Domain.Tests -v n`
Expected: all tests pass, including the two added in Task 2.

- [ ] **Step 3: Confirm the migration is the only pending one for this context**

Run:
```bash
dotnet ef migrations list \
  --project src/WardenId.ClientManagement.Infrastructure \
  --startup-project WardenId.Gateway \
  --context ClientManagementDbContext
```
Expected: exactly one migration, `AddClientManagementSchema`, no `(Pending)` markers beyond what a fresh DB would need.

- [ ] **Step 4: Manual run-through (requires a reachable Postgres — skip if none is configured in this environment)**

Run: `dotnet run --project WardenId.Gateway`
Then, with the app running, open `https://localhost:<port>/swagger` and confirm all 5 `ClientManagement - Clients` endpoints (Create/List/Get/Update/Delete) are listed under the `v1` API version. Exercise Create → Get → Update → Delete → Get (expect 404) once by hand to confirm the soft-delete fix (Task 1) actually removes the client from `Get`/`List` while leaving the row in Postgres with `IsDeleted = true`.

- [ ] **Step 5: Report**

No commit for this task — it's verification only. If any step fails, stop and fix the specific failing task before moving on; do not proceed to declare the plan complete.

---

## Self-Review Notes

- **Spec coverage:** every section of `2026-07-25-client-management-crud-design.md` maps to a task — New Projects → Tasks 3/4/6/7/8, Persistence Mapping → Task 5, Soft-Delete Fix → Task 1, Domain Additions → Task 2, CQRS Features → Tasks 9-13, Errors → no new task needed (already-existing `ClientErrors.NotFound`/`Validation` cover everything, confirmed in the spec itself), Testing → reflected in Global Constraints and Task 2/14.
- **Type consistency checked:** `Client.Id` (`Guid`) vs `Client.ClientId` (VO) usage is consistent across Tasks 9-13 — every Command/Query/Endpoint route param named `clientId` or `ClientId` (except inside `Create.Command`, which has no `ClientId` param at all, matching the spec) refers to the `Guid Id`; `ClientListItem`/`ClientDetail` explicitly project `client.ClientId.Value` for the OAuth string separately. `IClientRepository.GetByIdAsync(Guid tenantId, Guid clientId, ...)` signature (Task 3) matches every call site in Tasks 12/13.
- **No placeholders found** on re-scan.
