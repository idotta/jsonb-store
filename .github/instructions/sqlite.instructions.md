# SQLite-Specific Instructions

## Required SQLite Version

**SQLite 3.45+** is required for JSONB support. The library should validate this at startup.

```csharp
var version = await connection.QueryFirstAsync<string>("SELECT sqlite_version()");
var parts = version.Split('.').Select(int.Parse).ToArray();
if (parts[0] < 3 || (parts[0] == 3 && parts[1] < 45))
{
    throw new NotSupportedException($"SQLite 3.45+ required for JSONB. Found: {version}");
}
```

## JSONB Functions

### Writing Data (JSON → JSONB)

```sql
-- Convert JSON text to JSONB binary format
INSERT INTO Customer (id, data) VALUES (@Id, jsonb(@JsonText))

-- The jsonb() function creates compact binary representation
UPDATE Customer SET data = jsonb(@JsonText) WHERE id = @Id
```

### Reading Data (JSONB → JSON)

```sql
-- Convert JSONB back to JSON text for deserialization
SELECT json(data) as data FROM Customer WHERE id = @Id

-- json() is required because JSONB is binary and not human-readable
```

### Querying JSON Fields

```sql
-- Extract a value from JSON/JSONB
SELECT json_extract(data, '$.email') FROM Customer

-- Shorthand syntax (SQLite 3.38+)
SELECT data->>'$.email' FROM Customer

-- Nested paths
SELECT json_extract(data, '$.address.city') FROM Customer

-- Array access
SELECT json_extract(data, '$.tags[0]') FROM Customer
```

### JSON Modification

```sql
-- Set a single field
UPDATE Customer 
SET data = json_set(data, '$.email', @NewEmail)
WHERE id = @Id

-- Remove a field
UPDATE Customer 
SET data = json_remove(data, '$.temporaryField')
WHERE id = @Id

-- Patch multiple fields
UPDATE Customer 
SET data = json_patch(data, @PatchJson)
WHERE id = @Id
```

## Performance Configuration

### WAL Mode (Write-Ahead Logging)

```sql
-- Enable WAL for better concurrency (default in LiteDocumentStore)
PRAGMA journal_mode = WAL;

-- Benefits:
-- - Readers don't block writers
-- - Writers don't block readers
-- - Better performance for most workloads
```

### Synchronous Mode

```sql
-- NORMAL provides good balance of safety and speed (default in LiteDocumentStore)
PRAGMA synchronous = NORMAL;

-- Options:
-- OFF    - Fastest, but risk of corruption on power loss
-- NORMAL - Safe with WAL, good performance
-- FULL   - Safest, slower
```

### Other Performance PRAGMAs

```sql
-- Increase cache size (default is often too small)
PRAGMA cache_size = -64000;  -- 64MB (negative = KB)

-- Memory-mapped I/O for large databases
PRAGMA mmap_size = 268435456;  -- 256MB

-- Temp store in memory
PRAGMA temp_store = MEMORY;

-- Page size (must be set before creating tables)
PRAGMA page_size = 4096;  -- Match filesystem block size
```

## Indexing Strategies

### Index on JSON Field

```sql
-- Create index on extracted JSON field
CREATE INDEX idx_customer_email 
ON Customer(json_extract(data, '$.email'));

-- Query will use index
SELECT * FROM Customer 
WHERE json_extract(data, '$.email') = 'john@example.com';
```

### Generated Columns (Preferred for Frequent Queries)

```sql
-- Add virtual column that extracts JSON field
ALTER TABLE Customer 
ADD COLUMN email TEXT 
GENERATED ALWAYS AS (json_extract(data, '$.email')) VIRTUAL;

-- Create standard index on virtual column
CREATE INDEX idx_customer_email ON Customer(email);

-- Query uses simple column syntax
SELECT * FROM Customer WHERE email = 'john@example.com';
```

### Partial Indexes

```sql
-- Index only active customers
CREATE INDEX idx_active_customers 
ON Customer(json_extract(data, '$.email'))
WHERE json_extract(data, '$.active') = 1;
```

## Transaction Handling

### Basic Transaction

```sql
BEGIN TRANSACTION;
-- operations
COMMIT;
-- or ROLLBACK on error
```

### Savepoints (Nested Transactions)

```sql
BEGIN TRANSACTION;
SAVEPOINT sp1;
-- operations
RELEASE sp1;  -- or ROLLBACK TO sp1
COMMIT;
```

### Deferred vs Immediate

```sql
-- Deferred: Lock acquired on first write (default)
BEGIN DEFERRED TRANSACTION;

-- Immediate: Write lock acquired immediately
BEGIN IMMEDIATE TRANSACTION;

-- Exclusive: Full exclusive lock
BEGIN EXCLUSIVE TRANSACTION;
```

## Common SQLite Error Codes

| Code | Name | Cause | Solution |
|------|------|-------|----------|
| 5 | SQLITE_BUSY | Database locked by another process | Retry with backoff |
| 6 | SQLITE_LOCKED | Table locked within same connection | Check transaction logic |
| 11 | SQLITE_CORRUPT | Database corruption | Restore from backup |
| 13 | SQLITE_FULL | Disk full | Free disk space |
| 19 | SQLITE_CONSTRAINT | Constraint violation | Check unique/FK constraints |

## Connection String Options

```csharp
// Basic file database
"Data Source=app.db"

// In-memory (for testing)
"Data Source=:memory:"

// Shared in-memory (multiple connections)
"Data Source=file::memory:?cache=shared"

// Read-only
"Data Source=app.db;Mode=ReadOnly"

// Create if not exists (default behavior)
"Data Source=app.db;Mode=ReadWriteCreate"

// With password (requires SQLCipher)
"Data Source=app.db;Password=secret"
```

## Best Practices

1. **Always use parameterized queries** - Never concatenate user input
2. **Use transactions for batches** - Single transaction for multiple writes
3. **Close connections properly** - Use `using` statements
4. **Checkpoint WAL periodically** - `PRAGMA wal_checkpoint(TRUNCATE)`
5. **Vacuum occasionally** - `VACUUM` to reclaim space after many deletes
6. **Backup regularly** - `VACUUM INTO 'backup.db'` for online backup
