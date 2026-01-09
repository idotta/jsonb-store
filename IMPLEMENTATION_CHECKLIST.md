# LiteDocumentStore Implementation Checklist

A comprehensive checklist for building a production-ready hybrid SQLite library that provides convenient JSON document storage while preserving full relational database capabilities.

---

## ⚠️ Recent Unplanned Changes (Phase 1 Extensions)

The implementation has progressed beyond the original Phase 1 scope with several architectural improvements:

### Core Architecture Enhancements
- ✅ **Connection Ownership Pattern**: DocumentStore now supports `ownsConnection` parameter for flexible lifecycle management
- ✅ **Factory Pattern Implementation**: Added `IDocumentStoreFactory` and `DocumentStoreFactory` for proper composition
- ✅ **SqlGenerator Extraction**: SQL generation moved to separate `SqlGenerator` class for testability
- ✅ **Transaction Support**: Implemented `ExecuteInTransactionAsync` with multiple overloads

### Configuration & Builder Pattern
- ✅ **Fluent Builder API**: Complete `DocumentStoreOptionsBuilder` with method chaining
- ✅ **Advanced Connection Options**: Added pooling config, foreign keys, busy timeout, and additional pragmas
- ✅ **Per-Store Customization**: Options can override serializer and naming conventions per instance

### DI & Multi-Database Support
- ✅ **Keyed Services**: Full support for multiple database instances via `AddKeyedLiteDocumentStore()`
- ✅ **Factory Registration**: Automatic registration of `IDocumentStoreFactory` in DI container
- ✅ **Configurable Lifetimes**: Support for Singleton, Scoped, and Transient registrations

### Logging & Observability
- ✅ **Comprehensive Logging**: Debug and Information level logging throughout all operations
- ✅ **NullLogger Support**: Graceful fallback when no logger is configured

### Testing
- ✅ **Expanded Test Coverage**: Unit tests for ownership patterns, disposal, and table naming
- ✅ **Integration Tests**: Full CRUD operations, transactions with commit/rollback verification

---

## 1. Core Architecture & Abstractions

- [x] **Define interfaces for extensibility**
  - [x] `IDocumentStore` - Generic document store contract (formerly `IRepository`)
  - [x] `IJsonSerializer` - Pluggable JSON serialization (System.Text.Json, Newtonsoft, etc.)
  - [x] `IConnectionFactory` - Connection lifecycle management
  - [x] `ITableNamingConvention` - Customizable table naming (pluralization, snake_case, etc.)
  - [x] **NEW**: `IDocumentStoreFactory` - Factory for creating configured document stores

- [x] **Configuration system**
  - [x] `DocumentStoreOptions` class with builder pattern
  - [x] WAL mode toggle, synchronous level, page size, cache size
  - [x] Connection string builder for common scenarios
  - [x] Support for in-memory databases (`:memory:`, shared cache)
  - [x] **NEW**: `DocumentStoreOptionsBuilder` with fluent API
  - [x] **NEW**: Factory methods for in-memory, shared in-memory, and file-based databases
  - [x] **NEW**: Support for additional pragmas and custom serializer/naming convention per-store
  - [x] **NEW**: Foreign keys and busy timeout configuration

- [x] **Dependency Injection support**
  - [x] `IServiceCollection.AddLiteDocumentStore()` extension method
  - [x] Registers `SqliteConnection` for hybrid usage
  - [x] Scoped vs Singleton connection strategies
  - [x] Named/keyed store registration for multiple databases (keyed services for .NET 8+)
  - [x] **NEW**: `AddKeyedLiteDocumentStore()` for managing multiple database instances
  - [x] **NEW**: Automatic factory registration (`IDocumentStoreFactory`)
  - [x] **NEW**: Configurable service lifetime (Singleton, Scoped, Transient)
  - [x] **NEW**: Core dependencies registered as singletons (stateless services)

