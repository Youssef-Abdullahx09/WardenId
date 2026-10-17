namespace WardenId.Protocol.Application.Auditing;

/// <summary>
/// A Protocol command run for a user who is already known (a sign-in step after the password, an account page), whose
/// Identity writes must be attributed to that user rather than to the anonymous browser request. See
/// <see cref="ActAsUserBehavior{TRequest,TResponse}"/>.
/// </summary>
public interface IActsAsUser
{
    Guid TenantId { get; }
    Guid UserId { get; }
}
