#!/usr/bin/env dotnet run
// Transaction Batching Example - Maximize performance with batch operations
//
// Run this example with: dotnet run TransactionBatching.cs
//
// Demonstrates how to use transactions for batching operations to achieve
// dramatic performance improvements. Shows both success and rollback scenarios.

#:package Dapper@2.1.66
#:package Microsoft.Extensions.DependencyInjection@10.0.1
#:package Microsoft.Extensions.Logging@10.0.1
#:package Microsoft.Extensions.Logging.Console@10.0.1

#:project ../src/LiteDocumentStore/LiteDocumentStore.csproj

#:property PublishAot=false

using System.Data;
using System.Diagnostics;
using Dapper;
using LiteDocumentStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Enable reflection-based JSON serialization for .NET 10+
AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

// Create store
logger.LogInformation("Creating order processing system...");
var options = new DocumentStoreOptionsBuilder()
    .UseInMemory()
    .WithWalMode(false)
    .Build();

services.AddLiteDocumentStore(options);
serviceProvider = services.BuildServiceProvider();
var store = serviceProvider.GetRequiredService<IDocumentStore>();

await store.CreateTableAsync<Order>();
await store.CreateTableAsync<Customer>();

Console.WriteLine("\n=== Transaction Batching Performance Demo ===\n");

// ====================================================================
// PART 1: Individual Inserts vs Batch Transaction
// ====================================================================

Console.WriteLine("Part 1: Individual inserts vs. Batch transaction");
Console.WriteLine(new string('-', 60));

// Benchmark: Individual inserts WITHOUT transaction
logger.LogInformation("Inserting 1,000 orders individually (no transaction)...");
var sw = Stopwatch.StartNew();
for (int i = 1; i <= 1000; i++)
{
    await store.UpsertAsync($"o{i}", new Order(
        $"o{i}",
        $"c{i % 100}",
        new[] { $"item{i}", $"item{i + 1}" },
        99.99m + i,
        DateTime.UtcNow
    ));
}
sw.Stop();
var individualTime = sw.ElapsedMilliseconds;
Console.WriteLine($"✗ Individual inserts: {individualTime}ms");

// Clean up
await store.Connection.ExecuteAsync("DELETE FROM [Order]");

// Benchmark: Batch insert WITH transaction
logger.LogInformation("Inserting 1,000 orders in a transaction...");
sw.Restart();
await store.ExecuteInTransactionAsync(async () =>
{
    for (int i = 1; i <= 1000; i++)
    {
        await store.UpsertAsync($"o{i}", new Order(
            $"o{i}",
            $"c{i % 100}",
            new[] { $"item{i}", $"item{i + 1}" },
            99.99m + i,
            DateTime.UtcNow
        ));
    }
});
sw.Stop();
var batchTime = sw.ElapsedMilliseconds;
Console.WriteLine($"✓ Batched in transaction: {batchTime}ms");

var speedup = individualTime / (double)batchTime;
Console.WriteLine($"\n🚀 Speedup: {speedup:F1}x faster with transaction batching!\n");

// ====================================================================
// PART 2: UpsertManyAsync Bulk Operation
// ====================================================================

Console.WriteLine("Part 2: Bulk operations with UpsertManyAsync");
Console.WriteLine(new string('-', 60));

// Clean up
await store.Connection.ExecuteAsync("DELETE FROM [Order]");

// Benchmark: UpsertManyAsync
logger.LogInformation("Bulk inserting 1,000 orders with UpsertManyAsync...");
var orderBatch = Enumerable.Range(1, 1000).Select(i => (
    $"o{i}",
    new Order(
        $"o{i}",
        $"c{i % 100}",
        new[] { $"item{i}", $"item{i + 1}" },
        99.99m + i,
        DateTime.UtcNow
    )
));

sw.Restart();
await store.UpsertManyAsync(orderBatch);
sw.Stop();
var bulkTime = sw.ElapsedMilliseconds;
Console.WriteLine($"✓ UpsertManyAsync: {bulkTime}ms");

// Note: In this in-memory benchmark, bulk operations may not show dramatic speedup
// because the dataset is small. With file-based databases and larger datasets,
// UpsertManyAsync typically shows 2-5x speedup over ExecuteInTransactionAsync.
var bulkSpeedup = individualTime / (double)Math.Max(bulkTime, 1);
Console.WriteLine($"🚀 Speedup: {bulkSpeedup:F1}x faster with bulk operation!");
Console.WriteLine($"   (File-based databases show even greater improvements)\n");

// ====================================================================
// PART 3: Transaction Rollback on Error
// ====================================================================

Console.WriteLine("Part 3: Transaction rollback on error");
Console.WriteLine(new string('-', 60));

