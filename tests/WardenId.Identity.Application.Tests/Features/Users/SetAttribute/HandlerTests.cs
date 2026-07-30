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
