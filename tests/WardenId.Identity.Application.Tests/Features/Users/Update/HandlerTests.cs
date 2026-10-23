using WardenId.Identity.Application.Features.Users.Update;
using WardenId.Identity.Domain.Events;
using VerifyEmailCommand = WardenId.Identity.Application.Features.Users.VerifyEmail.Command;
using VerifyEmailHandler = WardenId.Identity.Application.Features.Users.VerifyEmail.Handler;

namespace WardenId.Identity.Application.Tests.Features.Users.Update;

public class HandlerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorUserId = Guid.NewGuid();
    private readonly FakeUserRepository _repository = new();
    private readonly FakeIdentityEmailSender _emailSender = new();

    private Handler NewHandler() => new(_repository, _emailSender);

    private async Task<User> SeedActiveUserAsync(string email = "someone@example.com", string username = "someone")
    {
        var user = TestUsers.CreatePendingVerification(_tenantId, email, username);
        user.AddEmailVerificationToken(EmailVerificationToken.Create("first", DateTimeOffset.UtcNow.AddHours(1)));
        Assert.True(user.VerifyEmail("first").IsSuccess);
        user.ClearDomainEvents();
        await _repository.CreateAsync(user, user.Id);
        return user;
    }

    [Fact]
    public async Task Handle_ChangesTheEmail_AndSendsAVerificationEmailToTheNewAddress()
    {
        var user = await SeedActiveUserAsync();

        var result = await NewHandler().Handle(
            new Command(_tenantId, user.Id, " Changed@Example.com ", null, null, null, _actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("changed@example.com", user.Email.Value);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Contains(RequiredAction.VerifyEmail, user.PendingActions);
        var sent = Assert.Single(_emailSender.SentVerificationEmails);
        Assert.Equal("changed@example.com", sent.ToEmail);
        Assert.Equal("someone", sent.Username);
        Assert.Equal(1, _repository.UpdateCallCount);
        Assert.Equal(_actorUserId, _repository.LastActorId);
    }

    [Fact]
    public async Task Handle_TheSentToken_VerifiesTheNewEmail()
    {
        var user = await SeedActiveUserAsync();
        await NewHandler().Handle(new Command(_tenantId, user.Id, "changed@example.com", null, null, null, _actorUserId), CancellationToken.None);
        var rawToken = _emailSender.SentVerificationEmails.Single().RawToken;

        var verify = await new VerifyEmailHandler(_repository).Handle(
            new VerifyEmailCommand(_tenantId, user.Id, rawToken), CancellationToken.None);

        Assert.True(verify.IsSuccess);
        Assert.Empty(user.PendingActions);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public async Task Handle_ChangesTheUsername_WithoutReverification()
    {
        var user = await SeedActiveUserAsync();

        var result = await NewHandler().Handle(
            new Command(_tenantId, user.Id, null, " renamed ", null, null, _actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("renamed", user.Username.Value);
        Assert.Empty(user.PendingActions);
        Assert.Empty(_emailSender.SentVerificationEmails);
        Assert.Contains(user.DomainEvents, e => e is UserUsernameChangedDomainEvent);
        Assert.Equal(1, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_ChangesBoth_AndSendsTheVerificationWithTheNewUsername()
    {
        var user = await SeedActiveUserAsync();

        var result = await NewHandler().Handle(
            new Command(_tenantId, user.Id, "changed@example.com", "renamed", null, null, _actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("renamed", Assert.Single(_emailSender.SentVerificationEmails).Username);
        Assert.Contains(user.DomainEvents, e => e is UserEmailChangedDomainEvent);
        Assert.Contains(user.DomainEvents, e => e is UserUsernameChangedDomainEvent);
    }

    [Fact]
    public async Task Handle_WithNothingChanged_SucceedsWithoutSavingOrSending()
    {
        var user = await SeedActiveUserAsync();

        var result = await NewHandler().Handle(
            new Command(_tenantId, user.Id, "SOMEONE@example.com", "someone", null, null, _actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _repository.UpdateCallCount);
        Assert.Empty(_emailSender.SentVerificationEmails);
        Assert.Empty(user.PendingActions);
    }

    [Fact]
    public async Task Handle_WithNeitherField_SucceedsWithoutSaving()
    {
        var user = await SeedActiveUserAsync();

        var result = await NewHandler().Handle(new Command(_tenantId, user.Id, null, null, null, null, _actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_FailsWithEmailAlreadyInUse_WhenAnotherUserHasTheEmail()
    {
        var user = await SeedActiveUserAsync();
        await SeedActiveUserAsync("taken@example.com", "other");

        var result = await NewHandler().Handle(
            new Command(_tenantId, user.Id, "TAKEN@example.com", null, null, null, _actorUserId), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrors.EmailAlreadyInUse().Message, result.Errors[0].Message);
        Assert.Equal("someone@example.com", user.Email.Value);
        Assert.Equal(0, _repository.UpdateCallCount);
        Assert.Empty(_emailSender.SentVerificationEmails);
    }

    [Fact]
    public async Task Handle_FailsWithUsernameAlreadyInUse_WhenAnotherUserHasTheUsername()
    {
        var user = await SeedActiveUserAsync();
        await SeedActiveUserAsync("other@example.com", "taken");

        var result = await NewHandler().Handle(
            new Command(_tenantId, user.Id, "changed@example.com", "taken", null, null, _actorUserId), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrors.UsernameAlreadyInUse().Message, result.Errors[0].Message);
        Assert.Equal("someone@example.com", user.Email.Value);
        Assert.Equal(0, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_Succeeds_WhenAUserInAnotherTenantHasTheEmail()
    {
        var user = await SeedActiveUserAsync();
        var otherTenantUser = TestUsers.CreatePendingVerification(Guid.NewGuid(), "changed@example.com", "renamed");
        await _repository.CreateAsync(otherTenantUser, otherTenantUser.Id);

        var result = await NewHandler().Handle(
            new Command(_tenantId, user.Id, "changed@example.com", "renamed", null, null, _actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("not-an-email", null)]
    [InlineData("", null)]
    [InlineData(null, "x")]
    [InlineData(null, "has space")]
    public async Task Handle_FailsWithValidation_ForAnInvalidValue(string? email, string? username)
    {
        var user = await SeedActiveUserAsync();

        var result = await NewHandler().Handle(new Command(_tenantId, user.Id, email, username, null, null, _actorUserId), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Equal(0, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_FailsWithValidation_WhenTheUserIsDisabled()
    {
        var user = await SeedActiveUserAsync();
        user.Disable("investigation");

        var result = await NewHandler().Handle(
            new Command(_tenantId, user.Id, "changed@example.com", null, null, null, _actorUserId), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Equal(0, _repository.UpdateCallCount);
        Assert.Empty(_emailSender.SentVerificationEmails);
    }

    [Fact]
    public async Task Handle_FailsWithNotFound_WhenTheUserDoesNotExist()
    {
        var result = await NewHandler().Handle(
            new Command(_tenantId, Guid.NewGuid(), "changed@example.com", null, null, null, _actorUserId), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.NotFound, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_SetsTheNames_WithoutReverification()
    {
        var user = await SeedActiveUserAsync();

        var result = await NewHandler().Handle(
            new Command(_tenantId, user.Id, null, null, " Ada ", "Lovelace", _actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada", user.FirstName?.Value);
        Assert.Equal("Lovelace", user.LastName?.Value);
        Assert.Empty(user.PendingActions);
        Assert.Empty(_emailSender.SentVerificationEmails);
        Assert.Contains(user.DomainEvents, e => e is UserPersonNameChangedDomainEvent);
        Assert.Equal(1, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_LeavesANullName_AndRemovesABlankOne()
    {
        var user = await SeedActiveUserAsync();
        await NewHandler().Handle(new Command(_tenantId, user.Id, null, null, "Ada", "Lovelace", _actorUserId), CancellationToken.None);

        var result = await NewHandler().Handle(
            new Command(_tenantId, user.Id, null, null, null, "  ", _actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada", user.FirstName?.Value);
        Assert.Null(user.LastName);
        Assert.Equal(2, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_WithTheSameNames_SucceedsWithoutSaving()
    {
        var user = await SeedActiveUserAsync();
        await NewHandler().Handle(new Command(_tenantId, user.Id, null, null, "Ada", null, _actorUserId), CancellationToken.None);

        var result = await NewHandler().Handle(
            new Command(_tenantId, user.Id, null, null, " Ada ", "", _actorUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_FailsWithValidation_WhenANameIsTooLong()
    {
        var user = await SeedActiveUserAsync();

        var result = await NewHandler().Handle(
            new Command(_tenantId, user.Id, null, null, new string('a', PersonName.MaxLength + 1), null, _actorUserId),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Equal(0, _repository.UpdateCallCount);
    }
}