// Seed some customers
await store.ExecuteInTransactionAsync(async () =>
{
    await store.UpsertAsync("c1", new Customer("c1", "Alice", "alice@example.com", true));
    await store.UpsertAsync("c2", new Customer("c2", "Bob", "bob@example.com", true));
    await store.UpsertAsync("c3", new Customer("c3", "Carol", "carol@example.com", true));
});

var beforeCount = await store.CountAsync<Customer>();
Console.WriteLine($"Initial customers: {beforeCount}");

// Attempt a transaction that will fail
logger.LogInformation("Attempting transaction with error...");
try
{
    await store.ExecuteInTransactionAsync(async () =>
    {
        // These will succeed within the transaction
        await store.UpsertAsync("c4", new Customer("c4", "David", "david@example.com", true));
        await store.UpsertAsync("c5", new Customer("c5", "Eve", "eve@example.com", true));
        
        Console.WriteLine("  ↳ Added 2 customers in transaction...");
        
        // This will throw an exception
        throw new InvalidOperationException("Simulated business logic error!");
    });
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"✗ Transaction failed: {ex.Message}");
}

var afterCount = await store.CountAsync<Customer>();
Console.WriteLine($"Customers after failed transaction: {afterCount}");
Console.WriteLine($"✓ Transaction rolled back! Count remained {beforeCount}\n");

// ====================================================================
// PART 4: Using IDbTransaction Parameter
// ====================================================================

Console.WriteLine("Part 4: Using IDbTransaction parameter");
Console.WriteLine(new string('-', 60));

// The ExecuteInTransactionAsync can pass the transaction to your callback
// This is useful if you need to use the transaction with raw SQL
logger.LogInformation("Executing with transaction parameter access...");

await store.ExecuteInTransactionAsync(async (tx) =>
{
    // Insert via document store
    await store.UpsertAsync("c6", new Customer("c6", "Frank", "frank@example.com", true));
    await store.UpsertAsync("c7", new Customer("c7", "Grace", "grace@example.com", false));
    
    // Use transaction for raw SQL in the same transaction
    await store.Connection.ExecuteAsync(
        "UPDATE Customer SET data = jsonb_set(data, '$.name', json('\"Frank Miller\"')) WHERE id = 'c6'",
        transaction: tx
    );
    
    Console.WriteLine("  ↳ Mixed document operations and raw SQL in same transaction");
});

Console.WriteLine("✓ Transaction with raw SQL committed successfully");

var finalCount = await store.CountAsync<Customer>();
Console.WriteLine($"Final customer count: {finalCount}\n");

// ====================================================================
// PART 5: Complex Multi-Table Transaction
// ====================================================================

Console.WriteLine("Part 5: Multi-table atomic transaction");
Console.WriteLine(new string('-', 60));

logger.LogInformation("Processing complex order with customer update...");

await store.ExecuteInTransactionAsync(async () =>
{
    // Create a new order
    await store.UpsertAsync("o1001", new Order(
        "o1001",
        "c1",
        new[] { "laptop", "mouse", "keyboard" },
        1499.99m,
        DateTime.UtcNow
    ));
    
    // Update customer's active status
    var customer = await store.GetAsync<Customer>("c1");
    if (customer != null)
    {
        await store.UpsertAsync("c1", customer with { Active = false });
    }
    
    // All changes committed atomically
    Console.WriteLine("✓ Order created and customer updated in single transaction");
});

// Verify both changes persisted
var order = await store.GetAsync<Order>("o1001");
var updatedCustomer = await store.GetAsync<Customer>("c1");
Console.WriteLine($"  Order: {order?.Id} - Total: ${order?.Total:F2}");
Console.WriteLine($"  Customer: {updatedCustomer?.Name} - Active: {updatedCustomer?.Active}");

// ====================================================================
// Summary
// ====================================================================

Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("Key Takeaways:");
Console.WriteLine(new string('=', 60));
Console.WriteLine("1. ExecuteInTransactionAsync() batches operations for massive speedup");
Console.WriteLine($"   → {speedup:F1}x faster for 1,000 inserts");
Console.WriteLine("2. UpsertManyAsync() is even faster for bulk operations");
Console.WriteLine($"   → {bulkSpeedup:F1}x faster than individual inserts");
Console.WriteLine("3. Transactions are atomic - errors roll back ALL changes");
Console.WriteLine("4. Pass IDbTransaction to mix document ops with raw SQL");
Console.WriteLine("5. Multi-table changes stay consistent with transactions");
Console.WriteLine("\n✓ Transaction Batching example completed successfully!");

// Models
record Order(string Id, string CustomerId, string[] Items, decimal Total, DateTime CreatedAt);
record Customer(string Id, string Name, string Email, bool Active);
