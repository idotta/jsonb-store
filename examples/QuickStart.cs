#!/usr/bin/env dotnet run
// Quick Start Example - Basic CRUD operations with LiteDocumentStore
//
// Run this example with: dotnet run QuickStart.cs
// Or make it executable and run: ./QuickStart.cs (Unix) or QuickStart.cs (Windows PowerShell)

#:package Microsoft.Extensions.DependencyInjection@10.0.1
#:package Microsoft.Extensions.Logging@10.0.1
#:package Microsoft.Extensions.Logging.Console@10.0.1

#:project ../src/LiteDocumentStore/LiteDocumentStore.csproj

#:property PublishAot=false

using System.Data;
using LiteDocumentStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Enable reflection-based JSON serialization for .NET 10+
AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);

// Create service collection
var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

// Create a simple console logger
var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

// 1. Create a document store
logger.LogInformation("Creating document store...");
var options = new DocumentStoreOptionsBuilder()
    .UseInMemory() // Use in-memory for this example
    .WithWalMode(false) // WAL not supported for in-memory
    .Build();

services.AddLiteDocumentStore(options);
serviceProvider = services.BuildServiceProvider();
var store = serviceProvider.GetRequiredService<IDocumentStore>();

// 2. Create a table
logger.LogInformation("Creating Customer table...");
await store.CreateTableAsync<Customer>();

// 3. Insert/Update documents (Upsert)
logger.LogInformation("Inserting customers...");
await store.UpsertAsync("1", new Customer("1", "Alice Smith", "alice@example.com", 30, "New York"));
await store.UpsertAsync("2", new Customer("2", "Bob Johnson", "bob@example.com", 25, "Los Angeles"));
await store.UpsertAsync("3", new Customer("3", "Carol Williams", "carol@example.com", 35, "Chicago"));

// 4. Get a single document by ID
logger.LogInformation("Retrieving customer with ID '1'...");
var customer = await store.GetAsync<Customer>("1");
if (customer != null)
{
    Console.WriteLine($"Found: {customer.Name} ({customer.Email}) - Age {customer.Age}, lives in {customer.City}");
}

// 5. Update an existing document
logger.LogInformation("Updating customer '1'...");
await store.UpsertAsync("1", customer! with { Age = 31, City = "Boston" });

// 6. Verify the update
var updated = await store.GetAsync<Customer>("1");
if (updated != null)
{
    Console.WriteLine($"After update: {updated.Name} - Age {updated.Age}, lives in {updated.City}");
}

// 7. Get all documents
logger.LogInformation("Retrieving all customers...");
var allCustomers = (await store.GetAllAsync<Customer>()).ToList();
Console.WriteLine($"\nAll customers ({allCustomers.Count} total):");
foreach (var c in allCustomers)
{
    Console.WriteLine($"  - {c.Name} ({c.Email})");
}

// 8. Check if a document exists
var exists = await store.ExistsAsync<Customer>("2");
Console.WriteLine($"\nDoes customer '2' exist? {exists}");

// 9. Count documents
var count = await store.CountAsync<Customer>();
Console.WriteLine($"Total customers: {count}");

// 10. Delete a document
logger.LogInformation("Deleting customer '2'...");
var deleted = await store.DeleteAsync<Customer>("2");
Console.WriteLine($"Deleted: {deleted}");

// 11. Verify deletion
var remaining = (await store.GetAllAsync<Customer>()).ToList();
Console.WriteLine($"Remaining customers: {remaining.Count}");

// 12. Bulk operations with transactions
logger.LogInformation("Bulk insert with transaction...");
await store.ExecuteInTransactionAsync(async () =>
{
    await store.UpsertAsync("4", new Customer("4", "David Brown", "david@example.com", 40, "Houston"));
    await store.UpsertAsync("5", new Customer("5", "Eve Davis", "eve@example.com", 28, "Phoenix"));
    await store.UpsertAsync("6", new Customer("6", "Frank Miller", "frank@example.com", 33, "Philadelphia"));
});

// 13. Final count
var finalCount = await store.CountAsync<Customer>();
Console.WriteLine($"\nFinal customer count: {finalCount}");

Console.WriteLine("\n✓ Quick Start example completed successfully!");

record Customer(string Id, string Name, string Email, int Age, string City);
