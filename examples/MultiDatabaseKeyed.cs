#!/usr/bin/env dotnet run
// Multi-Database with Keyed Services Example - Multiple database instances with keyed DI
//
// Run this example with: dotnet run MultiDatabaseKeyed.cs
// Or make it executable and run: ./MultiDatabaseKeyed.cs (Unix) or MultiDatabaseKeyed.cs (Windows PowerShell)
//
// This example demonstrates how to manage multiple database instances using keyed services (requires .NET 8+).
// Perfect for dependency injection scenarios where different components need different databases,
// and you want the DI container to manage the lifecycle and injection.

#:package Microsoft.Extensions.DependencyInjection@10.0.1
#:package Microsoft.Extensions.Logging@10.0.1
#:package Microsoft.Extensions.Logging.Console@10.0.1

#:project ../src/LiteDocumentStore/LiteDocumentStore.csproj

#:property PublishAot=false

using LiteDocumentStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Enable reflection-based JSON serialization for .NET 10+
AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);

Console.WriteLine("========================================");
Console.WriteLine("Multi-Database with Keyed Services Example");
Console.WriteLine("========================================\n");

// Create service collection with logging
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

// ========================================
// Register multiple keyed document stores
// ========================================

// Register customer database (US customers)
services.AddKeyedLiteDocumentStore(
    serviceKey: "UsCustomers",
    configureOptions: options => options.ConnectionString = "Data Source=:memory:;Cache=Shared");

// Register customer database (EU customers) 
services.AddKeyedLiteDocumentStore(
    serviceKey: "EuCustomers",
    configureOptions: options => options.ConnectionString = "Data Source=:memory:;Cache=Shared");

// Register product catalog database
services.AddKeyedLiteDocumentStore(
    serviceKey: "Products",
    configureOptions: options => options.ConnectionString = "Data Source=:memory:;Cache=Shared");

// Register orders database with scoped lifetime (for per-request isolation)
services.AddKeyedLiteDocumentStore(
    serviceKey: "Orders",
    configureOptions: options => options.ConnectionString = "Data Source=:memory:;Cache=Shared",
    lifetime: ServiceLifetime.Scoped);

// Build service provider
var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

// ========================================
// Retrieve keyed stores and use them
// ========================================

logger.LogInformation("Retrieving keyed document stores...");

// Get US customers store
var usCustomersStore = serviceProvider.GetRequiredKeyedService<IDocumentStore>("UsCustomers");
await usCustomersStore.CreateTableAsync<Customer>();

// Get EU customers store
var euCustomersStore = serviceProvider.GetRequiredKeyedService<IDocumentStore>("EuCustomers");
await euCustomersStore.CreateTableAsync<Customer>();

// Get products store
var productsStore = serviceProvider.GetRequiredKeyedService<IDocumentStore>("Products");
await productsStore.CreateTableAsync<Product>();

// Create a scope for the scoped orders store
using var scope = serviceProvider.CreateScope();
var ordersStore = scope.ServiceProvider.GetRequiredKeyedService<IDocumentStore>("Orders");
await ordersStore.CreateTableAsync<Order>();

// ========================================
// Populate US customers
// ========================================

Console.WriteLine("\nUS Customers Database:");
await usCustomersStore.UpsertAsync("1", new Customer("1", "Alice Smith", "alice@acmeus.com", 30, "New York"));
await usCustomersStore.UpsertAsync("2", new Customer("2", "Bob Johnson", "bob@acmeus.com", 25, "Los Angeles"));
await usCustomersStore.UpsertAsync("3", new Customer("3", "Charlie Brown", "charlie@acmeus.com", 35, "Chicago"));

var usCustomers = (await usCustomersStore.GetAllAsync<Customer>()).ToList();
foreach (var customer in usCustomers)
{
    Console.WriteLine($"  - {customer.Name} ({customer.Email}) - {customer.City}");
}
Console.WriteLine($"Total: {usCustomers.Count} customers");

// ========================================
// Populate EU customers
// ========================================

