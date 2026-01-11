# LiteDocumentStore Examples

This folder contains executable C# examples demonstrating various features of LiteDocumentStore. All examples use .NET 10's single-file execution capability and can be run directly.

## Running Examples

Each example is a standalone `.cs` file that can be executed with:

```bash
dotnet run <example-name>.cs
```

Or on Unix-like systems (after setting executable permission):
```bash
chmod +x <example-name>.cs
./<example-name>.cs
```

On Windows PowerShell:
```powershell
./<example-name>.cs
```

## Available Examples

### 1. [QuickStart.cs](QuickStart.cs)
**Basic CRUD operations** - Learn the fundamentals:
- Creating a document store with in-memory database
- Creating tables for document types
- Inserting/updating documents (Upsert)
- Retrieving documents by ID (Get)
- Getting all documents (GetAll)
- Checking existence and counting documents
- Deleting documents
- Bulk operations with transactions

**Perfect for**: First-time users, understanding the basics

---

### 2. [VirtualColumn.cs](VirtualColumn.cs)
**Dramatic query performance improvements** - 100x-1000x speedup:
- What virtual columns are and why they matter
- Creating virtual columns with indexes
- Benchmarking queries with and without virtual columns
- Point queries (exact match) optimization
- Range queries on indexed columns
- Using raw SQL with virtual columns

**Perfect for**: Performance-critical applications, large datasets

**Key concept**: Virtual columns extract JSON fields into indexed SQLite columns, enabling index seeks instead of full table scans.

---

### 3. [HybridUsage.cs](HybridUsage.cs)
**Mix document storage with traditional SQL** - Best of both worlds:
- Creating document tables alongside relational tables
- Joining documents with JSON extraction
- Aggregate queries (SUM, COUNT, GROUP BY)
- Creating views over JSON data
- Full-text search with FTS5
- Complex multi-table queries

**Perfect for**: Real-world applications needing both flexible schemas and relational power

**Key concept**: LiteDocumentStore never prevents you from using raw SQL. The `Connection` property gives full access to Dapper and SQLite.

---

### 4. [ProjectionQuery.cs](ProjectionQuery.cs)
**Select only needed fields** - Reduce memory and improve performance:
- Using `SelectAsync` for field projection
- Nested property access (e.g., `c.Address.City`)
- Filtered projections with predicates
- Comparing full deserialization vs projection performance
- Complex aggregations with raw SQL

**Perfect for**: List views, reports, APIs that don't need full documents

**Key concept**: Projection queries extract specific JSON fields instead of deserializing entire documents.

---

### 5. [IndexManagement.cs](IndexManagement.cs)
**Optimize query performance with JSON indexes** - Avoid full table scans:
- Creating indexes on JSON properties
- Nested property indexing (e.g., `Address.City`)
- Composite indexes for multi-column queries
- Index naming conventions
- Benchmarking query performance with/without indexes
- Schema introspection with `SchemaIntrospector`

**Perfect for**: Improving query performance on large datasets

**Key concept**: Indexes on JSON properties use `json_extract()` to enable SQLite's B-tree index seeks.

---

### 6. [Migration.cs](Migration.cs)
**Schema versioning and evolution** - Track and apply schema changes:
- Defining migrations with version numbers
- Up/down migration support
- Applying pending migrations with `MigrationRunner`
- Rolling back to previous versions
- Migration history tracking in `__store_migrations`
- Schema introspection (tables, columns, indexes)
- Database statistics

**Perfect for**: Production applications needing schema versioning

**Key concept**: Each migration runs in a transaction and is recorded only if successful. Timestamp-based versioning ensures ordered execution.

---

### 7. [TransactionBatching.cs](TransactionBatching.cs) *(Coming Soon)*
**Batch operations with transactions** - Atomic multi-step operations:
- Using `ExecuteInTransactionAsync` for batch operations
- Automatic commit/rollback
- Combining multiple operations atomically
- Performance benefits of batching

**Perfect for**: Bulk inserts, complex multi-step operations

---

### 8. [MultiDatabase.cs](MultiDatabase.cs) *(Coming Soon)*
**Multiple database instances** - Multi-tenant or read replicas:
- Registering multiple document stores with DI
- Using keyed services (.NET 8+)
- Managing separate databases (e.g., read replicas, multi-tenant)
- Cross-database operations

**Perfect for**: Multi-tenant SaaS applications, read/write separation

---

## Example Structure

Each example follows this pattern:

1. **Shebang line** - `#!/usr/bin/env dotnet run` for Unix execution
2. **Description** - What the example demonstrates
3. **Using statements** - Required namespaces
4. **Model definitions** - Sample data structures
5. **Setup** - Creating store, tables, seeding data
6. **Feature demonstration** - The actual example code
7. **Summary** - Key takeaways

## Prerequisites

- .NET 10 SDK or later
- SQLite 3.45+ (bundled with modern .NET)
- LiteDocumentStore library (reference the local project or NuGet package)

## Learn **IndexManagement.cs** for query optimization
6. Study **Migration.cs** for production schema management
7. Tips for Learning

1. Start with **QuickStart.cs** to understand the basics
2. Run **VirtualColumn.cs** to see the performance magic
3. Explore **HybridUsage.cs** to understand the hybrid philosophy
4. Try **ProjectionQuery.cs** for efficient data access patterns
5. Modify the examples to experiment with your own data models

## Feedback

These examples are part of the LiteDocumentStore documentation. If you find issues or have suggestions for new examples, please open an issue or submit a PR!

---

**Happy coding!** 🚀
