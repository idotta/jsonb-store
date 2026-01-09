using Dapper;
using Microsoft.Data.Sqlite;

namespace LiteDocumentStore;

/// <summary>
/// Provides utilities for inspecting database schema information.
/// Create an instance with a connection to query table structures, indexes, and database statistics.
/// </summary>
public sealed class SchemaIntrospector
{
    private readonly SqliteConnection _connection;

    /// <summary>
    /// Initializes a new schema introspector with the specified connection.
    /// </summary>
    /// <param name="connection">The open SQLite connection</param>
    public SchemaIntrospector(SqliteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    /// <summary>
    /// Gets information about all tables in the database.
    /// </summary>
    /// <returns>An enumerable of table information records</returns>
    public async Task<IEnumerable<TableInfo>> GetTablesAsync()
    {
        var sql = @"
            SELECT name as Name, type as Type, sql as Sql
            FROM sqlite_master 
            WHERE type = 'table' 
            AND name NOT LIKE 'sqlite_%'
            ORDER BY name";

        return await _connection.QueryAsync<TableInfo>(sql).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if a table exists in the database.
    /// </summary>
    /// <param name="tableName">The name of the table to check</param>
    /// <returns>True if the table exists, false otherwise</returns>
    public async Task<bool> TableExistsAsync(string tableName)
    {
        ArgumentNullException.ThrowIfNull(tableName);

        var sql = @"
            SELECT COUNT(*) 
            FROM sqlite_master 
            WHERE type = 'table' 
            AND name = @TableName";

        var count = await _connection.ExecuteScalarAsync<int>(sql, new { TableName = tableName })
            .ConfigureAwait(false);

        return count > 0;
    }

    /// <summary>
    /// Gets information about all columns in a specific table.
    /// </summary>
    /// <param name="tableName">The name of the table</param>
    /// <returns>An enumerable of column information records</returns>
    public async Task<IEnumerable<ColumnInfo>> GetColumnsAsync(string tableName)
    {
        ArgumentNullException.ThrowIfNull(tableName);

        var sql = $"PRAGMA table_info([{tableName}])";
        var pragmaResults = await _connection.QueryAsync(sql).ConfigureAwait(false);

        return pragmaResults.Select(row => new ColumnInfo
        {
            ColumnId = (long)row.cid,
            Name = (string)row.name,
            Type = (string)row.type,
            NotNull = (long)row.notnull == 1,
            DefaultValue = row.dflt_value,
            IsPrimaryKey = (long)row.pk == 1
        });
    }

    /// <summary>
    /// Gets information about all indexes in the database or for a specific table.
    /// </summary>
    /// <param name="tableName">Optional table name to filter indexes</param>
    /// <returns>An enumerable of index information records</returns>
    public async Task<IEnumerable<IndexInfo>> GetIndexesAsync(string? tableName = null)
    {
        var sql = @"
            SELECT name as Name, tbl_name as TableName, sql as Sql
            FROM sqlite_master 
            WHERE type = 'index' 
            AND name NOT LIKE 'sqlite_%'";

        if (!string.IsNullOrEmpty(tableName))
        {
            sql += " AND tbl_name = @TableName";
        }

        sql += " ORDER BY name";

        return await _connection.QueryAsync<IndexInfo>(sql, new { TableName = tableName })
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if an index exists in the database.
    /// </summary>
    /// <param name="indexName">The name of the index to check</param>
    /// <returns>True if the index exists, false otherwise</returns>
    public async Task<bool> IndexExistsAsync(string indexName)
    {
        ArgumentNullException.ThrowIfNull(indexName);

        var sql = @"
            SELECT COUNT(*) 
            FROM sqlite_master 
            WHERE type = 'index' 
            AND name = @IndexName";

        var count = await _connection.ExecuteScalarAsync<int>(sql, new { IndexName = indexName })
            .ConfigureAwait(false);

        return count > 0;
    }

    /// <summary>
    /// Gets the SQLite version being used.
    /// </summary>
    /// <returns>The SQLite version string</returns>
    public async Task<string> GetSqliteVersionAsync()
    {
        var sql = "SELECT sqlite_version()";
        var version = await _connection.ExecuteScalarAsync<string>(sql).ConfigureAwait(false);
        return version ?? "Unknown";
    }

    /// <summary>
    /// Gets database statistics including page size, page count, and database size.
    /// </summary>
    /// <returns>Database statistics</returns>
    public async Task<DatabaseStatistics> GetDatabaseStatisticsAsync()
    {
        var pageCountSql = "PRAGMA page_count";
        var pageSizeSql = "PRAGMA page_size";
        var freeSql = "PRAGMA freelist_count";

        var pageCount = await _connection.ExecuteScalarAsync<long>(pageCountSql).ConfigureAwait(false);
        var pageSize = await _connection.ExecuteScalarAsync<long>(pageSizeSql).ConfigureAwait(false);
        var freePages = await _connection.ExecuteScalarAsync<long>(freeSql).ConfigureAwait(false);

        return new DatabaseStatistics
        {
            PageCount = pageCount,
            PageSize = pageSize,
            FreePages = freePages,
            DatabaseSizeBytes = pageCount * pageSize,
            UsedSizeBytes = (pageCount - freePages) * pageSize
        };
    }
}

/// <summary>
/// Contains information about a database table.
/// </summary>
public sealed class TableInfo
{
    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the table type (typically "table").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the CREATE TABLE SQL statement.
    /// </summary>
    public string? Sql { get; set; }
}

/// <summary>
/// Contains information about a table column.
/// </summary>
public sealed class ColumnInfo
{
    /// <summary>
    /// Gets or sets the column ID (position in table).
    /// </summary>
    public long ColumnId { get; set; }

    /// <summary>
    /// Gets or sets the column name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the column type (e.g., TEXT, INTEGER, BLOB).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the column has a NOT NULL constraint.
    /// </summary>
    public bool NotNull { get; set; }

    /// <summary>
    /// Gets or sets the default value for the column, if any.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this column is part of the primary key.
    /// </summary>
    public bool IsPrimaryKey { get; set; }
}

/// <summary>
/// Contains information about a database index.
/// </summary>
public sealed class IndexInfo
{
    /// <summary>
    /// Gets or sets the index name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the table this index belongs to.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the CREATE INDEX SQL statement.
    /// </summary>
    public string? Sql { get; set; }
}

/// <summary>
/// Contains database statistics information.
/// </summary>
public sealed class DatabaseStatistics
{
    /// <summary>
    /// Gets or sets the total number of pages in the database.
    /// </summary>
    public long PageCount { get; set; }

    /// <summary>
    /// Gets or sets the size of each page in bytes.
    /// </summary>
    public long PageSize { get; set; }

    /// <summary>
    /// Gets or sets the number of free pages.
    /// </summary>
    public long FreePages { get; set; }

    /// <summary>
    /// Gets or sets the total database size in bytes.
    /// </summary>
    public long DatabaseSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the used space in bytes.
    /// </summary>
    public long UsedSizeBytes { get; set; }
}
