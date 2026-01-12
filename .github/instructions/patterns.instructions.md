# Common Patterns Instructions

## Repository Pattern

The core `Repository` class provides document-style operations while exposing the connection for relational queries.

### Basic CRUD

```csharp
using var repo = new Repository("app.db");

// Create table (once per type)
await repo.CreateTableAsync<Customer>();

// Create/Update
await repo.UpsertAsync("cust-123", new Customer { Name = "John" });

// Read
var customer = await repo.GetAsync<Customer>("cust-123");

// Read all
var allCustomers = await repo.GetAllAsync<Customer>();

// Delete
var deleted = await repo.DeleteAsync<Customer>("cust-123");
```

### Hybrid Relational Access

```csharp
// Direct SQL via Dapper (always available)
var results = await repo.Connection.QueryAsync<CustomerDto>(
    @"SELECT json_extract(data, '$.name') as Name,
             json_extract(data, '$.email') as Email
      FROM Customer 
      WHERE json_extract(data, '$.active') = 1");

// Join document tables
var orderDetails = await repo.Connection.QueryAsync<OrderDetail>(
    @"SELECT o.id, json_extract(o.data, '$.total') as Total,
             json_extract(c.data, '$.name') as CustomerName
      FROM [Order] o
      JOIN Customer c ON json_extract(o.data, '$.customerId') = c.id");
```

## Transaction Pattern

Batch operations for performance:

```csharp
await repo.ExecuteInTransactionAsync(async () =>
{
    foreach (var customer in customers)
    {
        await repo.UpsertAsync(customer.Id, customer);
    }
});
```

With transaction access:

```csharp
await repo.ExecuteInTransactionAsync(async (transaction) =>
{
    // Multiple operations in same transaction
    await repo.UpsertAsync("cust-1", customer1);
    await repo.UpsertAsync("cust-2", customer2);
    
    // Can also use Dapper directly with transaction
    await repo.Connection.ExecuteAsync(
        "UPDATE Customer SET data = jsonb(@Data) WHERE id = @Id",
        new { Id = "cust-1", Data = json },
        transaction);
});
```

## JSON Path Indexing Pattern

Create indexes on frequently queried JSON fields:

```csharp
// Create an index on the email field
await repo.Connection.ExecuteAsync(
    @"CREATE INDEX IF NOT EXISTS idx_customer_email 
      ON Customer(json_extract(data, '$.email'))");

// Now this query uses the index
var customer = await repo.Connection.QueryFirstOrDefaultAsync<Customer>(
    @"SELECT json(data) as data FROM Customer 
      WHERE json_extract(data, '$.email') = @Email",
    new { Email = "john@example.com" });
```

## Virtual Column Pattern

For frequently queried fields, use generated columns:

```csharp
// Add a virtual column (one-time migration)
await repo.Connection.ExecuteAsync(
    @"ALTER TABLE Customer 
      ADD COLUMN email TEXT 
      GENERATED ALWAYS AS (json_extract(data, '$.email')) VIRTUAL");

// Create index on virtual column
await repo.Connection.ExecuteAsync(
    "CREATE INDEX IF NOT EXISTS idx_customer_email ON Customer(email)");

// Query uses standard column syntax
var customer = await repo.Connection.QueryFirstOrDefaultAsync<string>(
    "SELECT json(data) FROM Customer WHERE email = @Email",
    new { Email = "john@example.com" });
```

## Bulk Insert Pattern

For large datasets, use transactions with batching:

```csharp
public async Task BulkUpsertAsync<T>(IEnumerable<(string Id, T Data)> items, int batchSize = 1000)
{
    var batches = items.Chunk(batchSize);
    
    foreach (var batch in batches)
    {
        await repo.ExecuteInTransactionAsync(async () =>
        {
            foreach (var (id, data) in batch)
            {
                await repo.UpsertAsync(id, data);
            }
        });
    }
}
```

## Connection Reuse Pattern

For high-throughput scenarios, reuse the repository:

```csharp
// Singleton or scoped lifetime in DI
public class CustomerService
{
    private readonly Repository _repository;
    
    public CustomerService(Repository repository)
    {
        _repository = repository;
    }
    
    public async Task<Customer?> GetCustomerAsync(string id)
    {
        return await _repository.GetAsync<Customer>(id);
    }
}
```

## Mixed Schema Pattern

Combine document tables with traditional relational tables:

```csharp
// Document table for flexible data
await repo.CreateTableAsync<Customer>();

// Traditional relational table for structured data
await repo.Connection.ExecuteAsync(@"
    CREATE TABLE IF NOT EXISTS audit_log (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        entity_type TEXT NOT NULL,
        entity_id TEXT NOT NULL,
        action TEXT NOT NULL,
        timestamp INTEGER NOT NULL DEFAULT (strftime('%s', 'now')),
        user_id TEXT
    )");

// Query across both
var auditWithCustomer = await repo.Connection.QueryAsync(@"
    SELECT a.*, json_extract(c.data, '$.name') as CustomerName
    FROM audit_log a
    LEFT JOIN Customer c ON a.entity_id = c.id
    WHERE a.entity_type = 'Customer'");
```

## Error Handling Pattern

```csharp
try
{
    await repo.UpsertAsync(id, data);
}
catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // CONSTRAINT
{
    // Handle constraint violation
    throw new DuplicateKeyException($"Record with ID {id} already exists", ex);
}
catch (SqliteException ex) when (ex.SqliteErrorCode == 11) // CORRUPT
{
    // Handle database corruption
    _logger.LogCritical(ex, "Database corruption detected");
    throw;
}
```

## Optimistic Concurrency Pattern

```csharp
// Table with version column
await repo.Connection.ExecuteAsync(@"
    CREATE TABLE IF NOT EXISTS [Customer] (
        id TEXT PRIMARY KEY,
        data BLOB NOT NULL,
        version INTEGER NOT NULL DEFAULT 1
    )");

// Update with version check
var affected = await repo.Connection.ExecuteAsync(@"
    UPDATE [Customer] 
    SET data = jsonb(@Data), 
        version = version + 1
    WHERE id = @Id AND version = @ExpectedVersion",
    new { Id = id, Data = json, ExpectedVersion = version });

if (affected == 0)
    throw new ConcurrencyException("Record was modified by another process");
```
