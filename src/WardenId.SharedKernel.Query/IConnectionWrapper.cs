using System.Data;
using Dapper;

namespace WardenId.SharedKernel.Query;

public interface IConnectionWrapper
{
    ConnectionState State { get; }
    Task OpenAsync(CancellationToken cancellationToken);
    Task CloseAsync();

    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null,
        int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    Task<IEnumerable<TReturn>> QueryAsync<TFirst, TSecond, TReturn>(string sql, Func<TFirst, TSecond, TReturn> map,
        object? param = null, IDbTransaction? transaction = null, bool buffered = true, string splitOn = "Id",
        int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default);

    Task<IEnumerable<TReturn>> QueryAsync<TFirst, TSecond, TThird, TReturn>(string sql,
        Func<TFirst, TSecond, TThird, TReturn> map,
        object? param = null,
        IDbTransaction? transaction = null,
        bool buffered = true,
        string splitOn = "Id",
        int? commandTimeout = null,
        CommandType? commandType = null, CancellationToken cancellationToken = default);

    Task<IEnumerable<TReturn>> QueryAsync<TFirst, TSecond, TThird, TForth, TReturn>(string sql,
        Func<TFirst, TSecond, TThird, TForth, TReturn> map,
        object? param = null,
        IDbTransaction? transaction = null,
        bool buffered = true,
        string splitOn = "Id",
        int? commandTimeout = null,
        CommandType? commandType = null, CancellationToken cancellationToken = default);

    Task<IEnumerable<TReturn>> QueryAsync<TFirst, TSecond, TThird, TForth, TFifth, TReturn>(string sql,
        Func<TFirst, TSecond, TThird, TForth, TFifth, TReturn> map,
        object? param = null,
        IDbTransaction? transaction = null,
        bool buffered = true,
        string splitOn = "Id",
        int? commandTimeout = null,
        CommandType? commandType = null, CancellationToken cancellationToken = default);

    Task<T?> QueryFirstOrDefaultAsync<T>(string sql,
        object? param = null,
        IDbTransaction? transaction = null,
        int? commandTimeout = null,
        CommandType? commandType = null, CancellationToken cancellationToken = default);

    Task<SqlMapper.GridReader> QueryMultipleAsync(
        string sql,
        object? param = null,
        IDbTransaction? transaction = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default);
}