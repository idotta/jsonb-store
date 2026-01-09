using Microsoft.Data.Sqlite;

namespace JsonbStore;

/// <summary>
/// Defines the contract for SQLite connection lifecycle management.
/// Supports different connection strategies including single long-lived connections,
/// pooling, and in-memory databases.
/// </summary>
public interface IConnectionFactory
{
    /// <summary>
    /// Creates and opens a new SQLite connection synchronously.
    /// </summary>
    /// <returns>An open SQLite connection</returns>
    SqliteConnection CreateConnection();

    /// <summary>
    /// Creates and opens a new SQLite connection.
    /// </summary>
    /// <returns>An open SQLite connection</returns>
    Task<SqliteConnection> CreateConnectionAsync();

    /// <summary>
    /// Configures a SQLite connection with optimal performance settings
    /// (e.g., WAL mode, synchronous level, page size, cache size).
    /// </summary>
    /// <param name="connection">The connection to configure</param>
    void ConfigureConnection(SqliteConnection connection);

    /// <summary>
    /// Configures a SQLite connection with optimal performance settings
    /// (e.g., WAL mode, synchronous level, page size, cache size).
    /// </summary>
    /// <param name="connection">The connection to configure</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ConfigureConnectionAsync(SqliteConnection connection);

    /// <summary>
    /// Gets a value indicating whether the factory owns the connection lifecycle.
    /// If true, the factory is responsible for disposing connections.
    /// If false, the caller is responsible for disposal.
    /// </summary>
    bool OwnsConnection { get; }
}
