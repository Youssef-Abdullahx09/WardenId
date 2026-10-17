using MediatR;
using WardenId.Protocol.Application.Auditing;

namespace WardenId.Protocol.Application.Tests.Auditing;

public class ActAsUserBehaviorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed record UserCommand(Guid TenantId, Guid UserId) : IRequest<string>, IActsAsUser;

    private sealed record OtherCommand : IRequest<string>;

    [Fact]
    public async Task ActsAsUser_RunsTheHandlerInsideAScopeForItsUser_AndEndsItAfterwards()
    {
        var scope = new FakeAuditActorScope();
        (Guid, Guid)? seen = null;

        var response = await new ActAsUserBehavior<UserCommand, string>(scope).Handle(
            new UserCommand(TenantId, UserId),
            _ =>
            {
                seen = scope.Current;
                return Task.FromResult("done");
            },
            CancellationToken.None);

        Assert.Equal("done", response);
        Assert.Equal((TenantId, UserId), seen);
        Assert.Null(scope.Current);
    }

    [Fact]
    public async Task OtherRequests_PassStraightThrough()
    {
        var scope = new FakeAuditActorScope();

        var response = await new ActAsUserBehavior<OtherCommand, string>(scope).Handle(
            new OtherCommand(), _ => Task.FromResult("done"), CancellationToken.None);

        Assert.Equal("done", response);
        Assert.Empty(scope.Begun);
    }
}
