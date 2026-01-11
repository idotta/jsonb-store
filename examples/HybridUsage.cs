#!/usr/bin/env dotnet run
// Hybrid Usage Example - Mix document storage with traditional SQL
//
// Run this example with: dotnet run HybridUsage.cs
//
// LiteDocumentStore is a HYBRID library - you get convenient document storage
// AND full access to SQLite's relational features via the Connection property.

#:package Dapper@2.1.66
#:package Microsoft.Data.Sqlite@10.0.1
#:package Microsoft.Extensions.DependencyInjection@10.0.1
#:package Microsoft.Extensions.Logging@10.0.1
#:package Microsoft.Extensions.Logging.Console@10.0.1

#:project ../src/LiteDocumentStore/LiteDocumentStore.csproj

#:property PublishAot=false

using Dapper;
using LiteDocumentStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Enable reflection-based JSON serialization for .NET 10+
AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

// Create store
var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Creating hybrid database...");
var options = new DocumentStoreOptionsBuilder()
    .UseInMemory()
    .WithWalMode(false) // WAL not supported for in-memory
    .WithForeignKeys(true) // Important for relational integrity!
    .Build();

services.AddLiteDocumentStore(options);
serviceProvider = services.BuildServiceProvider();
var store = serviceProvider.GetRequiredService<IDocumentStore>();

// Create document tables
logger.LogInformation("Creating document tables...");
await store.CreateTableAsync<Customer>();
await store.CreateTableAsync<Order>();

// Insert customers using document store
logger.LogInformation("Inserting customers (document store)...");
await store.UpsertAsync("c1", new Customer("c1", "Alice Smith", "alice@example.com", "New York"));
await store.UpsertAsync("c2", new Customer("c2", "Bob Johnson", "bob@example.com", "Los Angeles"));
await store.UpsertAsync("c3", new Customer("c3", "Carol Williams", "carol@example.com", "Chicago"));

// Insert orders using document store
logger.LogInformation("Inserting orders (document store)...");
await store.UpsertAsync("o1", new Order("o1", "c1", DateTime.UtcNow.AddDays(-5), 150.00m, "Shipped"));
await store.UpsertAsync("o2", new Order("o2", "c1", DateTime.UtcNow.AddDays(-3), 75.50m, "Delivered"));
await store.UpsertAsync("o3", new Order("o3", "c2", DateTime.UtcNow.AddDays(-2), 220.00m, "Processing"));
await store.UpsertAsync("o4", new Order("o4", "c3", DateTime.UtcNow.AddDays(-1), 99.99m, "Shipped"));

// Now use RAW SQL for complex queries
logger.LogInformation("\nUsing raw SQL for relational queries...");