- [x] **Refactor existing `Repository.cs`**
  - [x] Rename to `DocumentStore` supporting `IDocumentStore`
  - [x] Decouple connection lifecycle (doesn't own connection)
  - [x] Replace direct `System.Text.Json` calls with `IJsonSerializer`
  - [x] Replace hardcoded `GetTableName<T>()` with `ITableNamingConvention`
  - [x] Accept `SqliteConnection` in constructor
  - [x] Use `IConnectionFactory` for creation (via DI)
  - [x] Add input validation (null/empty ID checks, etc.)
  - [x] Add `ILogger` integration for diagnostics
  - [x] Extract SQL generation to internal helper (for testability)
  - [x] **NEW**: Add configurable connection ownership (`ownsConnection` parameter)
  - [x] **NEW**: Implement `ExecuteInTransactionAsync` overloads for batch operations
  - [x] **NEW**: Internal implementation with proper disposal pattern

---

## 2. Connection & Lifecycle Management

- [x] **Connection pooling strategy**
  - [x] Single long-lived connection mode (current)

- [x] **Proper resource management**
  - [x] `IAsyncDisposable` pattern (exists, verified correct)
  - [x] `IDisposable` pattern for synchronous disposal
  - [x] **NEW**: Conditional connection disposal based on ownership
  - [x] **NEW**: Connection state validation in property accessor
  - [x] Connection state validation before operations
  - [x] Graceful shutdown with WAL checkpoint

- [x] **Factory pattern implementation**
  - [x] **NEW**: `IDocumentStoreFactory` interface
  - [x] **NEW**: `DefaultConnectionFactory` implementation
  - [x] **NEW**: `DocumentStoreFactory` for creating configured stores
  - [x] **NEW**: Async factory methods (`CreateAsync`, `CreateConnectionAsync`)
  - [x] **NEW**: Connection configuration via `ConfigureConnection` and `ConfigureConnectionAsync`

- [x] **Health checks**
  - [x] `IsHealthyAsync()` method for liveness probes
  - [x] SQLite version validation (require 3.45+ for JSONB)

---

## 3. Schema & Migration Management

- [x] **Index management**
  - [x] `CreateIndexAsync<T>(Expression<Func<T, object>> jsonPath)` for JSON path indexes
  - [x] Automatic index on `id` (already exists via PRIMARY KEY)
  - [x] Composite index support via `CreateCompositeIndexAsync<T>`
  - [x] Index existence checking before creation

- [x] **Schema versioning**
  - [x] Simple migration table (`__store_migrations`)
  - [x] Up/down migration support
  - [x] Schema introspection helpers
  - [x] `MigrationRunner` class for managing migrations independently from DocumentStore
  - [x] `IMigration` interface and `Migration` base class
  - [x] `SchemaIntrospector` public class for querying database schema independently
  - [x] Table, column, and index introspection methods
  - [x] Database statistics retrieval
  - [x] Clean separation: users pass connection to MigrationRunner and SchemaIntrospector directly

---

## 4. CRUD Operations Enhancement

- [x] **Transaction Support**
  - [x] **NEW**: `ExecuteInTransactionAsync` with two overloads (with/without IDbTransaction)
  - [x] **NEW**: Automatic commit on success, rollback on exception
  - [ ] Return affected rows count or the entity itself
  - [ ] Support partial updates (PATCH semantics with `json_patch()`)

- [ ] **Improved Upsert**
  - [ ] Return affected rows count or the entity itself
  - [ ] Support partial updates (PATCH semantics with `json_patch()`)
  - [ ] Bulk upsert with single statement (`INSERT ... VALUES (...), (...), ...`)

- [ ] **Batch operations**
  - [ ] `UpsertManyAsync<T>(IEnumerable<(string id, T data)> items)`
  - [ ] `DeleteManyAsync<T>(IEnumerable<string> ids)`
  - [ ] Configurable batch size for large datasets

- [ ] **Existence checks**
  - [ ] `ExistsAsync<T>(string id)` without deserializing
  - [ ] `CountAsync<T>()` for table row count

- [ ] **Pagination**
  - [ ] `GetPagedAsync<T>(int offset, int limit)`
  - [ ] Cursor-based pagination for large datasets

---

## 5. Querying Capabilities (The Hybrid Experience)

- [ ] **JSON path querying**
  - [ ] `QueryAsync<T>(string jsonPath, object value)` using `json_extract()`
  - [ ] `QueryAsync<T>(Expression<Func<T, bool>> predicate)` with expression tree translation
  - [ ] Support for `$.property`, `$.nested.property`, `$.array[0]`

- [ ] **Fluent query builder**
  ```csharp
  repo.Query<Customer>()
      .Where(c => c.Email, "Contains", "@company.com")
      .OrderBy("$.lastName")
      .Take(10)
      .ToListAsync();
  ```

- [ ] **Raw SQL escape hatch** (critical for hybrid use)
  - [x] **IMPLEMENTED**: Direct access to Dapper's full API via `Connection` property (exists)
  - [ ] `ExecuteAsync(string sql, object? param = null)`
  - [ ] `QueryAsync<TResult>(string sql, object? param = null)`
  - [ ] `QueryFirstOrDefaultAsync<TResult>(string sql, object? param = null)`

- [ ] **Projection queries**
  - [ ] Select specific JSON fields only
  - [ ] `SELECT json_extract(data, '$.name') as Name FROM Customer`

---

## 6. Relational Features (Fluid Experience)

- [ ] **Relationship support**
  - [ ] Foreign key helpers between document tables
  - [ ] `JoinAsync<T1, T2>()` for cross-table queries
  - [ ] Document references by ID with lazy loading option

- [ ] **Mixed schema support**
  - [ ] Allow creating traditional relational tables alongside document tables
  - [ ] `ExecuteSchemaAsync(string ddl)` for custom table creation
  - [ ] Views over JSON data with `json_extract()`

- [ ] **Virtual columns** (SQLite generated columns)
  - [ ] Extract frequently queried JSON fields as indexed virtual columns
  - [ ] `ALTER TABLE ADD COLUMN email TEXT GENERATED ALWAYS AS (json_extract(data, '$.email'))`

---

## 7. Performance Optimizations

- [ ] **Prepared statement caching**
  - [ ] Cache parameterized SQL for repeated operations
  - [ ] Dapper handles this partially, verify optimization

- [ ] **JSONB verification**
  - [ ] Ensure `jsonb()` function is used on write (exists)
  - [ ] Ensure `json()` is used on read for deserialization (exists)
  - [ ] Consider direct BLOB read for internal JSONB processing

- [ ] **Transaction improvements**
  - [ ] `ITransactionScope` abstraction
  - [ ] Nested transaction support (savepoints)
  - [ ] Explicit read-only transaction mode (`BEGIN DEFERRED`)

- [ ] **Benchmarking suite**
  - [ ] BenchmarkDotNet project
  - [ ] Compare vs raw Dapper, EF Core, LiteDB
  - [ ] Measure: single insert, bulk insert, query, full-table scan

---

## 8. Error Handling & Resilience

- [x] **Custom exceptions** (basic implementation)
  - [x] **NEW**: Built-in .NET exceptions used appropriately (ArgumentNullException, ObjectDisposedException, etc.)
  - [ ] `LiteDocumentStoreException` base class
  - [ ] `TableNotFoundException`, `SerializationException`, `ConcurrencyException`
  - [ ] Preserve inner SQLite exceptions

- [x] **Validation**
  - [x] Validate ID is not null/empty before operations
  - [x] Validate data is not null before upsert
  - [x] **NEW**: Null checks with ArgumentNullException.ThrowIfNull()
  - [ ] Optional data validation via `IValidatableObject` or custom validator

- [ ] **Retry policies**
  - [ ] Configurable retry on `SQLITE_BUSY` and `SQLITE_LOCKED`
  - [ ] Exponential backoff with jitter
  - [ ] Optional Polly integration

---

## 9. Observability

- [x] **Logging**
  - [x] `ILogger<DocumentStore>` integration
  - [x] **NEW**: Debug-level logging for all operations (CreateTable, Upsert, Get, Delete, etc.)
  - [x] **NEW**: Information-level logging for significant events
  - [x] **NEW**: NullLogger support when no logger is provided
  - [ ] Log slow queries (configurable threshold)

- [ ] **Metrics**
  - [ ] Operation counters (inserts, updates, deletes, queries)
  - [ ] Latency histograms
  - [ ] Optional OpenTelemetry integration

- [ ] **Diagnostics**
  - [ ] `DiagnosticSource` events for APM tools
  - [ ] Activity tracing for distributed systems

---

## 10. Type Handler System

- [ ] **Improve `SqliteJsonbTypeHandler<T>`**
  - [ ] Make JSON serializer injectable
  - [ ] Support custom `JsonSerializerOptions`
  - [ ] Handle `null` values explicitly

- [ ] **Auto-registration**
  - [ ] Automatic type handler registration on first use
  - [ ] Type handler cache to avoid re-registration

- [ ] **Complex type support**
  - [ ] Collections, dictionaries, nested objects
  - [ ] Polymorphic serialization with type discriminator

---

## 11. Testing Infrastructure

- [x] **Unit tests** (mock connection)
  - [x] Test all public API methods
  - [x] **NEW**: Connection ownership tests (ownsConnection parameter)
  - [x] **NEW**: Disposal pattern verification
  - [x] Edge cases: null, empty, special characters in ID
  - [ ] Concurrency scenarios

- [x] **Integration tests** (real SQLite)
  - [x] In-memory database for speed
  - [x] **NEW**: Full CRUD operation testing
  - [x] **NEW**: Transaction commit and rollback tests
  - [x] **NEW**: GetAllAsync, DeleteAsync verification
  - [ ] File-based database for WAL testing
  - [ ] Multi-connection concurrency tests

- [ ] **Test helpers**
  - [ ] `LiteDocumentStoreTestFixture` for easy test setup
  - [ ] Database seeding utilities

---

## 12. Documentation & Developer Experience

- [x] **XML documentation**
  - [x] All public APIs documented
  - [x] **NEW**: Comprehensive XML docs for all interfaces and public classes
  - [x] **NEW**: Parameter descriptions and return value documentation
  - [ ] Include examples in `<example>` tags
  - [ ] Generate API reference site

- [x] **README improvements**
  - [x] Quick start guide with code examples
  - [x] **NEW**: Features list with checkmarks
  - [x] **NEW**: JSONB benefits explained
  - [x] **NEW**: CI/CD badges
  - [ ] Common patterns and recipes
  - [ ] FAQ section
  - [ ] Performance tuning guide

- [ ] **Sample projects**
  - [ ] Console app example
  - [ ] ASP.NET Core integration example
  - [ ] Multi-table relational + document hybrid example

---

## 13. Packaging & Distribution

- [ ] **NuGet package**
  - [ ] Proper `.nuspec` or SDK-style properties
  - [ ] Source Link for debugging .snupkg
  - [ ] README in package
  - [ ] Icon and license metadata

- [ ] **Versioning**
  - [ ] Semantic versioning
  - [ ] Changelog maintenance
  - [ ] GitHub releases automation (exists in CI)

---

## 14. Security Considerations

- [ ] **SQL injection prevention**
  - [ ] All user-facing table names validated/sanitized
  - [ ] Parameterized queries only (exists via Dapper)
  - [ ] Consider allowlist for table name characters

- [ ] **Encryption at rest** (optional)
  - [ ] Document SQLCipher integration path
  - [ ] Or recommend file-system encryption

---

## Implementation Phases

| Phase | Focus | Priority |
|-------|-------|----------|
| **Phase 1** | Core Stability | Interfaces, Configuration, Error handling, Logging |
| **Phase 2** | Query Power | JSON path queries, Fluent builder, Raw SQL access |
| **Phase 3** | Hybrid Experience | Index management, Virtual columns, Relationship helpers |
| **Phase 4** | Production Ready | DI integration, Health checks, Retry policies, Metrics |
| **Phase 5** | Polish | Benchmarks, Docs, Samples, NuGet packaging |

---

## Notes

- **Hybrid Philosophy**: The library should never prevent users from using raw SQL or traditional relational patterns. The document store features are conveniences, not constraints.
- **Performance First**: Every feature should be evaluated against the performance requirements in the README (35% faster than raw file I/O, transaction batching, async operations).
- **SQLite 3.45+ Requirement**: JSONB support is mandatory; fail fast with a clear error message on older versions.
