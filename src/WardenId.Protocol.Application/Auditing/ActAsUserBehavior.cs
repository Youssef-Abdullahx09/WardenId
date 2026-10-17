using MediatR;
using WardenId.SharedKernel.Application.Abstractions;

namespace WardenId.Protocol.Application.Auditing;

/// <summary>
/// Runs an <see cref="IActsAsUser"/> command inside an audit actor scope for its user, so the admin.* capture of the
/// Identity writes it sends (failed-attempt counters, factors, required actions, the password) names the user and
/// lands in the user's tenant instead of an anonymous platform actor. Wrapping the handler (not a page) matters: the
/// scope is AsyncLocal and only flows into calls made inside it. Every other request passes straight through.
/// </summary>
public sealed class ActAsUserBehavior<TRequest, TResponse>(IAuditActorScope actorScope)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IActsAsUser acting) return await next();

        using (actorScope.BeginEndUser(acting.TenantId, acting.UserId))
            return await next();
    }
}
