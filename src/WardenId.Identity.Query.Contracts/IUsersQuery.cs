using FluentResults;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Identity.Query.Contracts;

/// <summary>Read-side (Dapper) queries over the Identity schema's users.</summary>
public interface IUsersQuery
{
    /// <summary>The user with its attributes and MFA factors; null when there's no such live (not soft-deleted) user.</summary>
    Task<Result<Features.Users.Get.Item?>> Get(
        Features.Users.Get.Query query,
        CancellationToken cancellationToken);

    Task<Result<PaginationListResponse<Features.Users.List.Item>>> List(
        Features.Users.List.Query query,
        CancellationToken cancellationToken);

    /// <summary>The user's own account view; null when there's no such live (not soft-deleted) user.</summary>
    Task<Result<Features.Users.GetAccount.Item?>> GetAccount(
        Features.Users.GetAccount.Query query,
        CancellationToken cancellationToken);

    /// <summary>What sign-in needs of a user; null when there's no such live (not soft-deleted) user.</summary>
    Task<Result<Features.Users.GetForSignIn.Item?>> GetForSignIn(
        Features.Users.GetForSignIn.Query query,
        CancellationToken cancellationToken);
}
