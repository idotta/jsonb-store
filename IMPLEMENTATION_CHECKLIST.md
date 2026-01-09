# JsonbStore Implementation Checklist

A comprehensive checklist for building a production-ready hybrid SQLite library that provides convenient JSON document storage while preserving full relational database capabilities.

---

## 1. Core Architecture & Abstractions

- [ ] **Define interfaces for extensibility**
  - [ ] `IRepository<T>` - Generic repository contract
  - [ ] `IJsonSerializer` - Pluggable JSON serialization (System.Text.Json, Newtonsoft, etc.)
  - [ ] `IConnectionFactory` - Connection lifecycle management
  - [ ] `ITableNamingConvention` - Customizable table naming (pluralization, snake_case, etc.)

- [ ] **Configuration system**
  - [ ] `JsonbStoreOptions` class with builder pattern
  - [ ] WAL mode toggle, synchronous level, page size, cache size
  - [ ] Connection string builder for common scenarios
  - [ ] Support for in-memory databases (`:memory:`, shared cache)

- [ ] **Dependency Injection support**
  - [ ] `IServiceCollection.AddJsonbStore()` extension method
  - [ ] Scoped vs Singleton connection strategies
  - [ ] Named/keyed repository registration for multiple databases

---

## 2. Connection & Lifecycle Management

- [ ] **Connection pooling strategy**
  - [ ] Single long-lived connection mode (current)
  - [ ] Pool mode with `SqliteConnectionPool` wrapper
  - [ ] Configurable pool size and timeout

- [ ] **Proper resource management**
  - [ ] `IAsyncDisposable` pattern (exists, verify correctness)
  - [ ] Connection state validation before operations
  - [ ] Graceful shutdown with WAL checkpoint

- [ ] **Health checks**
  - [ ] `IsHealthyAsync()` method for liveness probes
  - [ ] SQLite version validation (require 3.45+ for JSONB)

---

## 3. Schema & Migration Management

- [ ] **Table creation improvements**
  - [ ] Support custom table names via attribute or fluent config
  - [ ] Optional `version` column for optimistic concurrency
  - [ ] Optional soft-delete (`deleted_at` column)
  - [ ] Configurable primary key types (GUID, string, int with auto-increment)

- [ ] **Index management**
  - [ ] `CreateIndexAsync<T>(Expression<Func<T, object>> jsonPath)` for JSON path indexes
  - [ ] Automatic index on `id` (already exists via PRIMARY KEY)
  - [ ] Composite index support
  - [ ] Index existence checking before creation

- [ ] **Schema versioning**
  - [ ] Simple migration table (`__jsonb_migrations`)
  - [ ] Up/down migration support
  - [ ] Schema introspection helpers

---

## 4. CRUD Operations Enhancement

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
  - [ ] `ExecuteAsync(string sql, object? param = null)`
  - [ ] `QueryAsync<TResult>(string sql, object? param = null)`
  - [ ] `QueryFirstOrDefaultAsync<TResult>(string sql, object? param = null)`
  - [ ] Direct access to Dapper's full API via `Connection` property (exists)

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

- [ ] **Custom exceptions**
  - [ ] `JsonbStoreException` base class
  - [ ] `TableNotFoundException`, `SerializationException`, `ConcurrencyException`
  - [ ] Preserve inner SQLite exceptions

- [ ] **Retry policies**
  - [ ] Configurable retry on `SQLITE_BUSY` and `SQLITE_LOCKED`
  - [ ] Exponential backoff with jitter
  - [ ] Optional Polly integration

- [ ] **Validation**
  - [ ] Validate ID is not null/empty before operations
  - [ ] Optional data validation via `IValidatableObject` or custom validator

---

## 9. Observability

- [ ] **Logging**
  - [ ] `ILogger<Repository>` integration
  - [ ] Log slow queries (configurable threshold)
  - [ ] Debug-level SQL logging

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

- [ ] **Unit tests** (mock connection)
  - [ ] Test all public API methods
  - [ ] Edge cases: null, empty, special characters in ID
  - [ ] Concurrency scenarios

- [ ] **Integration tests** (real SQLite)
  - [ ] In-memory database for speed
  - [ ] File-based database for WAL testing
  - [ ] Multi-connection concurrency tests

- [ ] **Test helpers**
  - [ ] `JsonbStoreTestFixture` for easy test setup
  - [ ] Database seeding utilities

---

## 12. Documentation & Developer Experience

- [ ] **XML documentation**
  - [ ] All public APIs documented (partially exists)
  - [ ] Include examples in `<example>` tags
  - [ ] Generate API reference site

- [ ] **README improvements**
  - [ ] Quick start guide with code examples
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
  - [ ] Source Link for debugging
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
