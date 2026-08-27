using System.Net;
using Microsoft.Extensions.Options;
using WardenId.Audit.Infrastructure.Witnessing;

namespace WardenId.Audit.Infrastructure.Tests;

public class WebhookCheckpointWitnessTests
{
    private sealed class StubHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode);
        }
    }

    [Fact]
    public async Task WitnessAsync_PostsSignedNoteText_ToConfiguredUrl()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var sut = new WebhookCheckpointWitness(httpClient, Options.Create(new AuditWitnessOptions { WebhookUrl = "https://sink.example/hook" }));

        await sut.WitnessAsync("warden-audit/platform/adminconfig\n5\nroot==\n\n— kid sig==\n");

        Assert.Equal("https://sink.example/hook", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("warden-audit/platform/adminconfig\n5\nroot==\n\n— kid sig==\n", handler.LastBody);
    }

    [Fact]
    public async Task WitnessAsync_NonSuccessStatus_Throws()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError);
        using var httpClient = new HttpClient(handler);
        var sut = new WebhookCheckpointWitness(httpClient, Options.Create(new AuditWitnessOptions { WebhookUrl = "https://sink.example/hook" }));

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.WitnessAsync("note"));
    }

    [Fact]
    public async Task WitnessAsync_NoUrlConfigured_Throws()
    {
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.OK));
        var sut = new WebhookCheckpointWitness(httpClient, Options.Create(new AuditWitnessOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.WitnessAsync("note"));
    }
}