// Query 1: Join orders with customers using JSON extraction
var ordersWithCustomers = await store.Connection.QueryAsync<OrderWithCustomer>(@"
    SELECT 
        o.id as OrderId,
        json_extract(o.data, '$.OrderDate') as OrderDate,
        json_extract(o.data, '$.TotalAmount') as TotalAmount,
        json_extract(c.data, '$.Name') as CustomerName,
        json_extract(c.data, '$.Email') as CustomerEmail
    FROM [Order] o
    INNER JOIN Customer c ON json_extract(o.data, '$.CustomerId') = c.id
    ORDER BY json_extract(o.data, '$.OrderDate') DESC
");

Console.WriteLine("\nOrders with Customer Details:");
foreach (var order in ordersWithCustomers)
{
    Console.WriteLine($"  Order {order.OrderId}: ${order.TotalAmount:F2} - {order.CustomerName} ({order.CustomerEmail})");
}

// Query 2: Aggregate queries
var customerSpending = await store.Connection.QueryAsync<(string Name, string Email, decimal Total)>(@"
    SELECT 
        json_extract(c.data, '$.Name') as Name,
        json_extract(c.data, '$.Email') as Email,
        SUM(CAST(json_extract(o.data, '$.TotalAmount') as REAL)) as Total
    FROM Customer c
    LEFT JOIN [Order] o ON c.id = json_extract(o.data, '$.CustomerId')
    GROUP BY c.id
    ORDER BY Total DESC
");

Console.WriteLine("\nCustomer Spending Report:");
foreach (var (name, email, total) in customerSpending)
{
    Console.WriteLine($"  {name} ({email}): ${total:F2}");
}

// Query 3: Create a view over JSON data
logger.LogInformation("\nCreating a SQL view over JSON data...");
await store.Connection.ExecuteAsync(@"
    CREATE VIEW IF NOT EXISTS v_customer_orders AS
    SELECT 
        c.id as customer_id,
        json_extract(c.data, '$.Name') as customer_name,
        json_extract(c.data, '$.City') as city,
        o.id as order_id,
        json_extract(o.data, '$.OrderDate') as order_date,
        json_extract(o.data, '$.TotalAmount') as total_amount,
        json_extract(o.data, '$.Status') as status
    FROM Customer c
    LEFT JOIN [Order] o ON c.id = json_extract(o.data, '$.CustomerId')
");

// Query the view
var viewResults = await store.Connection.QueryAsync<dynamic>("SELECT * FROM v_customer_orders WHERE city = 'New York'");
Console.WriteLine("\nOrders from New York (via view):");
foreach (var row in viewResults)
{
    Console.WriteLine($"  {row.customer_name} - Order {row.order_id}: ${row.total_amount} ({row.status})");
}

// Query 4: Mix document and traditional tables
logger.LogInformation("\nCreating a traditional relational table alongside document tables...");
await store.Connection.ExecuteAsync(@"
    CREATE TABLE IF NOT EXISTS product_inventory (
        product_id TEXT PRIMARY KEY,
        quantity INTEGER NOT NULL,
        warehouse_location TEXT NOT NULL,
        last_updated DATETIME DEFAULT CURRENT_TIMESTAMP
    )
");

await store.Connection.ExecuteAsync(@"
    INSERT INTO product_inventory (product_id, quantity, warehouse_location) VALUES
    ('prod1', 100, 'Warehouse A'),
    ('prod2', 50, 'Warehouse B'),
    ('prod3', 200, 'Warehouse A')
");

var inventory = await store.Connection.QueryAsync<dynamic>("SELECT * FROM product_inventory");
Console.WriteLine("\nTraditional table (product_inventory):");
foreach (var item in inventory)
{
    Console.WriteLine($"  Product {item.product_id}: {item.quantity} units in {item.warehouse_location}");
}

// Query 5: Advanced filtering with SQLite functions
logger.LogInformation("\nAdvanced filtering with SQLite functions...");
var advancedResults = await store.Connection.QueryAsync<dynamic>(@"
    SELECT 
        id,
        json_extract(data, '$.Name') as name,
        json_extract(data, '$.Email') as email,
        json_extract(data, '$.City') as city
    FROM Customer 
    WHERE LOWER(json_extract(data, '$.Name')) LIKE '%alice%' 
       OR LOWER(json_extract(data, '$.Name')) LIKE '%bob%'
");

Console.WriteLine("\nAdvanced filter results for names containing 'alice' or 'bob':");
foreach (var result in advancedResults)
{
    Console.WriteLine($"  {result.name} from {result.city} ({result.email})");
}

Console.WriteLine("\n✓ Hybrid Usage example completed!");
Console.WriteLine("\nKey Takeaways:");
Console.WriteLine("  • Use document store for convenience: CreateTable, Upsert, Query");
Console.WriteLine("  • Use raw SQL for complex queries: joins, aggregations, views");
Console.WriteLine("  • Mix document tables with traditional relational tables");
Console.WriteLine("  • Access full SQLite features: triggers, CTEs, window functions");
Console.WriteLine("  • Best of both worlds: flexible schemas + relational power");

// Define models
record Customer(string Id, string Name, string Email, string City);
record Order(string Id, string CustomerId, DateTime OrderDate, decimal TotalAmount, string Status);
record OrderWithCustomer(string OrderId, string OrderDate, double TotalAmount, string CustomerName, string CustomerEmail);