using MediatR;

namespace WardenId.Protocol.Infrastructure.Tests.Fakes;

/// <summary>Answers each request type with a configured response and records every request sent.</summary>
public sealed class StubSender : ISender
{
    private readonly Dictionary<Type, object> _responses = [];

    public List<object> Sent { get; } = [];

    public StubSender Returns<TRequest, TResponse>(TResponse response) where TRequest : IRequest<TResponse>
    {
        _responses[typeof(TRequest)] = response!;
        return this;
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        Sent.Add(request);
        return _responses.TryGetValue(request.GetType(), out var response)
            ? Task.FromResult((TResponse)response)
            : throw new NotSupportedException($"No response configured for {request.GetType()}.");
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
        throw new NotSupportedException();

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
