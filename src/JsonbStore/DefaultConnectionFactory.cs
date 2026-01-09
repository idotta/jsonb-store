using Microsoft.Data.Sqlite;
using System.Data;

namespace JsonbStore;

/// <summary>
/// Default implementation of <see cref="IConnectionFactory"/> using connection strings.
/// </summary>
public class DefaultConnectionFactory : IConnectionFactory
{
    private readonly JsonbStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of DefaultConnectionFactory.
    /// </summary>
    /// <param name="options">The JsonbStore configuration options</param>
    public DefaultConnectionFactory(JsonbStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public bool OwnsConnection => true;

    /// <inheritdoc/>
    public async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();
        await ConfigureConnectionAsync(connection);
        return connection;
    }

    /// <inheritdoc/>
    public async Task ConfigureConnectionAsync(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        // Configure WAL mode
        if (_options.EnableWalMode)
        {
            await connection.ExecuteAsync("PRAGMA journal_mode = WAL;");
        }

        // Configure synchronous mode
        var syncMode = _options.SynchronousMode switch
        {
            SynchronousMode.Off => "OFF",
            SynchronousMode.Normal => "NORMAL",
            SynchronousMode.Full => "FULL",
            _ => "NORMAL"
        };
        await connection.ExecuteAsync($"PRAGMA synchronous = {syncMode};");

        // Configure page size (must be set before any tables are created)
        await connection.ExecuteAsync($"PRAGMA page_size = {_options.PageSize};");

        // Configure cache size
        await connection.ExecuteAsync($"PRAGMA cache_size = {_options.CacheSize};");

        // Configure busy timeout
        await connection.ExecuteAsync($"PRAGMA busy_timeout = {_options.BusyTimeoutMs};");

        // Configure foreign keys
        if (_options.EnableForeignKeys)
        {
            await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
        }

        // Execute additional pragmas
        foreach (var pragma in _options.AdditionalPragmas)
        {
            await connection.ExecuteAsync(pragma);
        }
    }
}

/// <summary>
/// Extension methods for SqliteConnection to execute commands.
/// </summary>
internal static class SqliteConnectionExtensions
{
    public static async Task ExecuteAsync(this SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}
