using WardenId.Protocol.Application.Features.BackchannelLogout;

namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeLogoutTokenFactory : ILogoutTokenFactory
{
    public sealed record Call(Guid TenantId, string ClientId, Guid SubjectId, Guid Sid);

    public Result<string> Response { get; set; } = Result.Ok("logout-token");

    public List<Call> Calls { get; } = [];

    public Task<Result<string>> CreateAsync(Guid tenantId, string clientId, Guid subjectId, Guid sid, CancellationToken cancellationToken)
    {
        Calls.Add(new Call(tenantId, clientId, subjectId, sid));
        return Task.FromResult(Response);
    }
}
