using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiteDocumentStore;

/// <summary>
/// A high-performance document store for storing JSON objects in SQLite.
/// Uses Dapper for minimal mapping overhead and supports JSON document storage using JSONB format (SQLite 3.45+).
/// Can optionally own and manage the lifecycle of its SqliteConnection.
/// </summary>
internal sealed class DocumentStore : IDocumentStore
{
    private readonly SqliteConnection _connection;
    private readonly ITableNamingConvention _tableNamingConvention;
    private readonly ILogger<DocumentStore> _logger;
    private readonly VirtualColumnCache _virtualColumnCache;
    private readonly bool _ownsConnection;
    private bool _disposed;

    /// <summary>
    /// Initializes a new document store with the specified connection and dependencies.
    /// </summary>
    /// <param name="connection">The open SQLite connection</param>
    /// <param name="tableNamingConvention">Table naming convention (defaults to DefaultTableNamingConvention)</param>
    /// <param name="logger">Logger for diagnostics (optional)</param>
    /// <param name="ownsConnection">Whether this store owns and should dispose the connection (default: false)</param>
    public DocumentStore(
        SqliteConnection connection,
        ITableNamingConvention? tableNamingConvention = null,
        ILogger<DocumentStore>? logger = null,
        bool ownsConnection = false)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _tableNamingConvention = tableNamingConvention ?? new DefaultTableNamingConvention();
        _logger = logger ?? NullLogger<DocumentStore>.Instance;
        _virtualColumnCache = new VirtualColumnCache(connection);
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
    /// Ensures the connection is in an open state before performing database operations.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the connection is not open.</exception>
    private void EnsureConnectionOpen()
    {
        if (_connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException(
                $"Connection is not open. Current state: {_connection.State}. " +
                "Please ensure the connection is opened before using the DocumentStore.");
        }
    }

    /// <inheritdoc />
    public async Task CreateTableAsync<T>()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        var tableName = _tableNamingConvention.GetTableName<T>();
        var sql = SqlGenerator.GenerateCreateTableSql(tableName);

