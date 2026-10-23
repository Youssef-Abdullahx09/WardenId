using System.Data;
using Dapper;
using FluentResults;
using WardenId.Identity.Query.Contracts;
using WardenId.SharedKernel.FileOperation;
using WardenId.SharedKernel.Query;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Identity.Query;

public sealed class UsersQuery(
    IConnectionWrapper connectionWrapper,
    IReadFileService readFileService) : IUsersQuery
{
    private const string GetSql = "/Features/Users/Get/Query.sql";
    private const string ListSql = "/Features/Users/List/Query.sql";
    private const string GetAccountSql = "/Features/Users/GetAccount/Query.sql";
    private const string GetForSignInSql = "/Features/Users/GetForSignIn/Query.sql";
    private const string OffsetParameter = "Offset";
    private const string SearchPatternParameter = "SearchPattern";
    private const string DescendingParameter = "Descending";

    // LIKE escaping for the search prefix; Query.sql declares ESCAPE '\'.
    private const string LikeEscape = "\\";
    private const string LikeAnyCharacters = "%";
    private const string LikeOneCharacter = "_";

    public async Task<Result<Contracts.Features.Users.Get.Item?>> Get(
        Contracts.Features.Users.Get.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(GetSql, cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        await using var grid = await connectionWrapper.QueryMultipleAsync(
            readingFileResult.Value,
            param: UserParameters(query.TenantId, query.UserId),
            cancellationToken: cancellationToken);
        var user = await grid.ReadSingleOrDefaultAsync<Contracts.Features.Users.Get.Item>();
        var attributes = await grid.ReadAsync<Contracts.Features.Users.Get.UserAttribute>();
        var factors = await grid.ReadAsync<Contracts.Features.Users.Get.MfaFactor>();
        if (user is null) return Result.Ok<Contracts.Features.Users.Get.Item?>(null);

        user.Attributes = attributes.ToArray();
        user.MfaFactors = factors.ToArray();
        return Result.Ok<Contracts.Features.Users.Get.Item?>(user);
    }

    public async Task<Result<PaginationListResponse<Contracts.Features.Users.List.Item>>> List(
        Contracts.Features.Users.List.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(ListSql, cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(query.TenantId), query.TenantId);
        queryParameters.Add(nameof(query.IncludeDeleted), query.IncludeDeleted);
        // Typed explicitly: an untyped NULL leaves Postgres unable to infer "@Status IS NULL".
        queryParameters.Add(nameof(query.Status), query.Status, DbType.String);
        queryParameters.Add(
            SearchPatternParameter,
            query.SearchPrefix is null ? null : LikePrefixPattern(query.SearchPrefix),
            DbType.String);
        queryParameters.Add(nameof(query.SortField), query.SortField.ToString(), DbType.String);
        queryParameters.Add(DescendingParameter, query.SortDirection == SortDirection.Desc);
        queryParameters.Add(nameof(query.PageSize), query.PageSize);
        queryParameters.Add(OffsetParameter, (query.PageNumber - 1) * query.PageSize);

        await using var grid = await connectionWrapper.QueryMultipleAsync(
            readingFileResult.Value,
            param: queryParameters,
            cancellationToken: cancellationToken);
        var total = await grid.ReadSingleAsync<long>();
        var users = (await grid.ReadAsync<Contracts.Features.Users.List.Item>()).ToList();

        return new PaginationListResponse<Contracts.Features.Users.List.Item>(total, users);
    }

    public async Task<Result<Contracts.Features.Users.GetAccount.Item?>> GetAccount(
        Contracts.Features.Users.GetAccount.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(GetAccountSql, cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        await using var grid = await connectionWrapper.QueryMultipleAsync(
            readingFileResult.Value,
            param: UserParameters(query.TenantId, query.UserId),
            cancellationToken: cancellationToken);
        var account = await grid.ReadSingleOrDefaultAsync<Contracts.Features.Users.GetAccount.Item>();
        var factors = await grid.ReadAsync<Contracts.Features.Users.GetAccount.Factor>();
        if (account is null) return Result.Ok<Contracts.Features.Users.GetAccount.Item?>(null);

        account.EnabledFactors = factors.ToArray();
        return Result.Ok<Contracts.Features.Users.GetAccount.Item?>(account);
    }

    public async Task<Result<Contracts.Features.Users.GetForSignIn.Item?>> GetForSignIn(
        Contracts.Features.Users.GetForSignIn.Query query,
        CancellationToken cancellationToken)
    {
        var readingFileResult = await readFileService.ReadFileContentAsync(GetForSignInSql, cancellationToken);
        if (readingFileResult.IsFailed) return Result.Fail(readingFileResult.Errors);

        var user = await connectionWrapper.QueryFirstOrDefaultAsync<Contracts.Features.Users.GetForSignIn.Item>(
            readingFileResult.Value,
            param: UserParameters(query.TenantId, query.UserId),
            cancellationToken: cancellationToken);

        return Result.Ok(user);
    }

    private static DynamicParameters UserParameters(Guid tenantId, Guid userId)
    {
        var queryParameters = new DynamicParameters();
        queryParameters.Add(nameof(Contracts.Features.Users.Get.Query.TenantId), tenantId);
        queryParameters.Add(nameof(Contracts.Features.Users.Get.Query.UserId), userId);
        return queryParameters;
    }

    // The prefix matches literally: its own escape character and wildcards are escaped, then "any characters" follows.
    private static string LikePrefixPattern(string prefix) =>
        prefix
            .Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace(LikeAnyCharacters, LikeEscape + LikeAnyCharacters)
            .Replace(LikeOneCharacter, LikeEscape + LikeOneCharacter)
        + LikeAnyCharacters;
}