Console.WriteLine("\nEU Customers Database:");
await euCustomersStore.UpsertAsync("1", new Customer("1", "Claire Dubois", "claire@acmeeu.com", 28, "Paris"));
await euCustomersStore.UpsertAsync("2", new Customer("2", "David Schmidt", "david@acmeeu.com", 32, "Berlin"));
await euCustomersStore.UpsertAsync("3", new Customer("3", "Elena Rossi", "elena@acmeeu.com", 27, "Rome"));
await euCustomersStore.UpsertAsync("4", new Customer("4", "Fernando Garcia", "fernando@acmeeu.com", 29, "Madrid"));

var euCustomers = (await euCustomersStore.GetAllAsync<Customer>()).ToList();
foreach (var customer in euCustomers)
{
    Console.WriteLine($"  - {customer.Name} ({customer.Email}) - {customer.City}");
}
Console.WriteLine($"Total: {euCustomers.Count} customers");

// ========================================
// Populate products
// ========================================

Console.WriteLine("\nProducts Database:");
await productsStore.UpsertAsync("p1", new Product("p1", "Laptop", 999.99m, "Electronics"));
await productsStore.UpsertAsync("p2", new Product("p2", "Mouse", 29.99m, "Electronics"));
await productsStore.UpsertAsync("p3", new Product("p3", "Desk Chair", 199.99m, "Furniture"));
await productsStore.UpsertAsync("p4", new Product("p4", "Monitor", 349.99m, "Electronics"));

var products = (await productsStore.GetAllAsync<Product>()).ToList();
foreach (var product in products)
{
    Console.WriteLine($"  - {product.Name}: ${product.Price} ({product.Category})");
}
Console.WriteLine($"Total: {products.Count} products");

// ========================================
// Populate orders
// ========================================

Console.WriteLine("\nOrders Database:");
await ordersStore.UpsertAsync("o1", new Order("o1", "1", "p1", 1, 999.99m));
await ordersStore.UpsertAsync("o2", new Order("o2", "2", "p2", 2, 59.98m));
await ordersStore.UpsertAsync("o3", new Order("o3", "3", "p3", 1, 199.99m));

var orders = (await ordersStore.GetAllAsync<Order>()).ToList();
foreach (var order in orders)
{
    Console.WriteLine($"  - Order {order.Id}: Customer {order.CustomerId}, Product {order.ProductId}, Qty: {order.Quantity}, Total: ${order.Total}");
}
Console.WriteLine($"Total: {orders.Count} orders");

// ========================================
// Demonstrate service lifetimes
// ========================================

Console.WriteLine("\n========================================");
Console.WriteLine("Service Lifetime Demonstration");
Console.WriteLine("========================================");

// Singleton stores - same instance across scopes
using (var scope1 = serviceProvider.CreateScope())
{
    var store1 = scope1.ServiceProvider.GetRequiredKeyedService<IDocumentStore>("UsCustomers");
    Console.WriteLine($"Scope 1 - US Customers store: {store1.GetHashCode()}");
}

using (var scope2 = serviceProvider.CreateScope())
{
    var store2 = scope2.ServiceProvider.GetRequiredKeyedService<IDocumentStore>("UsCustomers");
    Console.WriteLine($"Scope 2 - US Customers store: {store2.GetHashCode()} (same instance as Scope 1)");
}

// Scoped store - different instance per scope
using (var scope3 = serviceProvider.CreateScope())
{
    var ordersStore1 = scope3.ServiceProvider.GetRequiredKeyedService<IDocumentStore>("Orders");
    Console.WriteLine($"\nScope 3 - Orders store: {ordersStore1.GetHashCode()}");
}

using (var scope4 = serviceProvider.CreateScope())
{
    var ordersStore2 = scope4.ServiceProvider.GetRequiredKeyedService<IDocumentStore>("Orders");
    Console.WriteLine($"Scope 4 - Orders store: {ordersStore2.GetHashCode()} (different instance from Scope 3)");
}

// ========================================
// Demonstrate typed service classes
// ========================================

