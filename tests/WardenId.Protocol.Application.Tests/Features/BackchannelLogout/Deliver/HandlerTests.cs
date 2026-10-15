using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.Protocol.Application.Features.BackchannelLogout.Deliver;
using GetForAuthenticationQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication.Query;

namespace WardenId.Protocol.Application.Tests.Features.BackchannelLogout.Deliver;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid Sid = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();
    private static readonly BackchannelLogoutRequested Message = new(TenantId, "web", Sid, SubjectId);

    private static Result<ClientForAuthentication> Client(
        string? backchannelLogoutUri = "https://app.example/bcl", bool enabled = true) =>
        Result.Ok(new ClientForAuthentication(
            Guid.NewGuid(), "web", ClientKind.Confidential, enabled, [AllowedGrant.AuthorizationCode],
            ClientAuthMethod.SharedSecret, null, null, ["https://app.example/cb"], false,
            [], backchannelLogoutUri));

    private sealed record Sut(Handler Handler, FakeSender Sender, FakeLogoutTokenFactory Tokens, FakeBackchannelLogoutSender Http);

    private static Sut Create(Result<ClientForAuthentication> client)
    {
        var sender = new FakeSender { ClientResponse = client };
        var tokens = new FakeLogoutTokenFactory();
        var http = new FakeBackchannelLogoutSender();
        return new Sut(new Handler(sender, tokens, http), sender, tokens, http);
    }

    [Fact]
    public async Task Handle_SignsAndSends_ToTheClientsBackchannelUri()
    {
        var sut = Create(Client());

        var result = await sut.Handler.Handle(new Command(Message), CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Delivered, result.Value);
        Assert.Equal(new GetForAuthenticationQuery(TenantId, "web"), Assert.Single(sut.Sender.Sent));
        Assert.Equal(new FakeLogoutTokenFactory.Call(TenantId, "web", SubjectId, Sid), Assert.Single(sut.Tokens.Calls));
        Assert.Equal(
            new FakeBackchannelLogoutSender.Call(new Uri("https://app.example/bcl"), "logout-token"),
            Assert.Single(sut.Http.Calls));
    }

    [Theory]
    [InlineData(DeliveryOutcome.Rejected)]
    [InlineData(DeliveryOutcome.TransientFailure)]
    public async Task Handle_ReturnsTheSendersOutcome(DeliveryOutcome outcome)
    {
        var sut = Create(Client());
        sut.Http.Outcome = outcome;

        var result = await sut.Handler.Handle(new Command(Message), CancellationToken.None);

        Assert.Equal(outcome, result.Value);
    }

    [Fact]
    public async Task Handle_UnknownOrDeletedClient_IsSkipped()
    {
        var sut = Create(Result.Fail<ClientForAuthentication>(
            new Error("Client not found.").WithMetadata("ErrorCode", "NotFound")));

        var result = await sut.Handler.Handle(new Command(Message), CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Skipped, result.Value);
        Assert.Empty(sut.Tokens.Calls);
        Assert.Empty(sut.Http.Calls);
    }

    [Fact]
    public async Task Handle_DisabledClient_IsSkipped()
    {
        var sut = Create(Client(enabled: false));

        var result = await sut.Handler.Handle(new Command(Message), CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Skipped, result.Value);
        Assert.Empty(sut.Http.Calls);
    }

    [Fact]
    public async Task Handle_ClientWithoutBackchannelUri_IsSkipped()
    {
        var sut = Create(Client(backchannelLogoutUri: null));

        var result = await sut.Handler.Handle(new Command(Message), CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Skipped, result.Value);
        Assert.Empty(sut.Tokens.Calls);
    }

    [Fact]
    public async Task Handle_UnparsableStoredUri_IsRejected_WithoutSigning()
    {
        var sut = Create(Client(backchannelLogoutUri: "not a uri"));

        var result = await sut.Handler.Handle(new Command(Message), CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Rejected, result.Value);
        Assert.Empty(sut.Tokens.Calls);
        Assert.Empty(sut.Http.Calls);
    }

    [Fact]
    public async Task Handle_TokenFactoryFailure_IsTransient_AndNothingIsSent()
    {
        var sut = Create(Client());
        sut.Tokens.Response = Result.Fail<string>("Tenant has no active signing key.");

        var result = await sut.Handler.Handle(new Command(Message), CancellationToken.None);

        Assert.Equal(DeliveryOutcome.TransientFailure, result.Value);
        Assert.Empty(sut.Http.Calls);
    }
}
