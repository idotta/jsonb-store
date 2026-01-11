#!/usr/bin/env dotnet run
// Schema Migrations Example - Version and evolve your database schema
//
// Run this example with: dotnet run Migration.cs
//
// The migration system tracks schema versions and applies changes in order.
// Each migration runs in a transaction and is recorded in __store_migrations.

#:package Dapper@2.1.66
#:package Microsoft.Data.Sqlite@10.0.1
#:package Microsoft.Extensions.DependencyInjection@10.0.1
#:package Microsoft.Extensions.Logging@10.0.1
#:package Microsoft.Extensions.Logging.Console@10.0.1

#:project ../src/LiteDocumentStore/LiteDocumentStore.csproj

#:property PublishAot=false

using Dapper;
using LiteDocumentStore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Enable reflection-based JSON serialization for .NET 10+
AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Creating in-memory database for migration demo...");
var connectionString = "Data Source=:memory:";
using var connection = new SqliteConnection(connectionString);
await connection.OpenAsync();

// Configure connection pragmas
await connection.ExecuteAsync(@"
    PRAGMA journal_mode = WAL;
    PRAGMA foreign_keys = ON;
    PRAGMA synchronous = NORMAL;
");

// Create migration runner
var runner = new MigrationRunner(connection);

// Define migrations using version numbers (YYYYMMDDnnn format)
logger.LogInformation("\nDefining migrations...");

var migration1 = new Migration(
    version: 20260111001,
    name: "CreateInitialTables",
    upSql: @"
        -- Create Customer table
        CREATE TABLE Customer (
            id TEXT PRIMARY KEY,
            data BLOB NOT NULL
        );
        
        -- Create Order table
        CREATE TABLE [Order] (
            id TEXT PRIMARY KEY,
            data BLOB NOT NULL
        );
    ",
    downSql: @"
        DROP TABLE IF EXISTS [Order];
        DROP TABLE IF EXISTS Customer;
    "
);

var migration2 = new Migration(
    version: 20260111002,
    name: "AddCustomerEmailIndex",
    upSql: @"
        CREATE INDEX IF NOT EXISTS idx_customer_email 
        ON Customer(json_extract(data, '$.Email'));
    ",
    downSql: "DROP INDEX IF EXISTS idx_customer_email;"
);

var migration3 = new Migration(
    version: 20260111003,
    name: "AddOrderIndexes",
    upSql: @"
        -- Index on CustomerId for joins
        CREATE INDEX IF NOT EXISTS idx_order_customer 
        ON [Order](json_extract(data, '$.CustomerId'));
        
        -- Index on OrderDate for date range queries
        CREATE INDEX IF NOT EXISTS idx_order_date 
        ON [Order](json_extract(data, '$.OrderDate'));
    ",
    downSql: @"
        DROP INDEX IF EXISTS idx_order_date;
        DROP INDEX IF EXISTS idx_order_customer;
    "
);

var migration4 = new Migration(
    version: 20260111004,
    name: "AddVirtualColumnForCity",
    upSql: @"
        -- Add virtual column for fast city lookups
        ALTER TABLE Customer 
        ADD COLUMN city TEXT GENERATED ALWAYS AS (json_extract(data, '$.City')) VIRTUAL;
        
        CREATE INDEX IF NOT EXISTS idx_customer_city ON Customer(city);
    ",
    downSql: @"
        DROP INDEX IF EXISTS idx_customer_city;
        -- Note: SQLite doesn't support ALTER TABLE DROP COLUMN
        -- In production, you'd need to recreate the table without the column
    "
);

var allMigrations = new[] { migration1, migration2, migration3, migration4 };

Console.WriteLine($"Defined {allMigrations.Length} migrations:");
foreach (var m in allMigrations)
{
    Console.WriteLine($"  • {m.Version} - {m.Name}");
}

// Apply migrations
logger.LogInformation("\nApplying migrations...");
var appliedCount = await runner.ApplyMigrationsAsync(allMigrations);
Console.WriteLine($"✓ Applied {appliedCount} migration(s)\n");

// Check current version
var currentVersion = await runner.GetCurrentVersionAsync();
Console.WriteLine($"Current schema version: {currentVersion}");

// Get migration history
var appliedMigrations = await runner.GetAppliedMigrationsAsync();
Console.WriteLine($"\nMigration History ({appliedMigrations.Count()} applied):");
foreach (var record in appliedMigrations.OrderBy(m => m.Version))
{
    Console.WriteLine($"  • Version {record.Version}: {record.Name}");
    Console.WriteLine($"    Applied at: {record.AppliedAt:yyyy-MM-dd HH:mm:ss}");
}

// Inspect the schema using SchemaIntrospector
logger.LogInformation("\nInspecting database schema...");
var introspector = new SchemaIntrospector(connection);

var tables = await introspector.GetTablesAsync();
Console.WriteLine($"\nTables ({tables.Count()}):");
foreach (var table in tables)
{
    Console.WriteLine($"  • {table.Name}");

    // Show columns for each table
    var columns = await introspector.GetColumnsAsync(table.Name);
    foreach (var col in columns)
    {
        var pkIndicator = col.IsPrimaryKey ? " [PK]" : "";
        var notNullIndicator = col.NotNull ? " NOT NULL" : "";
        Console.WriteLine($"      - {col.Name}: {col.Type}{pkIndicator}{notNullIndicator}");
    }
}

var indexes = await introspector.GetIndexesAsync("Customer");
Console.WriteLine($"\nIndexes on Customer ({indexes.Count()}):");
foreach (var index in indexes)
{
    Console.WriteLine($"  • {index.Name}");
}

// Insert test data using raw SQL
logger.LogInformation("\nInserting test data...");

// Insert customers
await connection.ExecuteAsync(
    "INSERT OR REPLACE INTO Customer (id, data) VALUES (@Id, jsonb(@Data))",
    new[]
    {
        new { Id = "c1", Data = System.Text.Json.JsonSerializer.Serialize(new Customer("c1", "Alice Smith", "alice@example.com", "New York")) },
        new { Id = "c2", Data = System.Text.Json.JsonSerializer.Serialize(new Customer("c2", "Bob Johnson", "bob@example.com", "Los Angeles")) },
        new { Id = "c3", Data = System.Text.Json.JsonSerializer.Serialize(new Customer("c3", "Carol Williams", "carol@example.com", "Chicago")) }
    }
);

// Insert orders
await connection.ExecuteAsync(
    "INSERT OR REPLACE INTO [Order] (id, data) VALUES (@Id, jsonb(@Data))",
    new[]
    {
        new { Id = "o1", Data = System.Text.Json.JsonSerializer.Serialize(new Order("o1", "c1", DateTime.UtcNow.AddDays(-5), 150.00m)) },
        new { Id = "o2", Data = System.Text.Json.JsonSerializer.Serialize(new Order("o2", "c1", DateTime.UtcNow.AddDays(-3), 75.50m)) },
        new { Id = "o3", Data = System.Text.Json.JsonSerializer.Serialize(new Order("o3", "c2", DateTime.UtcNow.AddDays(-2), 220.00m)) }
    }
);

var customerCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Customer");
var orderCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM [Order]");
Console.WriteLine($"Inserted {customerCount} customers");
Console.WriteLine($"Inserted {orderCount} orders");

// Test that indexes are working
logger.LogInformation("\nTesting indexed queries...");
var aliceByEmail = await connection.QueryFirstOrDefaultAsync<string>(
    "SELECT json(data) FROM Customer WHERE json_extract(data, '$.Email') = @Email",
    new { Email = "alice@example.com" }
);
Console.WriteLine($"Found customer by email (using index): {aliceByEmail != null}");

var nyCustomers = await connection.QueryAsync<string>(
    "SELECT json(data) FROM Customer WHERE city = @City",
    new { City = "New York" }
);
Console.WriteLine($"Found customers in New York (using virtual column): {nyCustomers.Count()}");

// Demonstrate rollback
logger.LogInformation("\nDemonstrating rollback...");
Console.WriteLine($"Current version: {await runner.GetCurrentVersionAsync()}");

logger.LogInformation("Rolling back to version 20260111002...");
var rolledBackCount = await runner.RollbackToVersionAsync(20260111002, allMigrations);
Console.WriteLine($"✓ Rolled back {rolledBackCount} migration(s)");
Console.WriteLine($"Current version: {await runner.GetCurrentVersionAsync()}");

// Check what got rolled back
var remainingIndexes = await introspector.GetIndexesAsync("Customer");
Console.WriteLine($"\nIndexes remaining after rollback: {remainingIndexes.Count()}");
foreach (var index in remainingIndexes)
{
    Console.WriteLine($"  • {index.Name}");
}

// Check that Order indexes were removed
var orderIndexes = await introspector.GetIndexesAsync("Order");
Console.WriteLine($"Order indexes after rollback: {orderIndexes.Count()} (should be 0)");

// Note: We don't re-apply migrations here because SQLite doesn't support dropping columns
// In a real-world scenario, you would use table recreation for complex schema changes

// Database statistics
logger.LogInformation("\nDatabase statistics...");
var stats = await introspector.GetDatabaseStatisticsAsync();
Console.WriteLine($"  Page count: {stats.PageCount}");
Console.WriteLine($"  Page size: {stats.PageSize} bytes");
Console.WriteLine($"  Database size: {stats.DatabaseSizeBytes / 1024.0:F2} KB");

Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("MIGRATION SYSTEM SUMMARY");
Console.WriteLine(new string('=', 60));
Console.WriteLine("✓ Migrations run in order by version number");
Console.WriteLine("✓ Each migration runs in a transaction (atomic)");
Console.WriteLine("✓ Migration history tracked in __store_migrations");
Console.WriteLine("✓ Rollback support with down migrations");
Console.WriteLine("✓ Idempotent - safe to run multiple times");
Console.WriteLine("✓ SchemaIntrospector for schema inspection");
Console.WriteLine(new string('=', 60));

Console.WriteLine("\n✓ Migration example completed!");
Console.WriteLine("\nKey Takeaways:");
Console.WriteLine("  • Use timestamp-based version numbers (YYYYMMDDnnn)");
Console.WriteLine("  • Define both up and down SQL for each migration");
Console.WriteLine("  • MigrationRunner handles transaction management");
Console.WriteLine("  • ApplyMigrationsAsync runs pending migrations in order");
Console.WriteLine("  • RollbackToVersionAsync reverts to a previous version");
Console.WriteLine("  • SchemaIntrospector provides read-only schema queries");
Console.WriteLine("  • MigrationRunner and DocumentStore are independent but share connection");

record Customer(string Id, string Name, string Email, string City);
record Order(string Id, string CustomerId, DateTime OrderDate, decimal Amount);
