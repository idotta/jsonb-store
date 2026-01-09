using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;

namespace JsonbStore;

/// <summary>
/// A high-performance document store for storing JSON objects in SQLite.
/// Uses Dapper for minimal mapping overhead and supports JSON document storage using JSONB format (SQLite 3.45+).
/// Can optionally own and manage the lifecycle of its SqliteConnection.
/// </summary>
public sealed class DocumentStore : IDocumentStore
{
    private readonly SqliteConnection _connection;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly ITableNamingConvention _tableNamingConvention;
    private readonly ILogger<DocumentStore> _logger;
    private readonly bool _ownsConnection;
    private bool _disposed;

    /// <summary>
    /// Initializes a new document store with the specified connection and dependencies.
    /// </summary>
    /// <param name="connection">The open SQLite connection</param>
    /// <param name="jsonSerializer">JSON serializer implementation (defaults to SystemTextJsonSerializer)</param>
    /// <param name="tableNamingConvention">Table naming convention (defaults to DefaultTableNamingConvention)</param>
    /// <param name="logger">Logger for diagnostics (optional)</param>
    /// <param name="ownsConnection">Whether this store owns and should dispose the connection (default: false)</param>
    public DocumentStore(
        SqliteConnection connection,
        IJsonSerializer? jsonSerializer = null,
        ITableNamingConvention? tableNamingConvention = null,
        ILogger<DocumentStore>? logger = null,
        bool ownsConnection = false)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _jsonSerializer = jsonSerializer ?? new SystemTextJsonSerializer();
        _tableNamingConvention = tableNamingConvention ?? new DefaultTableNamingConvention();
        _logger = logger ?? NullLogger<DocumentStore>.Instance;
        _ownsConnection = ownsConnection;
    }

    /// <summary>
    /// Gets a value indicating whether this store owns and manages the connection lifecycle.
    /// </summary>
    public bool OwnsConnection => _ownsConnection;

    /// <summary>
    /// Gets the underlying SQLite connection for advanced operations.
    /// </summary>
    public SqliteConnection Connection
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _connection;
        }
    }

    /// <summary>
    /// Creates a table for storing JSON objects with a generic schema using JSONB format.
    /// The table name will be the name of the type T.
    /// </summary>
    /// <typeparam name="T">Type whose name will be used as the table name</typeparam>
    public async Task CreateTableAsync<T>()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tableName = _tableNamingConvention.GetTableName<T>();
        var sql = SqlGenerator.GenerateCreateTableSql(tableName);

        _logger.LogDebug("Creating table {TableName} for type {TypeName}", tableName, typeof(T).Name);
        await _connection.ExecuteAsync(sql);
        _logger.LogInformation("Table {TableName} created successfully", tableName);
    }

    /// <summary>
    /// Inserts or updates a JSON object in a table named after the type T using JSONB format.
    /// </summary>
    /// <typeparam name="T">Type of the object to store (also used as table name)</typeparam>
    /// <param name="id">Unique identifier for the object</param>
    /// <param name="data">The object to store</param>
    public async Task UpsertAsync<T>(string id, T data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("ID cannot be null or empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(data);

        var tableName = _tableNamingConvention.GetTableName<T>();
        var json = _jsonSerializer.Serialize(data);
        var sql = SqlGenerator.GenerateUpsertSql(tableName);

        _logger.LogDebug("Upserting document {Id} into table {TableName}", id, tableName);

        await _connection.ExecuteAsync(sql, new
        {
            Id = id,
            Data = json
        });

        _logger.LogDebug("Document {Id} upserted successfully in table {TableName}", id, tableName);
    }

    /// <summary>
    /// Retrieves a JSON object by its ID from a table named after the type T.
    /// </summary>
    /// <typeparam name="T">Type of the object to retrieve (also used as table name)</typeparam>
    /// <param name="id">Unique identifier of the object</param>
    /// <returns>The deserialized object, or default if not found</returns>
    public async Task<T?> GetAsync<T>(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("ID cannot be null or empty.", nameof(id));
        }

        var tableName = _tableNamingConvention.GetTableName<T>();
        var sql = SqlGenerator.GenerateGetByIdSql(tableName);

        _logger.LogDebug("Retrieving document {Id} from table {TableName}", id, tableName);

        var json = await _connection.QueryFirstOrDefaultAsync<string>(sql, new { Id = id });

        if (string.IsNullOrEmpty(json))
        {
            _logger.LogDebug("Document {Id} not found in table {TableName}", id, tableName);
            return default;
        }

        var result = _jsonSerializer.Deserialize<T>(json);
        _logger.LogDebug("Document {Id} retrieved successfully from table {TableName}", id, tableName);
        return result;
    }

    /// <summary>
    /// Retrieves all JSON objects from a table named after the type T.
    /// </summary>
    /// <typeparam name="T">Type of the objects to retrieve (also used as table name)</typeparam>
    /// <returns>An enumerable of deserialized objects</returns>
    public async Task<IEnumerable<T>> GetAllAsync<T>()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tableName = _tableNamingConvention.GetTableName<T>();
        var sql = SqlGenerator.GenerateGetAllSql(tableName);

        _logger.LogDebug("Retrieving all documents from table {TableName}", tableName);

        var jsonResults = await _connection.QueryAsync<string>(sql);

        var results = new List<T>();
        foreach (var json in jsonResults)
        {
            var item = _jsonSerializer.Deserialize<T>(json);
            if (item != null)
            {
                results.Add(item);
            }
        }

        _logger.LogDebug("Retrieved {Count} documents from table {TableName}", results.Count, tableName);
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
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("ID cannot be null or empty.", nameof(id));
        }

        var tableName = _tableNamingConvention.GetTableName<T>();
        var sql = SqlGenerator.GenerateDeleteSql(tableName);

        _logger.LogDebug("Deleting document {Id} from table {TableName}", id, tableName);

        var affectedRows = await _connection.ExecuteAsync(sql, new { Id = id });
        var deleted = affectedRows > 0;

        if (deleted)
        {
            _logger.LogInformation("Document {Id} deleted from table {TableName}", id, tableName);
        }
        else
        {
            _logger.LogDebug("Document {Id} not found in table {TableName} (nothing to delete)", id, tableName);
        }

        return deleted;
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
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Use existing transaction if any?
        // _connection.BeginTransaction() requires the connection to be open.
        // It throws if a transaction is already active on this connection (SQLite supports one transaction per connection unless using Savepoints).
        // Since we don't control the connection, we should check if we can start a transaction.
        // However, standard ADO.NET SqliteConnection.BeginTransaction() will fail if currently in a transaction.
        // For now, naive implementation: try to begin.
        // Ideally we should support nested transactions or check, but simpler first.
        
        using var transaction = _connection.BeginTransaction();
        try
        {
            await action(transaction).ConfigureAwait(false);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Disposes the document store and, if owned, the underlying connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsConnection)
        {
            _logger.LogDebug("Disposing owned connection");
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes the document store and, if owned, the underlying connection.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsConnection)
        {
            _logger.LogDebug("Disposing owned connection");
            _connection.Dispose();
        }
    }
}
