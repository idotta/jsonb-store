namespace JsonbStore;

/// <summary>
/// Builder for creating JsonbStoreOptions with a fluent API.
/// </summary>
public class JsonbStoreOptionsBuilder
{
    private readonly JsonbStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of JsonbStoreOptionsBuilder.
    /// </summary>
    public JsonbStoreOptionsBuilder()
    {
        _options = new JsonbStoreOptions();
    }

    /// <summary>
    /// Initializes a new instance of JsonbStoreOptionsBuilder with a connection string.
    /// </summary>
    /// <param name="connectionString">The database file path or connection string</param>
    public JsonbStoreOptionsBuilder(string connectionString)
    {
        _options = new JsonbStoreOptions(connectionString);
    }

    /// <summary>
    /// Sets the connection string.
    /// </summary>
    /// <param name="connectionString">The database file path or connection string</param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder WithConnectionString(string connectionString)
    {
        _options.ConnectionString = connectionString;
        return this;
    }

    /// <summary>
    /// Configures the database to use a file-based SQLite database.
    /// </summary>
    /// <param name="filePath">Path to the database file</param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder UseFile(string filePath)
    {
        _options.ConnectionString = $"Data Source={filePath}";
        return this;
    }

    /// <summary>
    /// Configures the database to use an in-memory SQLite database.
    /// Data will be lost when the connection closes.
    /// </summary>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder UseInMemory()
    {
        _options.ConnectionString = "Data Source=:memory:";
        _options.EnableWalMode = false; // WAL not supported for :memory:
        _options.SynchronousMode = SynchronousMode.Off;
        return this;
    }

    /// <summary>
    /// Configures the database to use a shared in-memory SQLite database.
    /// Multiple connections can access the same in-memory database.
    /// </summary>
    /// <param name="cacheName">Optional name for the shared cache (default: "shared")</param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder UseSharedInMemory(string cacheName = "shared")
    {
        _options.ConnectionString = $"Data Source=file:{cacheName}?mode=memory&cache=shared";
        _options.EnableWalMode = false; // WAL not supported for in-memory
        _options.SynchronousMode = SynchronousMode.Off;
        return this;
    }

    /// <summary>
    /// Enables or disables Write-Ahead Logging (WAL) mode.
    /// </summary>
    /// <param name="enabled">True to enable WAL mode, false to disable</param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder WithWalMode(bool enabled = true)
    {
        _options.EnableWalMode = enabled;
        return this;
    }

    /// <summary>
    /// Sets the synchronous mode for SQLite.
    /// </summary>
    /// <param name="mode">The synchronous mode (Off, Normal, or Full)</param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder WithSynchronousMode(SynchronousMode mode)
    {
        _options.SynchronousMode = mode;
        return this;
    }

    /// <summary>
    /// Sets the page size in bytes.
    /// Valid values are powers of 2 between 512 and 65536.
    /// </summary>
    /// <param name="pageSize">The page size in bytes</param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder WithPageSize(int pageSize)
    {
        if (pageSize < 512 || pageSize > 65536 || (pageSize & (pageSize - 1)) != 0)
        {
            throw new ArgumentException("Page size must be a power of 2 between 512 and 65536.", nameof(pageSize));
        }
        _options.PageSize = pageSize;
        return this;
    }

    /// <summary>
    /// Sets the cache size in number of pages or kilobytes.
    /// </summary>
    /// <param name="cacheSize">
    /// Positive values specify number of pages.
    /// Negative values specify kilobytes (e.g., -2000 = 2MB).
    /// </param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder WithCacheSize(int cacheSize)
    {
        _options.CacheSize = cacheSize;
        return this;
    }

    /// <summary>
    /// Sets the cache size in megabytes.
    /// </summary>
    /// <param name="cacheSizeMb">Cache size in megabytes</param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder WithCacheSizeMb(int cacheSizeMb)
    {
        _options.CacheSize = -cacheSizeMb * 1024;
        return this;
    }

    /// <summary>
    /// Sets the busy timeout in milliseconds.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder WithBusyTimeout(int timeoutMs)
    {
        _options.BusyTimeoutMs = timeoutMs;
        return this;
    }

    /// <summary>
    /// Enables or disables foreign key constraints.
    /// </summary>
    /// <param name="enabled">True to enable foreign keys, false to disable</param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder WithForeignKeys(bool enabled = true)
    {
        _options.EnableForeignKeys = enabled;
        return this;
    }

    /// <summary>
    /// Enables connection pooling with optional pool size configuration.
    /// </summary>
    /// <param name="maxPoolSize">Maximum number of connections in the pool (default: 10)</param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder WithConnectionPooling(int maxPoolSize = 10)
    {
        _options.UseConnectionPooling = true;
        _options.MaxPoolSize = maxPoolSize;
        return this;
    }

    /// <summary>
    /// Sets the table naming convention.
    /// </summary>
    /// <param name="convention">The table naming convention to use</param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder WithTableNamingConvention(ITableNamingConvention convention)
    {
        _options.TableNamingConvention = convention;
        return this;
    }

    /// <summary>
    /// Sets the JSON serializer.
    /// </summary>
    /// <param name="serializer">The JSON serializer to use</param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder WithJsonSerializer(IJsonSerializer serializer)
    {
        _options.JsonSerializer = serializer;
        return this;
    }

    /// <summary>
    /// Adds a custom PRAGMA statement to execute on connection open.
    /// </summary>
    /// <param name="pragma">The PRAGMA statement (e.g., "PRAGMA temp_store = MEMORY")</param>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder AddPragma(string pragma)
    {
        if (!string.IsNullOrWhiteSpace(pragma))
        {
            _options.AdditionalPragmas.Add(pragma);
        }
        return this;
    }

    /// <summary>
    /// Configures options for high-performance scenarios.
    /// Sets: WAL mode, synchronous=NORMAL, larger cache, optimized page size.
    /// </summary>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder OptimizeForPerformance()
    {
        _options.EnableWalMode = true;
        _options.SynchronousMode = SynchronousMode.Normal;
        _options.PageSize = 8192; // Larger page size for better throughput
        _options.CacheSize = -4000; // 4MB cache
        return this;
    }

    /// <summary>
    /// Configures options for maximum durability and data safety.
    /// Sets: WAL mode, synchronous=FULL, foreign keys enabled.
    /// </summary>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder OptimizeForSafety()
    {
        _options.EnableWalMode = true;
        _options.SynchronousMode = SynchronousMode.Full;
        _options.EnableForeignKeys = true;
        return this;
    }

    /// <summary>
    /// Configures options for development/testing scenarios.
    /// Sets: In-memory database, no WAL, synchronous=OFF for maximum speed.
    /// </summary>
    /// <returns>This builder for method chaining</returns>
    public JsonbStoreOptionsBuilder OptimizeForTesting()
    {
        UseInMemory();
        return this;
    }

    /// <summary>
    /// Builds the JsonbStoreOptions instance.
    /// </summary>
    /// <returns>The configured JsonbStoreOptions</returns>
    public JsonbStoreOptions Build()
    {
        if (string.IsNullOrEmpty(_options.ConnectionString))
        {
            throw new InvalidOperationException("Connection string must be set before building options.");
        }
        return _options.Clone();
    }
}
