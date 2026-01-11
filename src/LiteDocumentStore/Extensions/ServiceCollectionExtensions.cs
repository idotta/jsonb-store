using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace LiteDocumentStore;

/// <summary>
/// Extension methods for configuring LiteDocumentStore services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds LiteDocumentStore services to the specified <see cref="IServiceCollection"/> with a singleton document store.
    /// Uses a single long-lived connection for optimal performance.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="DocumentStoreOptions"/></param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining</returns>
    public static IServiceCollection AddLiteDocumentStore(
        this IServiceCollection services,
        Action<DocumentStoreOptions> configureOptions)
    {
        return services.AddLiteDocumentStore(configureOptions, ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Adds LiteDocumentStore services to the specified <see cref="IServiceCollection"/> with configurable lifetime.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="DocumentStoreOptions"/></param>
    /// <param name="lifetime">The service lifetime (Singleton recommended for single long-lived connection, Scoped for connection per request)</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining</returns>
    public static IServiceCollection AddLiteDocumentStore(
        this IServiceCollection services,
        Action<DocumentStoreOptions> configureOptions,
        ServiceLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new DocumentStoreOptions();
        configureOptions(options);

        return services.AddLiteDocumentStore(options, lifetime);
    }

    /// <summary>
    /// Adds LiteDocumentStore services to the specified <see cref="IServiceCollection"/> with pre-configured options.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to</param>
    /// <param name="options">The pre-configured <see cref="DocumentStoreOptions"/></param>
    /// <param name="lifetime">The service lifetime (default: Singleton)</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining</returns>
    public static IServiceCollection AddLiteDocumentStore(
        this IServiceCollection services,
        DocumentStoreOptions options,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        // Register core dependencies as singletons (stateless, reusable)
        services.TryAddSingleton<IConnectionFactory, DefaultConnectionFactory>();
        services.TryAddSingleton<ITableNamingConvention, DefaultTableNamingConvention>();

        // Register the document store factory
        services.TryAddSingleton<IDocumentStoreFactory>(sp => new DocumentStoreFactory(
            sp.GetRequiredService<IConnectionFactory>(),
            sp.GetRequiredService<ITableNamingConvention>(),
            sp.GetService<ILoggerFactory>()));

        // Register the DocumentStore with the specified lifetime
        // The store is created via the factory and owns its connection
        services.TryAdd(ServiceDescriptor.Describe(
            typeof(IDocumentStore),
            sp => sp.GetRequiredService<IDocumentStoreFactory>().Create(options),
            lifetime));

        return services;
    }

    // Note: For multiple database support, use AddKeyedLiteDocumentStore() instead (requires .NET 8+).

    /// <summary>
    /// Adds a keyed LiteDocumentStore document store for managing multiple databases (requires .NET 8+).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to</param>
    /// <param name="serviceKey">The key to identify this store instance</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="DocumentStoreOptions"/></param>
    /// <param name="lifetime">The service lifetime (default: Singleton)</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining</returns>
    public static IServiceCollection AddKeyedLiteDocumentStore(
        this IServiceCollection services,
        object serviceKey,
        Action<DocumentStoreOptions> configureOptions,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new DocumentStoreOptions();
        configureOptions(options);

        return services.AddKeyedLiteDocumentStore(serviceKey, options, lifetime);
    }

    /// <summary>
    /// Adds a keyed LiteDocumentStore document store for managing multiple databases (requires .NET 8+).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to</param>
    /// <param name="serviceKey">The key to identify this store instance</param>
    /// <param name="options">The pre-configured <see cref="DocumentStoreOptions"/></param>
    /// <param name="lifetime">The service lifetime (default: Singleton)</param>
    /// <returns>The <see cref="IServiceCollection"/> for method chaining</returns>
    public static IServiceCollection AddKeyedLiteDocumentStore(
        this IServiceCollection services,
        object serviceKey,
        DocumentStoreOptions options,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(options);

        // Register core dependencies as singletons (stateless, reusable)
        services.TryAddSingleton<IConnectionFactory, DefaultConnectionFactory>();
        services.TryAddSingleton<ITableNamingConvention, DefaultTableNamingConvention>();

        // Register the document store factory (shared across all keyed stores)
        services.TryAddSingleton<IDocumentStoreFactory>(sp => new DocumentStoreFactory(
            sp.GetRequiredService<IConnectionFactory>(),
            sp.GetRequiredService<ITableNamingConvention>(),
            sp.GetService<ILoggerFactory>()));

        // Register the keyed DocumentStore
        services.Add(ServiceDescriptor.DescribeKeyed(
            typeof(IDocumentStore),
            serviceKey,
            (sp, _) => sp.GetRequiredService<IDocumentStoreFactory>().Create(options),
            lifetime));

        return services;
    }
}
