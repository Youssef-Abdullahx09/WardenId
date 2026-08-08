using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Features.Users.Register;

namespace WardenId.Identity.Application.Tests.Features.Users.Register;

public class HandlerTests
{
    private static Handler CreateHandler(
        FakeUserRepository repository,
        FakePasswordHasher? passwordHasher = null,
        FakeBreachedPasswordChecker? breachedPasswordChecker = null,
        FakeIdentityEmailSender? emailSender = null,
        IdentityPolicyOptions? options = null) =>
        new(
            repository,
            passwordHasher ?? new FakePasswordHasher(),
            breachedPasswordChecker ?? new FakeBreachedPasswordChecker(),
            emailSender ?? new FakeIdentityEmailSender(),
            Options.Create(options ?? new IdentityPolicyOptions()));

    [Fact]
    public async Task Handle_ReturnsUserIdAndRawToken_WhenEmailAndUsernameAreUnique()
    {
        var emailSender = new FakeIdentityEmailSender();
        var handler = CreateHandler(new FakeUserRepository(), emailSender: emailSender);

        var result = await handler.Handle(
            new Command(Guid.NewGuid(), "someone@example.com", "someone", "P@ssw0rd1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.UserId);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RawVerificationToken));
        Assert.Single(emailSender.SentVerificationEmails);
        Assert.Equal("someone@example.com", emailSender.SentVerificationEmails[0].ToEmail);
        Assert.Equal(result.Value.RawVerificationToken, emailSender.SentVerificationEmails[0].RawToken);
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
