using FluentResults;
using MediatR;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning;

namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeSender : ISender
{
    public Result<AuthenticatedUser>? AuthenticateResponse { get; set; }
    public Result<ActiveSigningKeyDto>? SigningKeyResponse { get; set; }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        return request switch
        {
            Command => (Task<TResponse>)(object)Task.FromResult(
                AuthenticateResponse ?? throw new InvalidOperationException("AuthenticateResponse not configured.")),
            Query => (Task<TResponse>)(object)Task.FromResult(
                SigningKeyResponse ?? throw new InvalidOperationException("SigningKeyResponse not configured.")),
            _ => throw new NotSupportedException($"Unexpected request type: {request.GetType()}")
        };
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
