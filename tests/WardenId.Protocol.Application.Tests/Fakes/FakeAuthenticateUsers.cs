namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeAuthenticateUsers : IAuthenticateUsers
{
    public Result<AuthenticatedUserInfo>? Response { get; set; }

    public Task<Result<AuthenticatedUserInfo>> AuthenticateAsync(
        Guid tenantId, string emailOrUsername, string password, CancellationToken cancellationToken) =>
        Task.FromResult(Response ?? throw new InvalidOperationException("Response not configured."));
}