        _logger.LogDebug("Creating table {TableName} for type {TypeName}", tableName, typeof(T).Name);
        await _connection.ExecuteAsync(sql);
        _logger.LogInformation("Table {TableName} created successfully", tableName);
    }

    /// <inheritdoc />
    public async Task<int> UpsertAsync<T>(string id, T data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("ID cannot be null or empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(data);

        var tableName = _tableNamingConvention.GetTableName<T>();
        var jsonBytes = JsonHelper.SerializeToUtf8Bytes(data);
        var sql = SqlGenerator.GenerateUpsertSql(tableName);

        _logger.LogDebug("Upserting document {Id} into table {TableName}", id, tableName);

        var affectedRows = await _connection.ExecuteAsync(sql, new
        {
            Id = id,
            Data = jsonBytes
        });

        _logger.LogDebug("Document {Id} upserted successfully in table {TableName}", id, tableName);

        return affectedRows;
    }

    /// <inheritdoc />
    public async Task<int> UpsertManyAsync<T>(IEnumerable<(string id, T data)> items)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        ArgumentNullException.ThrowIfNull(items);

        var itemsList = items.ToList();
        if (itemsList.Count == 0)
        {
            _logger.LogDebug("UpsertManyAsync called with empty collection, skipping");
            return 0;
        }

        var tableName = _tableNamingConvention.GetTableName<T>();
        var sql = SqlGenerator.GenerateBulkUpsertSql(tableName, itemsList.Count);

        _logger.LogDebug("Bulk upserting {Count} documents into table {TableName}", itemsList.Count, tableName);

        // Build dynamic parameters object
        var parameters = new DynamicParameters();
        for (int i = 0; i < itemsList.Count; i++)
        {
            // Validate all items
            if (string.IsNullOrWhiteSpace(itemsList[i].id))
            {
                throw new ArgumentException($"ID at index {i} cannot be null or empty.", nameof(items));
            }
            if (itemsList[i].data == null)
            {
                throw new ArgumentException($"Data at index {i} cannot be null.", nameof(items));
            }

            var (id, data) = itemsList[i];
            var jsonBytes = JsonHelper.SerializeToUtf8Bytes(data);
            parameters.Add($"Id{i}", id);
            parameters.Add($"Data{i}", jsonBytes);
        }

        var affectedRows = await _connection.ExecuteAsync(sql, parameters);

        _logger.LogInformation("Bulk upserted {Count} documents into table {TableName}, affected rows: {AffectedRows}",
            itemsList.Count, tableName, affectedRows);

        return affectedRows;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

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

        var result = JsonHelper.Deserialize<T>(json);
        _logger.LogDebug("Document {Id} retrieved successfully from table {TableName}", id, tableName);
        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> GetAllAsync<T>()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        var tableName = _tableNamingConvention.GetTableName<T>();
        var sql = SqlGenerator.GenerateGetAllSql(tableName);

        _logger.LogDebug("Retrieving all documents from table {TableName}", tableName);

        var jsonResults = await _connection.QueryAsync<string>(sql);

        var results = new List<T>();
        foreach (var json in jsonResults)
        {
            var item = JsonHelper.Deserialize<T>(json);
            if (item != null)
            {
                results.Add(item);
            }
        }

        _logger.LogDebug("Retrieved {Count} documents from table {TableName}", results.Count, tableName);
        return results;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync<T>(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

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

    /// <inheritdoc />
    public async Task<int> DeleteManyAsync<T>(IEnumerable<string> ids)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        ArgumentNullException.ThrowIfNull(ids);

        var idsList = ids.ToList();
        if (idsList.Count == 0)
        {
            _logger.LogDebug("DeleteManyAsync called with empty collection, skipping");
            return 0;
        }

        // Validate all IDs
        for (int i = 0; i < idsList.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(idsList[i]))
            {
                throw new ArgumentException($"ID at index {i} cannot be null or empty.", nameof(ids));
            }
        }

        var tableName = _tableNamingConvention.GetTableName<T>();
        var sql = SqlGenerator.GenerateBulkDeleteSql(tableName, idsList.Count);

        _logger.LogDebug("Bulk deleting {Count} documents from table {TableName}", idsList.Count, tableName);

        // Build dynamic parameters object
        var parameters = new DynamicParameters();
        for (int i = 0; i < idsList.Count; i++)
        {
            parameters.Add($"Id{i}", idsList[i]);
        }

        var affectedRows = await _connection.ExecuteAsync(sql, parameters);

        _logger.LogInformation("Bulk deleted {Count} documents from table {TableName}, affected rows: {AffectedRows}",
            idsList.Count, tableName, affectedRows);

        return affectedRows;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync<T>(string id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("ID cannot be null or empty.", nameof(id));
        }

        var tableName = _tableNamingConvention.GetTableName<T>();
        var sql = SqlGenerator.GenerateExistsSql(tableName);

        _logger.LogDebug("Checking existence of document {Id} in table {TableName}", id, tableName);

        var exists = await _connection.ExecuteScalarAsync<bool>(sql, new { Id = id });

        _logger.LogDebug("Document {Id} exists in table {TableName}: {Exists}", id, tableName, exists);

        return exists;
    }

    /// <inheritdoc />
    public async Task<long> CountAsync<T>()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        var tableName = _tableNamingConvention.GetTableName<T>();
        var sql = SqlGenerator.GenerateCountSql(tableName);

        _logger.LogDebug("Counting documents in table {TableName}", tableName);

        var count = await _connection.ExecuteScalarAsync<long>(sql);

        _logger.LogDebug("Table {TableName} contains {Count} documents", tableName, count);

        return count;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> QueryAsync<T, TValue>(string jsonPath, TValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            throw new ArgumentException("JSON path cannot be null or empty.", nameof(jsonPath));
        }

        ArgumentNullException.ThrowIfNull(value);

        var tableName = _tableNamingConvention.GetTableName<T>();
        var sql = SqlGenerator.GenerateQueryByJsonPathSql(tableName, jsonPath);

        _logger.LogDebug("Querying table {TableName} by JSON path {JsonPath} with value {Value}",
            tableName, jsonPath, value);

        var jsonResults = await _connection.QueryAsync<string>(sql, new { Value = value }).ConfigureAwait(false);
        var documents = jsonResults
            .Select(json => JsonHelper.Deserialize<T>(json))
            .Where(doc => doc != null)
            .Select(doc => doc!)
            .ToList();

        _logger.LogDebug("Query returned {Count} documents from table {TableName}", documents.Count, tableName);

        return documents;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> QueryAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        ArgumentNullException.ThrowIfNull(predicate);

        var tableName = _tableNamingConvention.GetTableName<T>();

        // Get virtual columns for this table to enable index usage
        var virtualColumns = await _virtualColumnCache.GetAsync(tableName).ConfigureAwait(false);

        // Translate the expression to SQL WHERE clause
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, virtualColumns);
        var sql = SqlGenerator.GenerateQueryWithWhereSql(tableName, whereClause);

        _logger.LogDebug("Querying table {TableName} with WHERE clause: {WhereClause}", tableName, whereClause);

        var jsonResults = await _connection.QueryAsync<string>(sql, parameters).ConfigureAwait(false);
        var documents = jsonResults
            .Select(json => JsonHelper.Deserialize<T>(json))
            .Where(doc => doc != null)
            .Select(doc => doc!)
            .ToList();

        _logger.LogDebug("Query returned {Count} documents from table {TableName}", documents.Count, tableName);

        return documents;
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(Func<IDbTransaction, Task> action)
    {
        await ExecuteInTransactionCoreAsync(action);
    }

    /// <inheritdoc />
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
        EnsureConnectionOpen();

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

    /// <inheritdoc />
    public async Task CreateIndexAsync<T>(System.Linq.Expressions.Expression<Func<T, object>> jsonPath, string? indexName = null)
    {
        ArgumentNullException.ThrowIfNull(jsonPath);
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        var tableName = _tableNamingConvention.GetTableName<T>();
        var pathString = ExtractJsonPath(jsonPath);
        var finalIndexName = indexName ?? GenerateIndexName(tableName, pathString);

        _logger.LogDebug("Creating index {IndexName} on table {TableName} for path {JsonPath}",
            finalIndexName, tableName, pathString);

        // Check if index already exists
        var indexExists = await _connection.QueryFirstOrDefaultAsync<int>(
            SqlGenerator.GenerateCheckIndexExistsSql(),
            new { IndexName = finalIndexName }).ConfigureAwait(false);

        if (indexExists > 0)
        {
            _logger.LogDebug("Index {IndexName} already exists, skipping creation", finalIndexName);
            return;
        }

        var sql = SqlGenerator.GenerateCreateJsonIndexSql(tableName, finalIndexName, pathString);
        await _connection.ExecuteAsync(sql).ConfigureAwait(false);

        _logger.LogInformation("Index {IndexName} created successfully on table {TableName} for path {JsonPath}",
            finalIndexName, tableName, pathString);
    }

    /// <inheritdoc />
    public async Task CreateCompositeIndexAsync<T>(System.Linq.Expressions.Expression<Func<T, object>>[] jsonPaths, string? indexName = null)
    {
        ArgumentNullException.ThrowIfNull(jsonPaths);
        if (jsonPaths.Length == 0)
        {
            throw new ArgumentException("At least one JSON path is required for composite index.", nameof(jsonPaths));
        }
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        var tableName = _tableNamingConvention.GetTableName<T>();
        var pathStrings = jsonPaths.Select(ExtractJsonPath);
        var finalIndexName = indexName ?? GenerateCompositeIndexName(tableName, pathStrings);

        _logger.LogDebug("Creating composite index {IndexName} on table {TableName} for paths [{JsonPaths}]",
            finalIndexName, tableName, string.Join(", ", pathStrings));

        // Check if index already exists
        var indexExists = await _connection.QueryFirstOrDefaultAsync<int>(
            SqlGenerator.GenerateCheckIndexExistsSql(),
            new { IndexName = finalIndexName }).ConfigureAwait(false);

        if (indexExists > 0)
        {
            _logger.LogDebug("Composite index {IndexName} already exists, skipping creation", finalIndexName);
            return;
        }

        var sql = SqlGenerator.GenerateCreateCompositeJsonIndexSql(tableName, finalIndexName, pathStrings);
        await _connection.ExecuteAsync(sql).ConfigureAwait(false);

        _logger.LogInformation("Composite index {IndexName} created successfully on table {TableName} for paths [{JsonPaths}]",
            finalIndexName, tableName, string.Join(", ", pathStrings));
    }

    /// <summary>
    /// Extracts the JSON path from a lambda expression.
    /// Supports simple property access (e.g., x => x.Email) and nested properties (e.g., x => x.Address.City).
    /// Uses property names as-is to match the default System.Text.Json serialization (PascalCase).
    /// </summary>
    private static string ExtractJsonPath<T>(System.Linq.Expressions.Expression<Func<T, object>> expression)
    {
        var body = expression.Body;

        // Handle convert expressions (when boxing value types to object)
        if (body is System.Linq.Expressions.UnaryExpression unary && unary.NodeType == System.Linq.Expressions.ExpressionType.Convert)
        {
            body = unary.Operand;
        }

        var members = new List<string>();
        var current = body;

        while (current is System.Linq.Expressions.MemberExpression memberExpr)
        {
            members.Insert(0, memberExpr.Member.Name);
            current = memberExpr.Expression;
        }

        if (members.Count == 0)
        {
            throw new ArgumentException(
                "Expression must be a property access (e.g., x => x.Email or x => x.Address.City).",
                nameof(expression));
        }

        // Use property names as-is to match default System.Text.Json serialization (PascalCase)
        var jsonPath = "$." + string.Join(".", members);
        return jsonPath;
    }

    /// <summary>
    /// Converts a property name to camelCase for JSON path.
    /// </summary>
    private static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
        {
            return str;
        }

        return char.ToLowerInvariant(str[0]) + str[1..];
    }

    /// <summary>
    /// Generates an index name from table name and JSON path.
    /// </summary>
    private static string GenerateIndexName(string tableName, string jsonPath)
    {
        // Remove special characters and convert to valid index name
        var pathPart = jsonPath.Replace("$.", "").Replace(".", "_");
        return $"idx_{tableName}_{pathPart}";
    }

    /// <summary>
    /// Generates a composite index name from table name and multiple JSON paths.
    /// </summary>
    private static string GenerateCompositeIndexName(string tableName, IEnumerable<string> jsonPaths)
    {
        var pathsPart = string.Join("_", jsonPaths.Select(p => p.Replace("$.", "").Replace(".", "_")));
        return $"idx_{tableName}_composite_{pathsPart}";
    }

    /// <inheritdoc />
    public async Task AddVirtualColumnAsync<T>(
        System.Linq.Expressions.Expression<Func<T, object>> jsonPath,
        string columnName,
        bool createIndex = false,
        string columnType = "TEXT")
    {
        ArgumentNullException.ThrowIfNull(jsonPath);

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw new ArgumentException("Column name cannot be null or empty.", nameof(columnName));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        var tableName = _tableNamingConvention.GetTableName<T>();
        var pathString = ExtractJsonPath(jsonPath);

        _logger.LogDebug("Adding virtual column {ColumnName} to table {TableName} for path {JsonPath}",
            columnName, tableName, pathString);

        // Check if column already exists using SchemaIntrospector
        var introspector = new SchemaIntrospector(_connection);
        var columnExists = await introspector.ColumnExistsAsync(tableName, columnName).ConfigureAwait(false);

        if (columnExists)
        {
            _logger.LogDebug("Column {ColumnName} already exists in table {TableName}, skipping creation",
                columnName, tableName);
        }
        else
        {
            var addColumnSql = SqlGenerator.GenerateAddVirtualColumnSql(tableName, columnName, pathString, columnType);
            await _connection.ExecuteAsync(addColumnSql).ConfigureAwait(false);

            _logger.LogInformation("Virtual column {ColumnName} created successfully on table {TableName} for path {JsonPath}",
                columnName, tableName, pathString);
        }

        // Register the virtual column in cache (whether newly created or already existing)
        _virtualColumnCache.Register(tableName, new VirtualColumnInfo(pathString, columnName, columnType));

        // Create index on the virtual column if requested
        if (createIndex)
        {
            var indexName = $"idx_{tableName}_{columnName}";

            // Check if index already exists
            var indexExists = await _connection.QueryFirstOrDefaultAsync<int>(
                SqlGenerator.GenerateCheckIndexExistsSql(),
                new { IndexName = indexName }).ConfigureAwait(false);

            if (indexExists > 0)
            {
                _logger.LogDebug("Index {IndexName} already exists, skipping creation", indexName);
            }
            else
            {
                var createIndexSql = SqlGenerator.GenerateCreateColumnIndexSql(tableName, indexName, columnName);
                await _connection.ExecuteAsync(createIndexSql).ConfigureAwait(false);

                _logger.LogInformation("Index {IndexName} created successfully on virtual column {ColumnName}",
                    indexName, columnName);
            }
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TResult>> SelectAsync<TSource, TResult>(
        System.Linq.Expressions.Expression<Func<TSource, TResult>> selector)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        ArgumentNullException.ThrowIfNull(selector);

        var tableName = _tableNamingConvention.GetTableName<TSource>();
        var fieldSelections = ExpressionToJsonPath.ExtractFieldSelections(selector);
        var sql = SqlGenerator.GenerateSelectFieldsSql(tableName, fieldSelections);

        _logger.LogDebug("Selecting fields {Fields} from table {TableName}",
            string.Join(", ", fieldSelections.Keys), tableName);

        var results = await _connection.QueryAsync<TResult>(sql).ConfigureAwait(false);

        _logger.LogDebug("Selected {Count} projected records from table {TableName}",
            results.Count(), tableName);

        return results;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TResult>> SelectAsync<TSource, TResult>(
        System.Linq.Expressions.Expression<Func<TSource, bool>> predicate,
        System.Linq.Expressions.Expression<Func<TSource, TResult>> selector)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureConnectionOpen();

        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(selector);

        var tableName = _tableNamingConvention.GetTableName<TSource>();
        var fieldSelections = ExpressionToJsonPath.ExtractFieldSelections(selector);

        // Get virtual columns for this table to enable index usage
        var virtualColumns = await _virtualColumnCache.GetAsync(tableName).ConfigureAwait(false);

        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, virtualColumns);
        var sql = SqlGenerator.GenerateSelectFieldsWithWhereSql(tableName, fieldSelections, whereClause);

        _logger.LogDebug("Selecting fields {Fields} from table {TableName} with WHERE clause: {WhereClause}",
            string.Join(", ", fieldSelections.Keys), tableName, whereClause);

        var results = await _connection.QueryAsync<TResult>(sql, parameters).ConfigureAwait(false);

        _logger.LogDebug("Selected {Count} projected records from table {TableName}",
            results.Count(), tableName);

        return results;
    }

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Don't check _disposed here - we want to return false instead of throwing
            if (_disposed)
            {
                _logger.LogWarning("Health check failed: DocumentStore is disposed");
                return false;
            }

            // Check connection state
            if (_connection.State != ConnectionState.Open)
            {
                _logger.LogWarning("Health check failed: Connection is not open (state: {State})", _connection.State);
                return false;
            }

            // Verify SQLite version supports JSONB (3.45+)
            var versionString = await _connection.QueryFirstOrDefaultAsync<string>(
                "SELECT sqlite_version()").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(versionString))
            {
                _logger.LogWarning("Health check failed: Could not retrieve SQLite version");
                return false;
            }

            if (!Version.TryParse(versionString, out var version))
            {
                _logger.LogWarning("Health check failed: Invalid SQLite version format: {Version}", versionString);
                return false;
            }

            var minVersion = new Version(3, 45, 0);
            if (version < minVersion)
            {
                _logger.LogWarning(
                    "Health check failed: SQLite version {Version} does not support JSONB (requires {MinVersion}+)",
                    version, minVersion);
                return false;
            }

            // Test basic query execution
            await _connection.QueryFirstOrDefaultAsync<int>("SELECT 1").ConfigureAwait(false);

            _logger.LogDebug("Health check passed: SQLite version {Version}", version);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed with exception");
            return false;
        }
    }

    /// <summary>
    /// Disposes the document store and, if owned, the underlying connection.
    /// Performs a WAL checkpoint if the connection is owned and in WAL mode to ensure data durability.
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
            await PerformWalCheckpointAsync().ConfigureAwait(false);
            _logger.LogDebug("Disposing owned connection");
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes the document store and, if owned, the underlying connection.
    /// Performs a WAL checkpoint if the connection is owned and in WAL mode to ensure data durability.
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
            PerformWalCheckpoint();
            _logger.LogDebug("Disposing owned connection");
            _connection.Dispose();
        }
    }

    /// <summary>
    /// Performs a WAL checkpoint to flush Write-Ahead Log to the database file for durability.
    /// Only executes if the connection is in a valid state and journal mode is WAL.
    /// </summary>
    private async Task PerformWalCheckpointAsync()
    {
        try
        {
            if (_connection.State == ConnectionState.Open)
            {
                // Check if we're in WAL mode
                var journalMode = await _connection.QueryFirstOrDefaultAsync<string>(
                    "PRAGMA journal_mode").ConfigureAwait(false);

                if (string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Executing WAL checkpoint before disposal");
                    // PRAGMA wal_checkpoint(TRUNCATE) ensures all WAL frames are checkpointed and the WAL file is truncated
                    await _connection.ExecuteAsync("PRAGMA wal_checkpoint(TRUNCATE)")
                        .ConfigureAwait(false);
                    _logger.LogInformation("WAL checkpoint completed successfully");
                }
            }
        }
        catch (Exception ex)
        {
            // Don't throw during disposal - log and continue
            _logger.LogWarning(ex, "Failed to perform WAL checkpoint during disposal");
        }
    }

    /// <summary>
    /// Performs a WAL checkpoint to flush Write-Ahead Log to the database file for durability (synchronous version).
    /// Only executes if the connection is in a valid state and journal mode is WAL.
    /// </summary>
    private void PerformWalCheckpoint()
    {
        try
        {
            if (_connection.State == ConnectionState.Open)
            {
                // Check if we're in WAL mode
                var journalMode = _connection.QueryFirstOrDefault<string>("PRAGMA journal_mode");

                if (string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Executing WAL checkpoint before disposal");
                    // PRAGMA wal_checkpoint(TRUNCATE) ensures all WAL frames are checkpointed and the WAL file is truncated
                    _connection.Execute("PRAGMA wal_checkpoint(TRUNCATE)");
                    _logger.LogInformation("WAL checkpoint completed successfully");
                }
            }
        }
        catch (Exception ex)
        {
            // Don't throw during disposal - log and continue
            _logger.LogWarning(ex, "Failed to perform WAL checkpoint during disposal");
        }
    }
}
