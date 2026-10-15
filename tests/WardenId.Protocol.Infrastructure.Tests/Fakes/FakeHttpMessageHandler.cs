namespace WardenId.Protocol.Infrastructure.Tests.Fakes;

public sealed class FakeHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public sealed record RecordedRequest(HttpMethod Method, Uri? Uri, string? MediaType, string Body);

    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Read the body now: the sender disposes the content as soon as SendAsync returns.
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri, request.Content?.Headers.ContentType?.MediaType, body));
        return await respond(request, cancellationToken);
    }
}
