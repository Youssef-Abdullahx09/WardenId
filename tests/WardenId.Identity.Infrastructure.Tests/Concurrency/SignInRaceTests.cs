using FluentResults;
using Microsoft.AspNetCore.DataProtection;
using WardenId.Identity.Application;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Mfa;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using AuthenticateCommand = WardenId.Identity.Integration.Command.Features.Users.Authenticate.Command;
using AuthenticateHandler = WardenId.Identity.Application.Features.Users.Authenticate.Handler;
using CompleteCommand = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.Command;
using CompleteHandler = WardenId.Identity.Application.Features.Users.CompleteTotpEnrollment.Handler;
using VerifyCommand = WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor.Command;
using VerifyHandler = WardenId.Identity.Application.Features.Users.VerifySecondFactor.Handler;

namespace WardenId.Identity.Infrastructure.Tests.Concurrency;

/// <summary>
/// Requests racing on one user, each with its own DbContext like separate HTTP requests. The xmin tokens on
/// Users, MfaFactors and RecoveryCodes must stop a code from being accepted twice and a failed attempt from being
/// lost to a concurrent write.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SignInRaceTests(PostgresFixture database)
{
    private const string Password = "Correct-Horse-1";
    private const string RecoveryCode = "abcde-fgh23";

    private static readonly byte[] Secret = "12345678901234567890"u8.ToArray();
    private static readonly ITotpSecretProtector Protector = new DataProtectionTotpSecretProtector(new EphemeralDataProtectionProvider());

    private readonly Guid _tenantId = Guid.NewGuid();

    [SkippableFact]
    public async Task ParallelWrongCodes_AreEachCounted()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync();
        const int requests = VerifyHandlerMaxAttempts;

        var results = await RaceAsync(requests, () => VerifyAsync(userId, "000000", new IdentityPolicyOptions { MaxFailedLoginAttempts = 100 }));

        Assert.All(results, r => Assert.Equal(SecondFactorFailureReason.InvalidCode, SecondFactorFailure.Read(r.Errors)));
        var user = await LoadAsync(userId);
        Assert.Equal(requests, user.Lockout.FailedAttemptCount);
    }

    [SkippableFact]
    public async Task ParallelWrongCodes_LockTheAccount_AtTheLimit()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync();

        var results = await RaceAsync(10, () => VerifyAsync(userId, "000000", new IdentityPolicyOptions { MaxFailedLoginAttempts = 3 }));

        Assert.All(results, r => Assert.True(r.IsFailed));
        var user = await LoadAsync(userId);
        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.Equal(3, user.Lockout.FailedAttemptCount);
        Assert.Single(results, r => SecondFactorFailure.Read(r.Errors) == SecondFactorFailureReason.LockedOut);
    }

    [SkippableFact]
    public async Task ParallelRequestsWithOneTotpCode_AcceptItOnce()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync();
        var code = Totp.ComputeCode(Secret, Totp.TimeStepAt(DateTimeOffset.UtcNow));

        var results = await RaceAsync(8, () => VerifyAsync(userId, code));

        // Near a step boundary the "same" code may not be accepted at all; it is never accepted twice.
        Assert.True(results.Count(r => r.IsSuccess) <= 1);
        Assert.All(results.Where(r => r.IsFailed), r => Assert.Equal(SecondFactorFailureReason.InvalidCode, SecondFactorFailure.Read(r.Errors)));
    }

    [SkippableFact]
    public async Task ParallelRequestsWithOneRecoveryCode_UseItOnce()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync();

        var results = await RaceAsync(8, () => VerifyAsync(userId, RecoveryCode));

        var success = Assert.Single(results, r => r.IsSuccess);
        Assert.Equal(SecondFactorMethod.RecoveryCode, success.Value.Method);
        Assert.All(results.Where(r => r.IsFailed), r => Assert.Equal(SecondFactorFailureReason.InvalidCode, SecondFactorFailure.Read(r.Errors)));
        var user = await LoadAsync(userId);
        Assert.Equal(1, user.RemainingRecoveryCodeCount);
    }

    [SkippableFact]
    public async Task ParallelWrongPasswords_AreEachCounted()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync();
        const int requests = AuthenticateHandlerMaxAttempts;
        var email = (await LoadAsync(userId)).Email.Value;

        var results = await RaceAsync(requests, async () =>
        {
            await using var context = database.CreateContext();
            var handler = new AuthenticateHandler(new UserRepository(context), new PlainPasswordHasher(),
                GlobalPolicies.Resolver(new IdentityPolicyOptions { MaxFailedLoginAttempts = 100 }));
            return (IResultBase)await handler.Handle(new AuthenticateCommand(_tenantId, email, "wrong-password"), CancellationToken.None);
        });

        Assert.All(results, r => Assert.True(r.IsFailed));
        Assert.Equal(requests, (await LoadAsync(userId)).Lockout.FailedAttemptCount);
    }

    [SkippableFact]
    public async Task ParallelEnrollmentCompletions_EnableTheFactorOnce()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync();
        var factorId = Guid.Empty;
        await InRequestAsync(async repository =>
        {
            var user = (await repository.GetByIdAsync(_tenantId, userId)).Value;
            var factor = MfaFactor.Create(MfaFactorType.Totp, Protector.Protect(Secret), DateTimeOffset.UtcNow);
            Assert.True(user.BeginMfaEnrollment(factor).IsSuccess);
            factorId = factor.Id;
            Assert.True((await repository.UpdateAsync(user, user.Id)).IsSuccess);
        });
        var code = Totp.ComputeCode(Secret, Totp.TimeStepAt(DateTimeOffset.UtcNow));

        var results = await RaceAsync(6, async () =>
        {
            await using var context = database.CreateContext();
            var handler = new CompleteHandler(new UserRepository(context), Protector, TimeProvider.System);
            return (IResultBase)await handler.Handle(new CompleteCommand(_tenantId, userId, factorId, code), CancellationToken.None);
        });

        Assert.True(results.Count(r => r.IsSuccess) <= 1);
        var reloaded = await LoadAsync(userId);
        if (results.Any(r => r.IsSuccess))
        {
            Assert.True(reloaded.MfaFactors.Single(f => f.Id == factorId).Enabled);
            Assert.Equal(RecoveryCodes.Count, reloaded.RecoveryCodes.Count);
        }
    }

    private const int VerifyHandlerMaxAttempts = 5;
    private const int AuthenticateHandlerMaxAttempts = 3;

    private async Task<Result<SecondFactorVerified>> VerifyAsync(Guid userId, string code, IdentityPolicyOptions? options = null)
    {
        await using var context = database.CreateContext();
        var handler = new VerifyHandler(new UserRepository(context),
            GlobalPolicies.Resolver(options ?? new IdentityPolicyOptions { MaxFailedLoginAttempts = 100 }), Protector, TimeProvider.System);
        return await handler.Handle(new VerifyCommand(_tenantId, userId, code), CancellationToken.None);
    }

    /// <summary>Starts every request at once, on the thread pool, and waits for all of them.</summary>
    private static async Task<T[]> RaceAsync<T>(int count, Func<Task<T>> request)
    {
        using var start = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, count).Select(_ => Task.Run(async () =>
        {
            start.Wait();
            return await request();
        })).ToArray();
        start.Set();
        return await Task.WhenAll(tasks);
    }

    private async Task<Guid> SeedUserAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var user = User.Register(_tenantId, Email.FromTrustedSource($"{suffix}@example.test"), Username.FromTrustedSource($"user{suffix}")).Value;
        user.SetPassword(PasswordCredential.Create(PlainPasswordHasher.HashOf(Password), "plain", 1, DateTimeOffset.UtcNow),
            PasswordPolicy.FromTrustedSource(8, false, false, false, false, historyCount: 0));
        var factor = MfaFactor.Create(MfaFactorType.Totp, Protector.Protect(Secret), DateTimeOffset.UtcNow.AddHours(-1));
        user.BeginMfaEnrollment(factor);
        var codes = new[]
        {
            Domain.RecoveryCode.Create(RecoveryCodes.Hash(RecoveryCodes.Normalize(RecoveryCode)!)),
            Domain.RecoveryCode.Create("unused")
        };
        // The first code was an hour ago, so every code around now is newer.
        user.CompleteMfaEnrollment(factor.Id, Totp.TimeStepAt(DateTimeOffset.UtcNow.AddHours(-1)), codes, DateTimeOffset.UtcNow.AddHours(-1));

        await InRequestAsync(repository => repository.CreateAsync(user, user.Id));
        return user.Id;
    }

    private async Task<User> LoadAsync(Guid userId)
    {
        await using var context = database.CreateContext();
        return (await new UserRepository(context).GetByIdAsync(_tenantId, userId)).Value;
    }

    private async Task InRequestAsync(Func<UserRepository, Task> step)
    {
        await using var context = database.CreateContext();
        await step(new UserRepository(context));
    }

    private sealed class PlainPasswordHasher : IPasswordHasher
    {
        public static string HashOf(string plaintext) => $"plain:{plaintext}";

        public string Algorithm => "plain";

        public string Hash(string plaintext) => HashOf(plaintext);

        public bool Verify(string plaintext, string hash) => hash == HashOf(plaintext);

        public bool NeedsRehash(string hash) => false;
    }
}
