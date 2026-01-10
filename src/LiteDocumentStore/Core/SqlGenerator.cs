namespace LiteDocumentStore;

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

    /// <summary>
    /// Generates SQL to check if a document exists by ID.
    /// </summary>
    public static string GenerateExistsSql(string tableName)
    {
        return $"SELECT EXISTS(SELECT 1 FROM [{tableName}] WHERE id = @Id)";
    }

    /// <summary>
    /// Generates SQL to count all documents in a table.
    /// </summary>
    public static string GenerateCountSql(string tableName)
    {
        return $"SELECT COUNT(*) FROM [{tableName}]";
    }

    /// <summary>
    /// Generates SQL to check if an index exists.
    /// </summary>
    public static string GenerateCheckIndexExistsSql()
    {
        return "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name=@IndexName";
    }

    /// <summary>
    /// Generates SQL for creating an index on a JSON path.
    /// </summary>
    /// <param name="tableName">The table name</param>
    /// <param name="indexName">The index name</param>
    /// <param name="jsonPath">The JSON path to index (e.g., '$.email')</param>
    public static string GenerateCreateJsonIndexSql(string tableName, string indexName, string jsonPath)
    {
        return $"CREATE INDEX IF NOT EXISTS [{indexName}] ON [{tableName}] (json_extract(data, '{jsonPath}'))";
    }

    /// <summary>
    /// Generates SQL for creating a composite index on multiple JSON paths.
    /// </summary>
    /// <param name="tableName">The table name</param>
    /// <param name="indexName">The index name</param>
    /// <param name="jsonPaths">The JSON paths to index</param>
    public static string GenerateCreateCompositeJsonIndexSql(string tableName, string indexName, IEnumerable<string> jsonPaths)
    {
        var extractClauses = string.Join(", ", jsonPaths.Select(p => $"json_extract(data, '{p}')"));
        return $"CREATE INDEX IF NOT EXISTS [{indexName}] ON [{tableName}] ({extractClauses})";
    }

    /// <summary>
    /// Generates SQL for bulk upserting multiple documents using a single statement.
    /// </summary>
    /// <param name="tableName">The table name</param>
    /// <param name="count">The number of items to upsert</param>
    public static string GenerateBulkUpsertSql(string tableName, int count)
    {
        if (count <= 0)
        {
            throw new ArgumentException("Count must be greater than zero.", nameof(count));
        }

        // Generate parameter placeholders for each item: (@Id0, jsonb(@Data0)), (@Id1, jsonb(@Data1)), ...
        var valuesClauses = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            valuesClauses.Add($"(@Id{i}, jsonb(@Data{i}), strftime('%s', 'now'))");
        }

        return $@"
            INSERT INTO [{tableName}] (id, data, updated_at)
            VALUES {string.Join(", ", valuesClauses)}
            ON CONFLICT(id) DO UPDATE SET
                data = excluded.data,
                updated_at = excluded.updated_at";
    }

    /// <summary>
    /// Generates SQL for bulk deleting multiple documents by their IDs using a single statement.
    /// </summary>
    /// <param name="tableName">The table name</param>
    /// <param name="count">The number of items to delete</param>
    public static string GenerateBulkDeleteSql(string tableName, int count)
    {
        if (count <= 0)
        {
            throw new ArgumentException("Count must be greater than zero.", nameof(count));
        }

        // Generate parameter placeholders for each ID: @Id0, @Id1, @Id2, ...
        var idParameters = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            idParameters.Add($"@Id{i}");
        }

        return $"DELETE FROM [{tableName}] WHERE id IN ({string.Join(", ", idParameters)})";
    }

    /// <summary>
    /// Generates SQL for querying documents by a JSON path and value.
    /// </summary>
    /// <param name="tableName">The table name</param>
    /// <param name="jsonPath">The JSON path to query (e.g., '$.email')</param>
    public static string GenerateQueryByJsonPathSql(string tableName, string jsonPath)
    {
        return $"SELECT json(data) as data FROM [{tableName}] WHERE json_extract(data, '{jsonPath}') = @Value";
    }

    /// <summary>
    /// Generates SQL for querying documents with a custom WHERE clause.
    /// </summary>
    /// <param name="tableName">The table name</param>
    /// <param name="whereClause">The WHERE clause (without the WHERE keyword)</param>
    public static string GenerateQueryWithWhereSql(string tableName, string whereClause)
    {
        return $"SELECT json(data) as data FROM [{tableName}] WHERE {whereClause}";
    }
}