Console.WriteLine("\n========================================");
Console.WriteLine("Typed Service Classes");
Console.WriteLine("========================================");

// Register typed services that depend on specific keyed stores
services.AddScoped<CustomerService>();
services.AddScoped<OrderService>();

// Build a new provider with the typed services
var typedProvider = services.BuildServiceProvider();

// Create a new scope and set up the tables for the typed services
using var typedScope = typedProvider.CreateScope();

// Get the stores and ensure tables exist (typed services need them)
var typedUsStore = typedScope.ServiceProvider.GetRequiredKeyedService<IDocumentStore>("UsCustomers");
var typedOrdersStore = typedScope.ServiceProvider.GetRequiredKeyedService<IDocumentStore>("Orders");

// Tables already exist from earlier, but in case they don't in the typed services scope:
// (This is a demonstration - in real apps, you'd run migrations on startup)
await typedUsStore.CreateTableAsync<Customer>();
await typedOrdersStore.CreateTableAsync<Order>();

// Seed some data for the typed services demo
await typedUsStore.UpsertAsync("100", new Customer("100", "Typed Service Customer", "typed@example.com", 40, "Seattle"));
await typedOrdersStore.UpsertAsync("o100", new Order("o100", "100", "p1", 1, 999.99m));

// Now use the typed services
var customerService = typedScope.ServiceProvider.GetRequiredService<CustomerService>();
var orderService = typedScope.ServiceProvider.GetRequiredService<OrderService>();

var activeCustomers = await customerService.GetActiveCustomersAsync();
Console.WriteLine($"\nCustomerService found {activeCustomers.Count()} active US customers");

var recentOrders = await orderService.GetRecentOrdersAsync();
Console.WriteLine($"OrderService found {recentOrders.Count()} recent orders");

// ========================================
// Summary
// ========================================

Console.WriteLine("\n========================================");
Console.WriteLine("Summary");
Console.WriteLine("========================================");
Console.WriteLine($"✓ Registered 4 keyed document stores in DI container");
Console.WriteLine($"✓ US Customers: {usCustomers.Count} customers (Singleton)");
Console.WriteLine($"✓ EU Customers: {euCustomers.Count} customers (Singleton)");
Console.WriteLine($"✓ Products: {products.Count} items (Singleton)");
Console.WriteLine($"✓ Orders: {orders.Count} transactions (Scoped)");
Console.WriteLine($"✓ Demonstrated typed services with keyed dependencies");
Console.WriteLine();
Console.WriteLine("Key Takeaways:");
Console.WriteLine("- Keyed services allow multiple stores in one DI container");
Console.WriteLine("- Use [FromKeyedServices] attribute to inject specific stores");
Console.WriteLine("- Singleton lifetime: single instance shared across the app");
Console.WriteLine("- Scoped lifetime: new instance per request/scope");
Console.WriteLine("- Perfect for clean architecture with domain-separated databases");
Console.WriteLine("- Type-safe dependency injection with compile-time checking");

// ========================================
// Model Definitions
// ========================================

record Customer(string Id, string Name, string Email, int Age, string City);
record Product(string Id, string Name, decimal Price, string Category);
record Order(string Id, string CustomerId, string ProductId, int Quantity, decimal Total);

// ========================================
// Typed Service Classes
// ========================================

class CustomerService
{
    private readonly IDocumentStore _store;

    public CustomerService([FromKeyedServices("UsCustomers")] IDocumentStore store)
    {
        _store = store;
    }

    public async Task<IEnumerable<Customer>> GetActiveCustomersAsync()
    {
        // In a real app, you might filter by some "active" criteria
        return await _store.GetAllAsync<Customer>();
    }
}

class OrderService
{
    private readonly IDocumentStore _store;

    public OrderService([FromKeyedServices("Orders")] IDocumentStore store)
    {
        _store = store;
    }

    public async Task<IEnumerable<Order>> GetRecentOrdersAsync()
    {
        // In a real app, you might filter by date
        return await _store.GetAllAsync<Order>();
    }
}
