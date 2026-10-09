using FluentResults;
using MediatR;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using TenancyActiveSigningKey = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.ActiveSigningKey;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys;
using AuthenticateCommand = WardenId.Identity.Integration.Command.Features.Users.Authenticate.Command;
using GetActiveForSigningQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.Query;
using GetForAuthenticationQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication.Query;
using GetPublicKeysQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys.Query;

namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class FakeSender : ISender
{
    public Result<AuthenticatedUser>? AuthenticateResponse { get; set; }
    public Result<TenancyActiveSigningKey>? SigningKeyResponse { get; set; }
    public Result<TenantPublicKeys>? PublicKeysResponse { get; set; }

    public Result<ClientForAuthentication>? ClientResponse { get; set; }

    public List<object> Sent { get; } = [];

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        Sent.Add(request);
        return request switch
        {
            AuthenticateCommand => (Task<TResponse>)(object)Task.FromResult(
                AuthenticateResponse ?? throw new InvalidOperationException("AuthenticateResponse not configured.")),
            GetActiveForSigningQuery => (Task<TResponse>)(object)Task.FromResult(
                SigningKeyResponse ?? throw new InvalidOperationException("SigningKeyResponse not configured.")),
            GetPublicKeysQuery => (Task<TResponse>)(object)Task.FromResult(
                PublicKeysResponse ?? throw new InvalidOperationException("PublicKeysResponse not configured.")),
            GetForAuthenticationQuery => (Task<TResponse>)(object)Task.FromResult(
                ClientResponse ?? throw new InvalidOperationException("ClientResponse not configured.")),
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
