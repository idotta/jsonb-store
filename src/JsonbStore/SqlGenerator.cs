namespace JsonbStore;

/// <summary>
/// Internal helper class for generating SQL statements. 
/// Extracted for testability and maintainability.
/// </summary>
internal static class SqlGenerator
{
    /// <summary>
    /// Generates SQL for creating a table with JSONB storage.
    /// </summary>
    public static string GenerateCreateTableSql(string tableName)
    {
        return $@"
            CREATE TABLE IF NOT EXISTS [{tableName}] (
                id TEXT PRIMARY KEY,
                data BLOB NOT NULL,
                created_at INTEGER NOT NULL DEFAULT (strftime('%s', 'now')),
                updated_at INTEGER NOT NULL DEFAULT (strftime('%s', 'now'))
            )";
    }

    /// <summary>
    /// Generates SQL for upserting a document using JSONB format.
    /// </summary>
    public static string GenerateUpsertSql(string tableName)
    {
        return $@"
            INSERT INTO [{tableName}] (id, data, updated_at)
            VALUES (@Id, jsonb(@Data), strftime('%s', 'now'))
            ON CONFLICT(id) DO UPDATE SET
                data = jsonb(@Data),
                updated_at = strftime('%s', 'now')";
    }

    /// <summary>
    /// Generates SQL for retrieving a document by ID.
    /// </summary>
    public static string GenerateGetByIdSql(string tableName)
    {
        return $"SELECT json(data) as data FROM [{tableName}] WHERE id = @Id";
    }

    /// <summary>
    /// Generates SQL for retrieving all documents from a table.
    /// </summary>
    public static string GenerateGetAllSql(string tableName)
    {
        return $"SELECT json(data) as data FROM [{tableName}]";
    }

    /// <summary>
    /// Generates SQL for deleting a document by ID.
    /// </summary>
    public static string GenerateDeleteSql(string tableName)
    {
        return $"DELETE FROM [{tableName}] WHERE id = @Id";
    }
}
