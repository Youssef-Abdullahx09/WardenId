using WardenId.Identity.Application.Features.Users.Create;

namespace WardenId.Identity.Application.Tests.Features.Users.Create;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    private static (Handler Handler, FakeUserRepository Repository, FakeIdentityEmailSender Email) CreateHandler()
    {
        var repository = new FakeUserRepository();
        var emailSender = new FakeIdentityEmailSender();
        var handler = new Handler(
            repository,
            new FakePasswordHasher(),
            new FakeBreachedPasswordChecker(),
            emailSender,
            TestPolicies.Resolver(new IdentityPolicyOptions()));
        return (handler, repository, emailSender);
    }

    private static Command NewUser(bool requirePasswordChange = true, string email = "new@example.com", string password = "P@ssw0rd1") =>
        new(TenantId, email, "newuser", null, null, password, requirePasswordChange, AdminId);

    [Fact]
    public async Task Handle_CreatesTheUser_AsTheAdmin_AndAsksForANewPassword()
    {
        var (handler, repository, emailSender) = CreateHandler();

        var result = await handler.Handle(NewUser(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = (await repository.GetByIdAsync(TenantId, result.Value.UserId, CancellationToken.None)).Value;
        Assert.Contains(RequiredAction.ResetPassword, user.PendingActions);
        Assert.Contains(RequiredAction.VerifyEmail, user.PendingActions);
        Assert.Equal(AdminId, repository.LastActorId);
        Assert.Equal("new@example.com", Assert.Single(emailSender.SentVerificationEmails).ToEmail);
    }

    [Fact]
    public async Task Handle_CanKeepTheAdminsPassword()
    {
        var (handler, repository, _) = CreateHandler();

        var result = await handler.Handle(NewUser(requirePasswordChange: false), CancellationToken.None);

        var user = (await repository.GetByIdAsync(TenantId, result.Value.UserId, CancellationToken.None)).Value;
        Assert.DoesNotContain(RequiredAction.ResetPassword, user.PendingActions);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheEmailIsTaken()
    {
        var (handler, _, _) = CreateHandler();
        await handler.Handle(NewUser(), CancellationToken.None);

        var result = await handler.Handle(NewUser() with { Username = "other" }, CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenThePasswordBreaksThePolicy()
    {
        var (handler, repository, emailSender) = CreateHandler();

        var result = await handler.Handle(NewUser(password: "short"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Empty(emailSender.SentVerificationEmails);
        Assert.False(await repository.ExistsByUsernameAsync(TenantId, Username.Create("newuser").Value, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CreatesTheUser_EvenWhenTheTenantDisabledSelfRegistration()
    {
        var tenantPolicies = new FakeIdentityTenantPolicyRepository()
            .With(TenantId, IdentityPolicyOverrides.None with { SelfRegistrationEnabled = false });
        var handler = new Handler(
            new FakeUserRepository(), new FakePasswordHasher(), new FakeBreachedPasswordChecker(), new FakeIdentityEmailSender(),
            TestPolicies.Resolver(new IdentityPolicyOptions { SelfRegistrationEnabled = false }, tenantPolicies));

        var result = await handler.Handle(NewUser(), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
