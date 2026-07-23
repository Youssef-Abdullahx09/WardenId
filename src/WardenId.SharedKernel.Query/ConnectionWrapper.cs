using System.Data;
using Dapper;
using Npgsql;

namespace WardenId.SharedKernel.Query;

public sealed class ConnectionWrapper(
    NpgsqlConnection connection) : IConnectionWrapper
{
    public ConnectionState State => connection.State;

    public Task CloseAsync()
    {
        return connection.CloseAsync();
    }

    public async Task OpenAsync(CancellationToken cancellationToken)
    {
        await connection.OpenAsync(cancellationToken);
    }

    public async Task<IEnumerable<TReturn>> QueryAsync<TFirst, TSecond, TReturn>(
        string sql,
        Func<TFirst, TSecond, TReturn> map,
        object? param = null,
        IDbTransaction? transaction = null,
        bool buffered = true,
        string splitOn = "Id",
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open) await OpenAsync(cancellationToken);
        return await connection.QueryAsync(
            sql,
            map,
            param,
            transaction,
            buffered,
            splitOn,
            commandTimeout,
            commandType);
    }

    public async Task<IEnumerable<TReturn>> QueryAsync<
        TFirst,
        TSecond,
        TThird,
        TReturn>(string sql,
        Func<TFirst, TSecond, TThird, TReturn> map,
        object? param = null,
        IDbTransaction? transaction = null,
        bool buffered = true,
        string splitOn = "Id",
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        return await connection.QueryAsync(
            sql,
            map,
            param,
            transaction,
            buffered,
            splitOn,
            commandTimeout,
            commandType);
    }

    public async Task<IEnumerable<TReturn>> QueryAsync<
        TFirst,
        TSecond,
        TThird,
        TForth,
        TReturn>(string sql,
        Func<TFirst, TSecond, TThird, TForth, TReturn> map,
        object? param = null,
        IDbTransaction? transaction = null,
        bool buffered = true,
        string splitOn = "Id",
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        return await connection.QueryAsync(
            sql,
            map,
            param,
            transaction,
            buffered,
            splitOn,
            commandTimeout,
            commandType);
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null,
        IDbTransaction? transaction = null, int? commandTimeout = null, CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        return await connection.QueryAsync<T>(
            sql,
            param,
            transaction,
            commandTimeout,
            commandType);
    }

    public async Task<IEnumerable<TReturn>> QueryAsync<TFirst, TSecond, TThird, TForth, TFifth, TReturn>(string sql,
        Func<TFirst, TSecond, TThird, TForth, TFifth, TReturn> map, object? param = null,
        IDbTransaction? transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null,
        CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        return await connection.QueryAsync(
            sql,
            map,
            param,
            transaction,
            buffered,
            splitOn,
            commandTimeout,
            commandType);
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? param = null,
        IDbTransaction? transaction = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<T>(
            sql,
            param,
            transaction,
            commandTimeout,
            commandType);
    }

    public async Task<SqlMapper.GridReader> QueryMultipleAsync(
        string sql,
        object? param = null,
        IDbTransaction? transaction = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open) await OpenAsync(cancellationToken);
        return await connection.QueryMultipleAsync(
            sql,
            param,
            transaction,
            commandTimeout,
            commandType);
    }
}