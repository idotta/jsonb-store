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
  - [x] ~~`IJsonSerializer`~~ - **REMOVED**: Replaced with fixed, optimized `JsonHelper` for performance
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
  - [x] ~~Replace direct `System.Text.Json` calls with `IJsonSerializer`~~ - **CHANGED**: Use fixed `JsonHelper` with optimized options
  - [x] Replace hardcoded `GetTableName<T>()` with `ITableNamingConvention`
  - [x] Accept `SqliteConnection` in constructor
  - [x] Use `IConnectionFactory` for creation (via DI)
  - [x] Add input validation (null/empty ID checks, etc.)
  - [x] Add `ILogger` integration for diagnostics
  - [x] Extract SQL generation to internal helper (for testability)
  - [x] **NEW**: Add configurable connection ownership (`ownsConnection` parameter)
  - [x] **NEW**: Implement `ExecuteInTransactionAsync` overloads for batch operations
  - [x] **NEW**: Internal implementation with proper disposal pattern
  - [x] **NEW**: Optimized serialization using `SerializeToUtf8Bytes` for byte[] instead of string

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
  - [x] Return affected rows count from `UpsertAsync`

- [x] **Improved Upsert**
  - [x] Return affected rows count from `UpsertAsync`
  - [x] Bulk upsert with single statement (`INSERT ... VALUES (...), (...), ...`)

- [x] **Batch operations**
  - [x] `UpsertManyAsync<T>(IEnumerable<(string id, T data)> items)`
  - [x] `DeleteManyAsync<T>(IEnumerable<string> ids)`

- [x] **Existence checks**
  - [x] `ExistsAsync<T>(string id)` without deserializing
  - [x] `CountAsync<T>()` for table row count

---

## 5. Querying Capabilities (The Hybrid Experience)

- [x] **JSON path querying**
  - [x] `QueryAsync<T>(string jsonPath, object value)` using `json_extract()`
  - [x] `QueryAsync<T>(Expression<Func<T, bool>> predicate)` with expression tree translation
  - [x] Support for `$.property`, `$.nested.property`, `$.array[0]`

- [x] **Raw SQL escape hatch** (critical for hybrid use)
  - [x] **IMPLEMENTED**: Direct access to Dapper's full API via `Connection` property (exists)

- [x] **Projection queries**
  - [x] Select specific JSON fields only
  - [x] `SELECT json_extract(data, '$.name') as Name FROM Customer`
  - [x] `SelectAsync<TSource, TResult>()` with expression-based field selection
  - [x] Supports anonymous types and custom projection types
  - [x] Can combine with predicates for filtered projections

---

## 6. SQLite Optimizations

- [x] **Mixed schema support** *(already supported)*
  - [x] Traditional relational tables work alongside document tables via `Connection` property
  - [x] Users can execute any DDL/DML using `Connection.ExecuteAsync()`
  - [x] Views over JSON data can be created using raw SQL

- [x] **Virtual columns** (SQLite generated columns)
  - [x] `AddVirtualColumnAsync<T>(Expression, columnName, createIndex)` helper
  - [x] Generates `ALTER TABLE ADD COLUMN ... GENERATED ALWAYS AS (json_extract(data, '$.path'))`
  - [x] Optional automatic index creation on the virtual column
  - [x] Column existence check before creation

---

## 7. Performance Optimizations

- [x] **Prepared statement caching**
  - [x] **INVESTIGATED & REJECTED** (January 2026)
  - [x] Implemented `PreparedStatementCache` with ConcurrentDictionary for thread-safe SQL caching
  - [x] Comprehensive benchmarks showed **3-5% SLOWER performance** despite 10-19% memory reduction
  - [x] **Root cause**: ConcurrentDictionary lookup + cache key construction costs more than generating simple SQL strings
  - [x] **Dapper already caches** at ADO.NET level (command plans, parameter mapping)
  - [x] Modern C# string interpolation is highly optimized by JIT compiler
  - [x] **Conclusion**: For a "Performance First" library, trading speed for ~200-400 KB per 1000 operations is the wrong tradeoff
  - [x] SQL generation is fast enough; focus optimization efforts elsewhere
  - [x] Full benchmark results preserved in `LiteDocumentStore.Benchmarks.PreparedStatementCacheBenchmark-report-github.md`

- [x] **JSONB verification**
  - [x] Ensure `jsonb()` function is used on write (verified in `SqlGenerator.GenerateUpsertSql` and `GenerateBulkUpsertSql`)
  - [x] Ensure `json()` is used on read for deserialization (verified in all SELECT operations)
  - [x] **INVESTIGATED & CONCLUDED**: Direct BLOB reading would require custom JSONB parser. Current approach using SQLite's `json()` function is optimal as System.Text.Json cannot parse SQLite's proprietary JSONB binary format. The in-database conversion is highly optimized C code.

