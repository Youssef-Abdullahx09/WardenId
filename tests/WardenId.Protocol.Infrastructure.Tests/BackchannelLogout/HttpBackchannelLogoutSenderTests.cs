using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.Protocol.Infrastructure.BackchannelLogout;
using WardenId.Protocol.Infrastructure.Tests.Fakes;

namespace WardenId.Protocol.Infrastructure.Tests.BackchannelLogout;

public class HttpBackchannelLogoutSenderTests
{
    private static readonly Uri Target = new("https://app.example/bcl");

    private static (HttpBackchannelLogoutSender Sender, FakeHttpMessageHandler Handler) Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond, TimeSpan? timeout = null)
    {
        var handler = new FakeHttpMessageHandler(respond);
        var client = new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(5) };
        return (new HttpBackchannelLogoutSender(client, NullLogger<HttpBackchannelLogoutSender>.Instance), handler);
    }

    private static (HttpBackchannelLogoutSender Sender, FakeHttpMessageHandler Handler) Responding(int status) =>
        Create((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));

    private static async Task<HttpResponseMessage> Hang(HttpRequestMessage _, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendAsync_PostsTheLogoutTokenAsFormBody()
    {
        var (sender, handler) = Responding(200);

        await sender.SendAsync(Target, "header.payload.signature", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(Target, request.Uri);
        Assert.Equal("application/x-www-form-urlencoded", request.MediaType);
        Assert.Equal("logout_token=header.payload.signature", request.Body);
    }

    [Fact]
    public async Task SendAsync_UriWithUserInfo_IsRejected_WithoutSending()
    {
        var (sender, handler) = Responding(200);

        var outcome = await sender.SendAsync(new Uri("https://user:pass@app.example/bcl"), "t", CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Rejected, outcome);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(204)]
    public async Task SendAsync_2xx_IsDelivered(int status)
    {
        var (sender, _) = Responding(status);

        Assert.Equal(DeliveryOutcome.Delivered, await sender.SendAsync(Target, "t", CancellationToken.None));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(302)]
    public async Task SendAsync_3xxOr4xx_IsRejected(int status)
    {
        var (sender, _) = Responding(status);

        Assert.Equal(DeliveryOutcome.Rejected, await sender.SendAsync(Target, "t", CancellationToken.None));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    public async Task SendAsync_5xx_IsTransientFailure(int status)
    {
        var (sender, _) = Responding(status);

        Assert.Equal(DeliveryOutcome.TransientFailure, await sender.SendAsync(Target, "t", CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_Timeout_IsTransientFailure()
    {
        var (sender, _) = Create(Hang, timeout: TimeSpan.FromMilliseconds(100));

        Assert.Equal(DeliveryOutcome.TransientFailure, await sender.SendAsync(Target, "t", CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_NetworkError_IsTransientFailure()
    {
        var (sender, _) = Create((_, _) => throw new HttpRequestException("connection refused"));

        Assert.Equal(DeliveryOutcome.TransientFailure, await sender.SendAsync(Target, "t", CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_CallerCancellation_Propagates()
    {
        var (sender, _) = Create(Hang);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.SendAsync(Target, "t", cancellation.Token));
    }

    [Theory]
    [InlineData("http://app.example/bcl")]
    [InlineData("https://app.example/bcl#fragment")]
    [InlineData("ftp://app.example/bcl")]
    public async Task SendAsync_DisallowedUri_IsRejected_WithoutSending(string uri)
    {
        var (sender, handler) = Responding(200);

        Assert.Equal(DeliveryOutcome.Rejected, await sender.SendAsync(new Uri(uri), "t", CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendAsync_RelativeUri_IsRejected_WithoutSending()
    {
        var (sender, handler) = Responding(200);

        Assert.Equal(DeliveryOutcome.Rejected, await sender.SendAsync(new Uri("/bcl", UriKind.Relative), "t", CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("http://localhost:5001/bcl")]
    [InlineData("http://127.0.0.1/bcl")]
    [InlineData("http://[::1]/bcl")]
    public async Task SendAsync_LoopbackHttp_IsSent(string uri)
    {
        var (sender, handler) = Responding(200);

        Assert.Equal(DeliveryOutcome.Delivered, await sender.SendAsync(new Uri(uri), "t", CancellationToken.None));
        Assert.Single(handler.Requests);
    }
}
