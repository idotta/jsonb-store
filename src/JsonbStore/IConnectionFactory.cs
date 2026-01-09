using Microsoft.Data.Sqlite;

namespace JsonbStore;

/// <summary>
/// Defines the contract for SQLite connection lifecycle management.
/// This factory is stateless - options are passed to each method, enabling
/// a single factory instance to create connections for multiple databases.
/// </summary>
public interface IConnectionFactory
{
    /// <summary>
    /// Creates and opens a new SQLite connection synchronously.
    /// </summary>
    /// <param name="options">Configuration options for the connection</param>
    /// <returns>An open SQLite connection</returns>
    SqliteConnection CreateConnection(JsonbStoreOptions options);

    /// <summary>
    /// Creates and opens a new SQLite connection.
    /// </summary>
    /// <param name="options">Configuration options for the connection</param>
    /// <returns>An open SQLite connection</returns>
    Task<SqliteConnection> CreateConnectionAsync(JsonbStoreOptions options);

    /// <summary>
    /// Configures a SQLite connection with optimal performance settings
    /// (e.g., WAL mode, synchronous level, page size, cache size).
    /// </summary>
    /// <param name="connection">The connection to configure</param>
    /// <param name="options">Configuration options to apply</param>
    void ConfigureConnection(SqliteConnection connection, JsonbStoreOptions options);

    /// <summary>
    /// Configures a SQLite connection with optimal performance settings
    /// (e.g., WAL mode, synchronous level, page size, cache size).
    /// </summary>
    /// <param name="connection">The connection to configure</param>
    /// <param name="options">Configuration options to apply</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ConfigureConnectionAsync(SqliteConnection connection, JsonbStoreOptions options);
}