- [x] **Benchmarking suite**
  - [x] BenchmarkDotNet project
  - [x] Compare vs raw Dapper, LiteDB
  - [x] Measure: single insert, bulk insert, query, full-table scan

- [x] **Dapper type handlers**
  - [x] **INVESTIGATED & REJECTED** (January 2026)
  - [x] Created `SqliteJsonbTypeHandler<T>` and `TypeHandlerRegistry` for automatic type mapping
  - [x] **Root cause**: System.Text.Json cannot parse SQLite's proprietary JSONB binary format
  - [x] **Required pattern**: Must use SQLite's `json()` function to convert JSONB → JSON string first
  - [x] Type handlers would receive raw JSONB blobs from `SELECT data FROM table` but can't deserialize them
  - [x] **Conclusion**: Manual deserialization with `JsonHelper` after `json()` conversion is the only viable approach
  - [x] Current pattern (`QueryAsync<string>` + `JsonHelper.Deserialize`) is explicit, clear, and works correctly
  - [x] `SqliteJsonbTypeHandler<T>` kept for reference but not used in the codebase

---

## 8. Error Handling & Resilience

- [x] **Custom exceptions**
  - [x] **NEW**: Built-in .NET exceptions used appropriately (ArgumentNullException, ObjectDisposedException, etc.)
  - [x] `LiteDocumentStoreException` base class
  - [x] `TableNotFoundException`, `SerializationException`, `ConcurrencyException`
  - [x] Preserve inner SQLite exceptions

- [x] **Validation**
  - [x] Validate ID is not null/empty before operations
  - [x] Validate data is not null before upsert
  - [x] **NEW**: Null checks with ArgumentNullException.ThrowIfNull()

---

## 9. Observability

- [x] **Logging**
  - [x] `ILogger<DocumentStore>` integration
  - [x] **NEW**: Debug-level logging for all operations (CreateTable, Upsert, Get, Delete, etc.)
  - [x] **NEW**: Information-level logging for significant events
  - [x] **NEW**: NullLogger support when no logger is provided

---

## 10. Testing Infrastructure

- [x] **Unit tests** (mock connection)
  - [x] Test all public API methods
  - [x] **NEW**: Connection ownership tests (ownsConnection parameter)
  - [x] **NEW**: Disposal pattern verification
  - [x] Edge cases: null, empty, special characters in ID
  - [x] Concurrency scenarios

- [x] **Integration tests** (real SQLite)
  - [x] In-memory database for speed
  - [x] **NEW**: Full CRUD operation testing
  - [x] **NEW**: Transaction commit and rollback tests
  - [x] **NEW**: GetAllAsync, DeleteAsync verification
  - [x] File-based database for WAL testing
  - [x] Multi-connection concurrency tests

- [x] **Test helpers**
  - [x] `LiteDocumentStoreTestFixture` for easy test setup
  - [x] Database seeding utilities

---

## 11. Documentation & Developer Experience

- [x] **XML documentation**
  - [x] All public APIs documented
  - [x] **NEW**: Comprehensive XML docs for all interfaces and public classes
  - [x] **NEW**: Parameter descriptions and return value documentation

- [x] **README improvements**
  - [x] Quick start guide with code examples
  - [x] **NEW**: Features list with checkmarks
  - [x] **NEW**: JSONB benefits explained
  - [x] **NEW**: CI/CD badges

- [x] **Executable examples** (using .NET 10 single-file execution)
  - [x] QuickStart.cs - Basic CRUD operations
  - [x] VirtualColumn.cs - Performance optimization with virtual columns
  - [x] HybridUsage.cs - Mix document storage with traditional SQL
  - [x] ProjectionQuery.cs - Field projection for performance
  - [x] IndexManagement.cs - Creating and using JSON path indexes
  - [x] Migration.cs - Schema versioning and evolution
  - [x] TransactionBatching.cs - Batch operations example
  - [x] MultiDatabase.cs - Multiple database instances with IDocumentStoreFactory
  - [x] MultiDatabaseKeyed.cs - Multiple database instances with keyed services
  - [x] examples/README.md - Documentation for all examples

---

## 12. Packaging & Distribution

- [x] **NuGet package**
  - [x] Proper `.nuspec` or SDK-style properties
  - [x] Source Link for debugging .snupkg
  - [x] README in package
  - [x] Icon and license metadata

- [x] **Versioning**
  - [x] Semantic versioning (follow SemVer 2.0)
  - [x] Changelog maintenance (GitHub releases serve as changelog)
  - [x] GitHub releases automation (via publish.yml workflow)

---

## 13. Security Considerations

- [ ] **SQL injection prevention**
  - [ ] All user-facing table names validated/sanitized
  - [ ] Parameterized queries only (exists via Dapper)
  - [ ] Consider allowlist for table name characters

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
