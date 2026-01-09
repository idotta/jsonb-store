using System.Data;
using Microsoft.Data.Sqlite;

namespace LiteDocumentStore;

/// <summary>
/// Defines the contract for a document store that provides JSON document storage
/// with full relational database capabilities. Supports multiple entity types
/// through generic methods. Implements disposal interfaces for proper resource cleanup.
/// </summary>
public interface IDocumentStore : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Creates a table for storing JSON objects with a generic schema using JSONB format.
    /// The table name will be derived from the type T.
    /// </summary>
    /// <typeparam name="T">Type whose name will be used as the table name</typeparam>
    /// <returns>A task representing the asynchronous operation</returns>
    Task CreateTableAsync<T>();

    /// <summary>
    /// Inserts or updates a JSON object in the document store using JSONB format.
    /// </summary>
    /// <typeparam name="T">Type of the object to store (also used as table name)</typeparam>
    /// <param name="id">Unique identifier for the object</param>
    /// <param name="data">The object to store</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task UpsertAsync<T>(string id, T data);

    /// <summary>
    /// Retrieves a JSON object by its ID from the document store.
    /// </summary>
    /// <typeparam name="T">Type of the object to retrieve (also used as table name)</typeparam>
    /// <param name="id">Unique identifier of the object</param>
    /// <returns>The deserialized object, or default if not found</returns>
    Task<T?> GetAsync<T>(string id);

    /// <summary>
    /// Retrieves all JSON objects from the document store.
    /// </summary>
    /// <typeparam name="T">Type of the objects to retrieve (also used as table name)</typeparam>
    /// <returns>An enumerable of deserialized objects</returns>
    Task<IEnumerable<T>> GetAllAsync<T>();

    /// <summary>
    /// Deletes a JSON object by its ID from the document store.
    /// </summary>
    /// <typeparam name="T">Type whose name will be used as the table name</typeparam>
    /// <param name="id">Unique identifier of the object to delete</param>
    /// <returns>True if the object was deleted, false if it didn't exist</returns>
    Task<bool> DeleteAsync<T>(string id);

    /// <summary>
    /// Executes a batch of operations within a transaction for optimal performance.
    /// </summary>
    /// <param name="action">Async action to execute within the transaction</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ExecuteInTransactionAsync(Func<IDbTransaction, Task> action);

    /// <summary>
    /// Executes a batch of operations within a transaction for optimal performance.
    /// </summary>
    /// <param name="action">Async action to execute within the transaction</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task ExecuteInTransactionAsync(Func<Task> action);

    /// <summary>
    /// Checks if the document store is healthy and ready for operations.
    /// Verifies the connection is open and validates SQLite version supports JSONB (3.45+).
    /// Useful for liveness probes in containerized environments and health monitoring.
    /// </summary>
    /// <returns>True if the store is healthy, false otherwise</returns>
    Task<bool> IsHealthyAsync();

    /// <summary>
    /// Creates an index on a JSON path expression for optimized query performance.
    /// Automatically checks if the index exists before creation to avoid errors.
    /// </summary>
    /// <typeparam name="T">Type whose table will have the index created</typeparam>
    /// <param name="jsonPath">Expression selecting the JSON property to index</param>
    /// <param name="indexName">Optional custom index name. If null, a name will be auto-generated</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task CreateIndexAsync<T>(System.Linq.Expressions.Expression<Func<T, object>> jsonPath, string? indexName = null);

    /// <summary>
    /// Creates a composite index on multiple JSON path expressions for optimized multi-column queries.
    /// Automatically checks if the index exists before creation to avoid errors.
    /// </summary>
    /// <typeparam name="T">Type whose table will have the index created</typeparam>
    /// <param name="jsonPaths">Array of expressions selecting the JSON properties to index</param>
    /// <param name="indexName">Optional custom index name. If null, a name will be auto-generated</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task CreateCompositeIndexAsync<T>(System.Linq.Expressions.Expression<Func<T, object>>[] jsonPaths, string? indexName = null);

    /// <summary>
    /// Gets the underlying SQLite connection for advanced operations and raw SQL access.
    /// This enables the hybrid experience where users can use both document storage
    /// and traditional relational database features.
    /// </summary>
    SqliteConnection Connection { get; }
}
