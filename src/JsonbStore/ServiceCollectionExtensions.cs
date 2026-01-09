using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace JsonbStore;

/// <summary>
/// Extension methods for configuring JsonbStore services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds JsonbStore services to the specified <see cref="IServiceCollection"/> with a singleton document store.
    /// Uses a single long-lived connection for optimal performance.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="JsonbStoreOptions"/></param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining</returns>
    public static IServiceCollection AddJsonbStore(
        this IServiceCollection services,
        Action<JsonbStoreOptions> configureOptions)
    {
        return services.AddJsonbStore(configureOptions, ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Adds JsonbStore services to the specified <see cref="IServiceCollection"/> with configurable lifetime.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="JsonbStoreOptions"/></param>
    /// <param name="lifetime">The service lifetime (Singleton recommended for single long-lived connection, Scoped for connection per request)</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining</returns>
    public static IServiceCollection AddJsonbStore(
        this IServiceCollection services,
        Action<JsonbStoreOptions> configureOptions,
        ServiceLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new JsonbStoreOptions();
        configureOptions(options);

        return services.AddJsonbStore(options, lifetime);
    }

    /// <summary>
    /// Adds JsonbStore services to the specified <see cref="IServiceCollection"/> with pre-configured options.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to</param>
    /// <param name="options">The pre-configured <see cref="JsonbStoreOptions"/></param>
    /// <param name="lifetime">The service lifetime (default: Singleton)</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining</returns>
    public static IServiceCollection AddJsonbStore(
        this IServiceCollection services,
        JsonbStoreOptions options,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        // Register the options as singleton (configuration should be immutable)
        services.TryAddSingleton(options);

        // Register default implementations if not already registered
        services.TryAddSingleton<IJsonSerializer, SystemTextJsonSerializer>();
        services.TryAddSingleton<ITableNamingConvention, DefaultTableNamingConvention>();

        // Register connection factory based on options
        if (options.ConnectionFactory == null)
        {
            services.TryAdd(ServiceDescriptor.Describe(
                typeof(IConnectionFactory),
                sp => new DefaultConnectionFactory(options),
                lifetime));
        }
        else
        {
            services.TryAdd(ServiceDescriptor.Describe(
                typeof(IConnectionFactory),
                sp => options.ConnectionFactory,
                lifetime));
        }

        // Register the DocumentStore with the specified lifetime
        services.TryAdd(ServiceDescriptor.Describe(
            typeof(IDocumentStore),
            sp => CreateDocumentStore(sp, options),
            lifetime));

        return services;
    }

    // Note: For multiple database support, use AddKeyedJsonbStore() instead (requires .NET 8+).

    /// <summary>
    /// Adds a keyed JsonbStore document store for managing multiple databases (requires .NET 8+).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to</param>
    /// <param name="serviceKey">The key to identify this store instance</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="JsonbStoreOptions"/></param>
    /// <param name="lifetime">The service lifetime (default: Singleton)</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining</returns>
    public static IServiceCollection AddKeyedJsonbStore(
        this IServiceCollection services,
        object serviceKey,
        Action<JsonbStoreOptions> configureOptions,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new JsonbStoreOptions();
        configureOptions(options);

        return services.AddKeyedJsonbStore(serviceKey, options, lifetime);
    }

    /// <summary>
    /// Adds a keyed JsonbStore document store for managing multiple databases (requires .NET 8+).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to</param>
    /// <param name="serviceKey">The key to identify this store instance</param>
    /// <param name="options">The pre-configured <see cref="JsonbStoreOptions"/></param>
    /// <param name="lifetime">The service lifetime (default: Singleton)</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining</returns>
    public static IServiceCollection AddKeyedJsonbStore(
        this IServiceCollection services,
        object serviceKey,
        JsonbStoreOptions options,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(options);

        // Register shared services if not already registered
        services.TryAddSingleton<IJsonSerializer, SystemTextJsonSerializer>();
        services.TryAddSingleton<ITableNamingConvention, DefaultTableNamingConvention>();

        // Register connection factory for this key
        if (options.ConnectionFactory == null)
        {
            services.Add(ServiceDescriptor.DescribeKeyed(
                typeof(IConnectionFactory),
                serviceKey,
                (sp, key) => new DefaultConnectionFactory(options),
                lifetime));
        }
        else
        {
            services.Add(ServiceDescriptor.DescribeKeyed(
                typeof(IConnectionFactory),
                serviceKey,
                (sp, key) => options.ConnectionFactory,
                lifetime));
        }

        // Register the keyed DocumentStore
        services.Add(ServiceDescriptor.DescribeKeyed(
            typeof(IDocumentStore),
            serviceKey,
            (sp, key) => CreateKeyedDocumentStore(sp, key!, options),
            lifetime));

        return services;
    }

    private static DocumentStore CreateDocumentStore(IServiceProvider serviceProvider, JsonbStoreOptions options)
    {
        var jsonSerializer = options.JsonSerializer ?? serviceProvider.GetRequiredService<IJsonSerializer>();
        var tableNamingConvention = options.TableNamingConvention ?? serviceProvider.GetRequiredService<ITableNamingConvention>();
        var logger = serviceProvider.GetService<ILogger<DocumentStore>>();

        return new DocumentStore(connection, jsonSerializer, tableNamingConvention, logger);
    }

    private static DocumentStore CreateKeyedDocumentStore(IServiceProvider serviceProvider, object key, JsonbStoreOptions options)
    {
        var jsonSerializer = options.JsonSerializer ?? serviceProvider.GetRequiredService<IJsonSerializer>();
        var tableNamingConvention = options.TableNamingConvention ?? serviceProvider.GetRequiredService<ITableNamingConvention>();
        var logger = serviceProvider.GetService<ILogger<DocumentStore>>();

        return new DocumentStore(connection, jsonSerializer, tableNamingConvention, logger);
    }
}
