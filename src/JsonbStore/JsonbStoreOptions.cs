namespace JsonbStore;

/// <summary>
/// Configuration options for JsonbStore repository behavior and SQLite performance settings.
/// </summary>
public class JsonbStoreOptions
{
    /// <summary>
    /// Gets or sets the database file path or connection string.
    /// Use ":memory:" for in-memory database or "file::memory:?cache=shared" for shared in-memory cache.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to enable Write-Ahead Logging (WAL) mode.
    /// WAL mode significantly improves write performance and concurrency.
    /// Default is true.
    /// </summary>
    public bool EnableWalMode { get; set; } = true;

    /// <summary>
    /// Gets or sets the synchronous mode for SQLite.
    /// Options: FULL (safest, slowest), NORMAL (balanced), OFF (fastest, risky).
    /// Default is NORMAL for optimal performance with reasonable durability.
    /// </summary>
    public SynchronousMode SynchronousMode { get; set; } = SynchronousMode.Normal;

    /// <summary>
    /// Gets or sets the page size in bytes.
    /// Valid values are powers of 2 between 512 and 65536.
    /// Default is 4096. Larger values may improve performance for large datasets.
    /// </summary>
    public int PageSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the cache size in number of pages.
    /// Negative values interpret as kilobytes (e.g., -2000 = 2MB).
    /// Default is -2000 (2MB).
    /// </summary>
    public int CacheSize { get; set; } = -2000;

    /// <summary>
    /// Gets or sets the busy timeout in milliseconds.
    /// How long to wait when the database is locked before returning SQLITE_BUSY.
    /// Default is 5000ms (5 seconds).
    /// </summary>
    public int BusyTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets whether to enable foreign key constraints.
    /// Default is true.
    /// </summary>
    public bool EnableForeignKeys { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use pooled connections.
    /// Default is false (single long-lived connection).
    /// </summary>
    public bool UseConnectionPooling { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of connections in the pool (if pooling is enabled).
    /// Default is 10.
    /// </summary>
    public int MaxPoolSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets the default table naming convention.
    /// If null, uses the simple type name as table name.
    /// </summary>
    public ITableNamingConvention? TableNamingConvention { get; set; }

    /// <summary>
    /// Gets or sets the JSON serializer to use.
    /// If null, uses System.Text.Json with default settings.
    /// </summary>
    public IJsonSerializer? JsonSerializer { get; set; }

    /// <summary>
    /// Gets or sets additional PRAGMA statements to execute on connection open.
    /// Useful for custom SQLite configuration.
    /// </summary>
    public List<string> AdditionalPragmas { get; set; } = new();

    /// <summary>
    /// Creates a new instance of JsonbStoreOptions with default settings.
    /// </summary>
    public JsonbStoreOptions()
    {
    }

    /// <summary>
    /// Creates a new instance of JsonbStoreOptions with the specified connection string.
    /// </summary>
    /// <param name="connectionString">The database file path or connection string</param>
    public JsonbStoreOptions(string connectionString)
    {
        ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Creates a builder for configuring JsonbStoreOptions with a fluent API.
    /// </summary>
    /// <param name="connectionString">The database file path or connection string</param>
    /// <returns>A new JsonbStoreOptionsBuilder instance</returns>
    public static JsonbStoreOptionsBuilder Builder(string connectionString)
    {
        return new JsonbStoreOptionsBuilder(connectionString);
    }

    /// <summary>
    /// Creates a builder for configuring JsonbStoreOptions with a fluent API.
    /// </summary>
    /// <returns>A new JsonbStoreOptionsBuilder instance</returns>
    public static JsonbStoreOptionsBuilder Builder()
    {
        return new JsonbStoreOptionsBuilder();
    }

    /// <summary>
    /// Creates options for a file-based SQLite database with optimized settings.
    /// </summary>
    /// <param name="filePath">Path to the database file</param>
    /// <returns>JsonbStoreOptions configured for file-based storage</returns>
    public static JsonbStoreOptions ForFile(string filePath)
    {
        return new JsonbStoreOptions
        {
            ConnectionString = $"Data Source={filePath}",
            EnableWalMode = true,
            SynchronousMode = SynchronousMode.Normal
        };
    }

    /// <summary>
    /// Creates options for an in-memory SQLite database.
    /// Data will be lost when the connection closes.
    /// </summary>
    /// <returns>JsonbStoreOptions configured for in-memory storage</returns>
    public static JsonbStoreOptions ForInMemory()
    {
        return new JsonbStoreOptions
        {
            ConnectionString = "Data Source=:memory:",
            EnableWalMode = false, // WAL not supported for :memory:
            SynchronousMode = SynchronousMode.Off // Maximum performance for in-memory
        };
    }

    /// <summary>
    /// Creates options for a shared in-memory SQLite database.
    /// Multiple connections can access the same in-memory database.
    /// </summary>
    /// <param name="cacheName">Optional name for the shared cache (default: "shared")</param>
    /// <returns>JsonbStoreOptions configured for shared in-memory storage</returns>
    public static JsonbStoreOptions ForSharedInMemory(string cacheName = "shared")
    {
        return new JsonbStoreOptions
        {
            ConnectionString = $"Data Source=file:{cacheName}?mode=memory&cache=shared",
            EnableWalMode = false, // WAL not supported for in-memory
            SynchronousMode = SynchronousMode.Off
        };
    }

    /// <summary>
    /// Creates a copy of the current options.
    /// </summary>
    /// <returns>A new JsonbStoreOptions instance with copied values</returns>
    public JsonbStoreOptions Clone()
    {
        return new JsonbStoreOptions
        {
            ConnectionString = ConnectionString,
            EnableWalMode = EnableWalMode,
            SynchronousMode = SynchronousMode,
            PageSize = PageSize,
            CacheSize = CacheSize,
            BusyTimeoutMs = BusyTimeoutMs,
            EnableForeignKeys = EnableForeignKeys,
            UseConnectionPooling = UseConnectionPooling,
            MaxPoolSize = MaxPoolSize,
            TableNamingConvention = TableNamingConvention,
            JsonSerializer = JsonSerializer,
            AdditionalPragmas = [.. AdditionalPragmas]
        };
    }
}

/// <summary>
/// SQLite synchronous mode settings.
/// </summary>
public enum SynchronousMode
{
    /// <summary>
    /// Fastest, but risky. Data may be corrupted on power loss or OS crash.
    /// </summary>
    Off = 0,

    /// <summary>
    /// Balanced performance and durability. Good for most applications.
    /// In WAL mode, only syncs on checkpoint (safe for application crashes).
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Safest, but slowest. Guarantees data integrity even on power loss.
    /// </summary>
    Full = 2
}
