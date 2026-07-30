namespace WardenId.Identity.Application.Tests;

public static class TestUsers
{
    public static User CreatePendingVerification(Guid tenantId, string email = "someone@example.com", string username = "someone") =>
        User.Register(tenantId, Email.Create(email).Value, Username.Create(username).Value).Value;
}
