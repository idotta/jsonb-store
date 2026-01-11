#!/usr/bin/env dotnet run
// Multi-Database Example - Multiple database instances with IDocumentStoreFactory
//
// Run this example with: dotnet run MultiDatabase.cs
// Or make it executable and run: ./MultiDatabase.cs (Unix) or MultiDatabase.cs (Windows PowerShell)
//
// This example demonstrates how to manage multiple database instances using IDocumentStoreFactory.
// Perfect for scenarios where you need separate databases for different tenants, environments,
// or data domains within the same application.

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
Console.WriteLine("Multi-Database Example");
Console.WriteLine("========================================\n");

// Create service collection with logging
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

// Register the document store factory
// The factory is registered as a singleton and reused to create multiple store instances
services.AddLiteDocumentStore(new DocumentStoreOptions
{
    ConnectionString = "Data Source=:memory:"
});

var serviceProvider = services.BuildServiceProvider();
var factory = serviceProvider.GetRequiredService<IDocumentStoreFactory>();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

// ========================================
// Scenario: Multi-tenant application with separate databases
// ========================================

logger.LogInformation("Creating separate databases for two tenants...");

// Create database for Tenant A (US customers)
var tenantAOptions = new DocumentStoreOptionsBuilder()
    .UseSharedInMemory("TenantA")
    .WithWalMode(false) // WAL not supported for in-memory
    .Build();

var tenantAStore = factory.Create(tenantAOptions);
await tenantAStore.CreateTableAsync<Customer>();

// Create database for Tenant B (EU customers)
var tenantBOptions = new DocumentStoreOptionsBuilder()
    .UseSharedInMemory("TenantB")
    .WithWalMode(false)
    .Build();

var tenantBStore = factory.Create(tenantBOptions);
await tenantBStore.CreateTableAsync<Customer>();

// Insert customers into Tenant A database
Console.WriteLine("\nTenant A (US Customers):");
await tenantAStore.UpsertAsync("1", new Customer("1", "Alice Smith", "alice@acmeus.com", 30, "New York"));
await tenantAStore.UpsertAsync("2", new Customer("2", "Bob Johnson", "bob@acmeus.com", 25, "Los Angeles"));

var tenantACustomers = (await tenantAStore.GetAllAsync<Customer>()).ToList();
foreach (var customer in tenantACustomers)
{
    Console.WriteLine($"  - {customer.Name} ({customer.Email}) - {customer.City}");
}
Console.WriteLine($"Total: {tenantACustomers.Count} customers");

// Insert customers into Tenant B database
Console.WriteLine("\nTenant B (EU Customers):");
await tenantBStore.UpsertAsync("1", new Customer("1", "Claire Dubois", "claire@acmeeu.com", 28, "Paris"));
await tenantBStore.UpsertAsync("2", new Customer("2", "David Schmidt", "david@acmeeu.com", 32, "Berlin"));
await tenantBStore.UpsertAsync("3", new Customer("3", "Elena Rossi", "elena@acmeeu.com", 27, "Rome"));

var tenantBCustomers = (await tenantBStore.GetAllAsync<Customer>()).ToList();
foreach (var customer in tenantBCustomers)
{
    Console.WriteLine($"  - {customer.Name} ({customer.Email}) - {customer.City}");
}
Console.WriteLine($"Total: {tenantBCustomers.Count} customers");

// ========================================
// Scenario: Separate databases for different data domains
// ========================================

logger.LogInformation("\nCreating separate databases for Products and Orders...");

// Create database for Products catalog
var productsOptions = new DocumentStoreOptionsBuilder()
    .UseSharedInMemory("Products")
    .WithWalMode(false)
    .Build();

var productsStore = factory.Create(productsOptions);
await productsStore.CreateTableAsync<Product>();

// Create database for Orders/transactions
var ordersOptions = new DocumentStoreOptionsBuilder()
    .UseSharedInMemory("Orders")
    .WithWalMode(false)
    .Build();

var ordersStore = factory.Create(ordersOptions);
await ordersStore.CreateTableAsync<Order>();

// Populate product catalog
Console.WriteLine("\nProduct Catalog Database:");
await productsStore.UpsertAsync("p1", new Product("p1", "Laptop", 999.99m, "Electronics"));
await productsStore.UpsertAsync("p2", new Product("p2", "Mouse", 29.99m, "Electronics"));
await productsStore.UpsertAsync("p3", new Product("p3", "Desk Chair", 199.99m, "Furniture"));

var products = (await productsStore.GetAllAsync<Product>()).ToList();
foreach (var product in products)
{
    Console.WriteLine($"  - {product.Name}: ${product.Price} ({product.Category})");
}

// Populate orders database
Console.WriteLine("\nOrders Database:");
await ordersStore.UpsertAsync("o1", new Order("o1", "1", "p1", 1, 999.99m));
await ordersStore.UpsertAsync("o2", new Order("o2", "2", "p2", 2, 59.98m));

var orders = (await ordersStore.GetAllAsync<Order>()).ToList();
foreach (var order in orders)
{
    Console.WriteLine($"  - Order {order.Id}: Customer {order.CustomerId}, Product {order.ProductId}, Total: ${order.Total}");
}

// ========================================
// Summary
// ========================================

Console.WriteLine("\n========================================");
Console.WriteLine("Summary");
Console.WriteLine("========================================");
Console.WriteLine($"✓ Created 4 separate databases using one factory");
Console.WriteLine($"✓ Tenant A: {tenantACustomers.Count} customers");
Console.WriteLine($"✓ Tenant B: {tenantBCustomers.Count} customers");
Console.WriteLine($"✓ Products: {products.Count} items");
Console.WriteLine($"✓ Orders: {orders.Count} transactions");
Console.WriteLine();
Console.WriteLine("Key Takeaways:");
Console.WriteLine("- IDocumentStoreFactory creates independent store instances");
Console.WriteLine("- Each store manages its own connection and lifetime");
Console.WriteLine("- Perfect for multi-tenant or domain-separated architectures");
Console.WriteLine("- Factory is stateless and can be safely registered as singleton");
Console.WriteLine("- Each store can have different configuration (WAL, pragmas, etc.)");

// Cleanup
await tenantAStore.DisposeAsync();
await tenantBStore.DisposeAsync();
await productsStore.DisposeAsync();
await ordersStore.DisposeAsync();

// ========================================
// Model Definitions
// ========================================

record Customer(string Id, string Name, string Email, int Age, string City);
record Product(string Id, string Name, decimal Price, string Category);
record Order(string Id, string CustomerId, string ProductId, int Quantity, decimal Total);
