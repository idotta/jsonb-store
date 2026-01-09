# Schema Migrations Example

This example demonstrates how to use the migration system to version and evolve your database schema.

## Basic Usage

### 1. Define Migrations

Migrations are defined using version numbers (typically timestamps) to ensure ordered execution:

```csharp
using LiteDocumentStore;

// Migration 1: Create initial tables
var migration1 = new Migration(
    version: 20260109001,
    name: "CreateInitialTables",
    upSql: @"
        CREATE TABLE Customer (
            id TEXT PRIMARY KEY,
            data BLOB NOT NULL
        );
        CREATE TABLE [Order] (
            id TEXT PRIMARY KEY,
            data BLOB NOT NULL
        );
    ",
    downSql: @"
        DROP TABLE [Order];
        DROP TABLE Customer;
    "
);

// Migration 2: Add indexes
var migration2 = new Migration(
    version: 20260109002,
    name: "AddCustomerEmailIndex",
    upSql: @"
        CREATE INDEX idx_customer_email 
        ON Customer(json_extract(data, '$.email'));
    ",
    downSql: "DROP INDEX idx_customer_email;"
);
```

### 2. Apply Migrations

Use the `MigrationRunner` to apply migrations to your database:

```csharp
using Microsoft.Data.Sqlite;
using LiteDocumentStore;

// Open connection
using var connection = new SqliteConnection("Data Source=myapp.db");
await connection.OpenAsync();

// Create migration runner
var runner = new MigrationRunner(connection);

// Apply migrations
var migrations = new[] { migration1, migration2 };
var appliedCount = await runner.ApplyMigrationsAsync(migrations);

Console.WriteLine($"Applied {appliedCount} migrations");
```

### 3. Check Migration Status

```csharp
// Get current version
var currentVersion = await runner.GetCurrentVersionAsync();
Console.WriteLine($"Current schema version: {currentVersion}");

// Get all applied migrations
var appliedMigrations = await runner.GetAppliedMigrationsAsync();
foreach (var migration in appliedMigrations)
{
    Console.WriteLine($"Version {migration.Version}: {migration.Name}");
}
```

### 4. Rollback Migrations

```csharp
// Rollback to a specific version
var rolledBackCount = await runner.RollbackToVersionAsync(20260109001, migrations);
Console.WriteLine($"Rolled back {rolledBackCount} migrations");

// Or rollback a specific migration
var rolledBack = await runner.RollbackMigrationAsync(migration2);
```

## Custom Migrations

You can create custom migration classes by implementing `IMigration`:

```csharp
public class AddVirtualColumnMigration : IMigration
{
    public long Version => 20260109003;
    public string Name => "AddVirtualEmailColumn";

    public async Task UpAsync(SqliteConnection connection)
    {
        await connection.ExecuteAsync(@"
            ALTER TABLE Customer 
            ADD COLUMN email TEXT GENERATED ALWAYS AS (json_extract(data, '$.email')) VIRTUAL;
            
            CREATE INDEX idx_customer_email_virtual ON Customer(email);
        ");
    }

    public async Task DownAsync(SqliteConnection connection)
    {
        await connection.ExecuteAsync(@"
            DROP INDEX idx_customer_email_virtual;
            -- Note: SQLite doesn't support dropping columns directly,
            -- would need to recreate table without the column
        ");
    }
}
```

## Schema Introspection

The `SchemaIntrospector` class provides utilities to inspect your database schema. Simply pass the connection:

```csharp
using var connection = new SqliteConnection("Data Source=myapp.db");
await connection.OpenAsync();

var introspector = new SchemaIntrospector(connection);

// Get all tables
var tables = await introspector.GetTablesAsync();
foreach (var table in tables)
{
    Console.WriteLine($"Table: {table.Name}");
}

// Check if a table exists
var exists = await introspector.TableExistsAsync("Customer");

// Get columns for a table
var columns = await introspector.GetColumnsAsync("Customer");
foreach (var column in columns)
{
    Console.WriteLine($"  {column.Name} ({column.Type})");
}

// Get indexes
var indexes = await introspector.GetIndexesAsync("Customer");
foreach (var index in indexes)
{
    Console.WriteLine($"Index: {index.Name}");
}

// Get database statistics
var stats = await introspector.GetDatabaseStatisticsAsync();
Console.WriteLine($"Database size: {stats.DatabaseSizeBytes / 1024} KB");
Console.WriteLine($"Page count: {stats.PageCount}");
Console.WriteLine($"Page size: {stats.PageSize} bytes");
```

## Best Practices

1. **Version Numbering**: Use timestamp-based version numbers (YYYYMMDDnnn) to avoid conflicts
   - Example: 20260109001 = January 9, 2026, first migration of the day

2. **Idempotent Up Scripts**: Make sure migrations can be safely re-run
   - Use `CREATE TABLE IF NOT EXISTS`
   - Use `CREATE INDEX IF NOT EXISTS`

3. **Atomic Migrations**: Each migration runs in a transaction
   - If any part fails, the entire migration is rolled back
   - The migration is only recorded if successful

4. **Test Rollbacks**: Always test your down migrations
   - Ensure they properly revert the changes
   - Document limitations (e.g., SQLite can't drop columns)

5. **Separation of Concerns**: Keep concerns cleanly separated
   - Use `MigrationRunner` for schema migrations (pass connection)
   - Use `IDocumentStore` for document CRUD operations
   - Use `SchemaIntrospector` for schema queries (pass connection)
   - All three are independent - no coupling between them

## Integration with DocumentStore

Migrations and schema introspection are separate from the document store, but work seamlessly together by sharing the same connection:

```csharp
// Open connection once
using var connection = new SqliteConnection("Data Source=myapp.db");
await connection.OpenAsync();

// Apply migrations first
var runner = new MigrationRunner(connection);
await runner.ApplyMigrationsAsync(migrations);

// Inspect schema if needed
var introspector = new SchemaIntrospector(connection);
var tables = await introspector.GetTablesAsync();
Console.WriteLine($"Database has {tables.Count()} tables");

// Then use the document store
var store = new DocumentStore(connection, ownsConnection: false);

// Tables created by migrations can be used as document tables
await store.UpsertAsync("customer-1", new Customer { 
    Name = "John Doe", 
    Email = "john@example.com" 
});

// Or access the connection for hybrid queries
var customers = await store.Connection.QueryAsync<CustomerDto>(@"
    SELECT 
        id,
        json_extract(data, '$.name') as Name,
        json_extract(data, '$.email') as Email
    FROM Customer
    WHERE email IS NOT NULL
");
```

Each component has a single responsibility:
- **DocumentStore**: Document CRUD operations, index management
- **MigrationRunner**: Schema migrations with history tracking
- **SchemaIntrospector**: Read-only schema queries
- All share the same `SqliteConnection` for consistency
