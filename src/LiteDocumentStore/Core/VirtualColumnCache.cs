using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace LiteDocumentStore;

/// <summary>
/// Contains information about a virtual column mapped to a JSON path.
/// </summary>
/// <param name="JsonPath">The JSON path expression (e.g., '$.Category')</param>
/// <param name="ColumnName">The virtual column name in the table</param>
/// <param name="ColumnType">The SQLite column type (e.g., TEXT, INTEGER, REAL)</param>
public sealed record VirtualColumnInfo(string JsonPath, string ColumnName, string ColumnType);

/// <summary>
/// Caches virtual column information per table to enable query optimization.
/// Automatically discovers existing virtual columns from the database schema on first access
/// and updates the cache when new virtual columns are added.
/// </summary>
internal sealed partial class VirtualColumnCache
{
    private readonly SqliteConnection _connection;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, VirtualColumnInfo>> _cache = new();

    // Regex to extract json_extract path from generated column definition
    // Matches: json_extract(data, '$.Path.To.Property')
    [GeneratedRegex(@"json_extract\s*\(\s*data\s*,\s*'(\$\.[^']+)'\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex JsonExtractPattern();

    /// <summary>
    /// Initializes a new virtual column cache with the specified connection.
    /// </summary>
    /// <param name="connection">The SQLite connection to use for schema introspection</param>
    public VirtualColumnCache(SqliteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    /// <summary>
    /// Gets virtual columns for a table. Loads from schema on first access.
    /// </summary>
    /// <param name="tableName">The table name to get virtual columns for</param>
    /// <returns>A dictionary mapping JSON paths to virtual column info</returns>
    public async ValueTask<IReadOnlyDictionary<string, VirtualColumnInfo>> GetAsync(string tableName)
    {
        ArgumentNullException.ThrowIfNull(tableName);

        if (_cache.TryGetValue(tableName, out var columns))
        {
            return columns;
        }

        var loaded = await LoadFromSchemaAsync(tableName).ConfigureAwait(false);
        _cache[tableName] = loaded;
        return loaded;
    }

    /// <summary>
    /// Registers a newly created virtual column, updating the cache without re-querying schema.
    /// </summary>
    /// <param name="tableName">The table name</param>
    /// <param name="column">The virtual column info to register</param>
    public void Register(string tableName, VirtualColumnInfo column)
    {
        ArgumentNullException.ThrowIfNull(tableName);
        ArgumentNullException.ThrowIfNull(column);

        _cache.AddOrUpdate(
            tableName,
            _ => new Dictionary<string, VirtualColumnInfo> { [column.JsonPath] = column },
            (_, existing) =>
            {
                var updated = new Dictionary<string, VirtualColumnInfo>(existing)
                {
                    [column.JsonPath] = column
                };
                return updated;
            });
    }

    /// <summary>
    /// Invalidates the cache for a specific table, forcing reload on next access.
    /// </summary>
    /// <param name="tableName">The table name to invalidate</param>
    public void Invalidate(string tableName)
    {
        ArgumentNullException.ThrowIfNull(tableName);
        _cache.TryRemove(tableName, out _);
    }

    /// <summary>
    /// Clears the entire cache.
    /// </summary>
    public void Clear() => _cache.Clear();

    /// <summary>
    /// Loads virtual column information from the database schema.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, VirtualColumnInfo>> LoadFromSchemaAsync(string tableName)
    {
        var result = new Dictionary<string, VirtualColumnInfo>();
        var introspector = new SchemaIntrospector(_connection);

        // Get all columns for the table
        var columns = await introspector.GetColumnsAsync(tableName).ConfigureAwait(false);
        var hiddenColumns = columns.Where(c => c.IsHidden).ToList();

        if (hiddenColumns.Count == 0)
        {
            return result;
        }

        // Get the table SQL to parse generated column expressions
        var tables = await introspector.GetTablesAsync().ConfigureAwait(false);
        var tableInfo = tables.FirstOrDefault(t =>
            string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));

        if (tableInfo?.Sql == null)
        {
            return result;
        }

        // Parse each hidden column's generation expression from the table SQL
        foreach (var column in hiddenColumns)
        {
            var jsonPath = ExtractJsonPathForColumn(tableInfo.Sql, column.Name);
            if (jsonPath != null)
            {
                result[jsonPath] = new VirtualColumnInfo(jsonPath, column.Name, column.Type);
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts the JSON path from a column's generated expression in the CREATE TABLE SQL.
    /// </summary>
    private static string? ExtractJsonPathForColumn(string tableSql, string columnName)
    {
        // Pattern to match: [columnName] TYPE GENERATED ALWAYS AS (json_extract(data, '$.Path')) VIRTUAL
        // We need to find the json_extract for this specific column
        var columnPattern = $@"\[?{Regex.Escape(columnName)}\]?\s+\w+\s+GENERATED\s+ALWAYS\s+AS\s*\(([^)]+)\)";
        var columnMatch = Regex.Match(tableSql, columnPattern, RegexOptions.IgnoreCase);

        if (!columnMatch.Success)
        {
            return null;
        }

        var expression = columnMatch.Groups[1].Value;
        var jsonMatch = JsonExtractPattern().Match(expression);

        return jsonMatch.Success ? jsonMatch.Groups[1].Value : null;
    }
}
