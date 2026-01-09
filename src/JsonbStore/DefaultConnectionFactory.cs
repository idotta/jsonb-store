using Microsoft.Data.Sqlite;
using System.Data;

namespace JsonbStore;

/// <summary>
/// Default stateless implementation of <see cref="IConnectionFactory"/>.
/// A single instance can create connections for multiple databases by passing
/// different options to each method.
/// </summary>
public sealed class DefaultConnectionFactory : IConnectionFactory
{
    /// <summary>
    /// Initializes a new instance of DefaultConnectionFactory.
    /// </summary>
    public DefaultConnectionFactory()
    {
    }

    /// <inheritdoc/>
    public SqliteConnection CreateConnection(JsonbStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var connection = new SqliteConnection(options.ConnectionString);
        connection.Open();
        ConfigureConnection(connection, options);
        return connection;
    }

    /// <inheritdoc/>
    public async Task<SqliteConnection> CreateConnectionAsync(JsonbStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var connection = new SqliteConnection(options.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, options).ConfigureAwait(false);
        return connection;
    }

    /// <inheritdoc/>
    public void ConfigureConnection(SqliteConnection connection, JsonbStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        // Configure WAL mode
        if (options.EnableWalMode)
        {
            connection.Execute("PRAGMA journal_mode = WAL;");
        }

        // Configure synchronous mode
        var syncMode = GetSynchronousModeString(options.SynchronousMode);
        connection.Execute($"PRAGMA synchronous = {syncMode};");

        // Configure page size (must be set before any tables are created)
        connection.Execute($"PRAGMA page_size = {options.PageSize};");

        // Configure cache size
        connection.Execute($"PRAGMA cache_size = {options.CacheSize};");

        // Configure busy timeout
        connection.Execute($"PRAGMA busy_timeout = {options.BusyTimeoutMs};");

        // Configure foreign keys
        if (options.EnableForeignKeys)
        {
            connection.Execute("PRAGMA foreign_keys = ON;");
        }

        // Execute additional pragmas
        foreach (var pragma in options.AdditionalPragmas)
        {
            connection.Execute(pragma);
        }
    }

    /// <inheritdoc/>
    public async Task ConfigureConnectionAsync(SqliteConnection connection, JsonbStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync().ConfigureAwait(false);
        }

        // Configure WAL mode
        if (options.EnableWalMode)
        {
            await connection.ExecuteAsync("PRAGMA journal_mode = WAL;").ConfigureAwait(false);
        }

        // Configure synchronous mode
        var syncMode = GetSynchronousModeString(options.SynchronousMode);
        await connection.ExecuteAsync($"PRAGMA synchronous = {syncMode};").ConfigureAwait(false);

        // Configure page size (must be set before any tables are created)
        await connection.ExecuteAsync($"PRAGMA page_size = {options.PageSize};").ConfigureAwait(false);

        // Configure cache size
        await connection.ExecuteAsync($"PRAGMA cache_size = {options.CacheSize};").ConfigureAwait(false);

        // Configure busy timeout
        await connection.ExecuteAsync($"PRAGMA busy_timeout = {options.BusyTimeoutMs};").ConfigureAwait(false);

        // Configure foreign keys
        if (options.EnableForeignKeys)
        {
            await connection.ExecuteAsync("PRAGMA foreign_keys = ON;").ConfigureAwait(false);
        }

        // Execute additional pragmas
        foreach (var pragma in options.AdditionalPragmas)
        {
            await connection.ExecuteAsync(pragma).ConfigureAwait(false);
        }
    }

    private static string GetSynchronousModeString(SynchronousMode mode)
    {
        return mode switch
        {
            SynchronousMode.Off => "OFF",
            SynchronousMode.Normal => "NORMAL",
            SynchronousMode.Full => "FULL",
            _ => "NORMAL"
        };
    }
}

/// <summary>
/// Extension methods for SqliteConnection to execute commands.
/// </summary>
internal static class SqliteConnectionExtensions
{
    public static void Execute(this SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    public static async Task ExecuteAsync(this SqliteConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
