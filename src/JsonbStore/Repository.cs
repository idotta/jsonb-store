using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;

namespace JsonbStore;

/// <summary>
/// A high-performance repository for storing JSON objects in a single SQLite file.
/// Uses Dapper for minimal mapping overhead and supports JSON document storage using JSONB format (SQLite 3.45+).
/// </summary>
public class Repository : IRepository
{
    private readonly SqliteConnection _connection;
    private readonly bool _ownsConnection;

    /// <summary>
    /// Initializes a new repository with the specified SQLite database file.
    /// Automatically configures WAL mode and synchronous=NORMAL for optimal performance.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database file</param>
    public Repository(string databasePath)
    {
        _connection = new SqliteConnection($"Data Source={databasePath}");
        _connection.Open();
        _ownsConnection = true;
        ConfigureConnection();
    }

    /// <summary>
    /// Initializes a new repository with an existing SQLite connection.
    /// </summary>
    /// <param name="connection">An open SQLite connection</param>
    public Repository(SqliteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _ownsConnection = false;
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }
        ConfigureConnection();
    }

    /// <summary>
    /// Configures SQLite for optimal performance with WAL mode and synchronous=NORMAL.
    /// </summary>
    private void ConfigureConnection()
    {
        _connection.Execute("PRAGMA journal_mode = WAL;");
        _connection.Execute("PRAGMA synchronous = NORMAL;");
    }

    /// <summary>
    /// Gets the table name from a type.
    /// </summary>
    private static string GetTableName<T>()
    {
        return typeof(T).Name;
    }

    /// <summary>
    /// Creates a table for storing JSON objects with a generic schema using JSONB format.
    /// The table name will be the name of the type T.
    /// </summary>
    /// <typeparam name="T">Type whose name will be used as the table name</typeparam>
    public async Task CreateTableAsync<T>()
    {
        var tableName = GetTableName<T>();
        var sql = $@"
            CREATE TABLE IF NOT EXISTS [{tableName}] (
                id TEXT PRIMARY KEY,
                data BLOB NOT NULL,
                created_at INTEGER NOT NULL DEFAULT (strftime('%s', 'now')),
                updated_at INTEGER NOT NULL DEFAULT (strftime('%s', 'now'))
            )";
        await _connection.ExecuteAsync(sql);
    }

    /// <summary>
    /// Inserts or updates a JSON object in a table named after the type T using JSONB format.
    /// </summary>
    /// <typeparam name="T">Type of the object to store (also used as table name)</typeparam>
    /// <param name="id">Unique identifier for the object</param>
    /// <param name="data">The object to store</param>
    public async Task UpsertAsync<T>(string id, T data)
    {
        var tableName = GetTableName<T>();
        var json = System.Text.Json.JsonSerializer.Serialize(data);

        // Use jsonb() function to convert JSON to JSONB format for storage
        var sql = $@"
            INSERT INTO [{tableName}] (id, data, updated_at)
            VALUES (@Id, jsonb(@Data), strftime('%s', 'now'))
            ON CONFLICT(id) DO UPDATE SET
                data = jsonb(@Data),
                updated_at = strftime('%s', 'now')";

        await _connection.ExecuteAsync(sql, new
        {
            Id = id,
            Data = json
        });
    }

    /// <summary>
    /// Retrieves a JSON object by its ID from a table named after the type T.
    /// </summary>
    /// <typeparam name="T">Type of the object to retrieve (also used as table name)</typeparam>
    /// <param name="id">Unique identifier of the object</param>
    /// <returns>The deserialized object, or default if not found</returns>
    public async Task<T?> GetAsync<T>(string id)
    {
        var tableName = GetTableName<T>();

        // Use json() function to convert JSONB back to JSON string
        var sql = $"SELECT json(data) as data FROM [{tableName}] WHERE id = @Id";
        var json = await _connection.QueryFirstOrDefaultAsync<string>(sql, new { Id = id });

        if (string.IsNullOrEmpty(json))
        {
            return default;
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
    }

    /// <summary>
    /// Retrieves all JSON objects from a table named after the type T.
    /// </summary>
    /// <typeparam name="T">Type of the objects to retrieve (also used as table name)</typeparam>
    /// <returns>An enumerable of deserialized objects</returns>
    public async Task<IEnumerable<T>> GetAllAsync<T>()
    {
        var tableName = GetTableName<T>();

        // Use json() function to convert JSONB back to JSON strings
        var sql = $"SELECT json(data) as data FROM [{tableName}]";
        var jsonResults = await _connection.QueryAsync<string>(sql);

        var results = new List<T>();
        foreach (var json in jsonResults)
        {
            var item = System.Text.Json.JsonSerializer.Deserialize<T>(json);
            if (item != null)
            {
                results.Add(item);
            }
        }
        return results;
    }

    /// <summary>
    /// Deletes a JSON object by its ID from a table named after the type T.
    /// </summary>
    /// <typeparam name="T">Type whose name will be used as the table name</typeparam>
    /// <param name="id">Unique identifier of the object to delete</param>
    /// <returns>True if the object was deleted, false if it didn't exist</returns>
    public async Task<bool> DeleteAsync<T>(string id)
    {
        var tableName = GetTableName<T>();
        var sql = $"DELETE FROM [{tableName}] WHERE id = @Id";
        var affectedRows = await _connection.ExecuteAsync(sql, new { Id = id });
        return affectedRows > 0;
    }

    /// <summary>
    /// Executes a batch of operations within a transaction for optimal performance.
    /// </summary>
    /// <param name="action">Async action to execute within the transaction</param>
    public async Task ExecuteInTransactionAsync(Func<IDbTransaction, Task> action)
    {
        await ExecuteInTransactionCoreAsync(action);
    }

    /// <summary>
    /// Executes a batch of operations within a transaction for optimal performance.
    /// </summary>
    /// <param name="action">Async action to execute within the transaction</param>
    public async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        await ExecuteInTransactionCoreAsync(_ => action());
    }

    /// <summary>
    /// Core transaction execution logic.
    /// </summary>
    private async Task ExecuteInTransactionCoreAsync(Func<IDbTransaction, Task> action)
    {
        using var transaction = _connection.BeginTransaction();
        try
        {
            await action(transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Gets the underlying SQLite connection for advanced operations.
    /// </summary>
    public SqliteConnection Connection => _connection;

    /// <summary>
    /// Disposes the repository and closes the database connection if owned.
    /// </summary>
    public void Dispose()
    {
        if (_ownsConnection)
        {
            _connection?.Dispose();
        }
    }

    /// <summary>
    /// Asynchronously disposes the repository and closes the database connection if owned.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_ownsConnection)
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
        }
    }
}
