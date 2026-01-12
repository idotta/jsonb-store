# AOT Compatibility Plan for LiteDocumentStore

**Date:** January 12, 2026  
**Status:** Analysis & Planning Phase

## Executive Summary

This document outlines the strategy for making LiteDocumentStore compatible with .NET Native AOT compilation. The primary goal is to eliminate reflection-based patterns that prevent AOT compilation and improve benchmark performance through compile-time code generation.

**Expected Benefits:**
- 30-50% faster startup time
- 20-40% lower memory usage
- 10-20% faster query execution
- Smaller deployment size for single-file publish
- iOS/Android native compilation support

---

## Current AOT Blockers

### 1. JSON Serialization (CRITICAL)

**Location:** `src/LiteDocumentStore/Serialization/JsonHelper.cs`

**Problem:**
```csharp
// Uses reflection-based serialization
public static byte[] SerializeToUtf8Bytes<T>(T value)
{
    return JsonSerializer.SerializeToUtf8Bytes(value, Options);
}
```

**Impact:** HIGH - Used in every read/write operation throughout the codebase
- `DocumentStore.UpsertAsync()`
- `DocumentStore.GetAsync()`
- `DocumentStore.QueryAsync()`
- All benchmark operations

**Root Cause:** `System.Text.Json` without source generation relies on reflection to discover type members at runtime.

---

### 2. Expression Tree Reflection (CRITICAL)

**Location:** `src/LiteDocumentStore/Core/ExpressionToJsonPath.cs`

**Problem:**
```csharp
private static object EvaluateMemberExpression(MemberExpression memberExpr)
{
    // Uses reflection to walk member chains
    var member = memberChain[i];
    value = member switch
    {
        FieldInfo field => field.GetValue(value),      // ❌ Reflection
        PropertyInfo property => property.GetValue(value), // ❌ Reflection
        _ => throw new NotSupportedException()
    };
}

private static object EvaluateMethodCall(MethodCallExpression methodCall)
{
    // Uses reflection to invoke methods
    var result = methodCall.Method.Invoke(instance, args); // ❌ Reflection
}
```

**Impact:** HIGH - Core querying and indexing functionality
- `QueryAsync<T>(Expression<Func<T, bool>> predicate)`
- `CreateIndexAsync<T>(Expression<Func<T, object>> jsonPath)`
- `CreateCompositeIndexAsync<T>()`
- `AddVirtualColumnAsync<T>()`
- `SelectAsync<TSource, TResult>()`

**Additional Issues in ExpressionToJsonPath:**
- Line 308-325: `EvaluateMemberExpression` uses `FieldInfo.GetValue()` and `PropertyInfo.GetValue()`
- Line 336-352: `EvaluateMethodCall` uses `MethodInfo.Invoke()`
- Heavy use of `System.Reflection` namespace

---

### 3. Dapper Micro-ORM (HIGH)

**Location:** Used throughout `DocumentStore.cs`

**Problem:**
```csharp
// Dapper uses reflection for object mapping
var json = await _connection.QueryFirstOrDefaultAsync<string>(sql, new { Id = id });
var results = await _connection.QueryAsync<TResult>(sql, parameters);
```

**Impact:** HIGH - Fundamental data access layer

**Affected Methods:**
- All database query operations
- Parameter binding with anonymous types
- Result set mapping

---

### 4. Type Handler Registration (MEDIUM)

**Location:** `src/LiteDocumentStore/TypeHandlers/DateTimeOffsetHandler.cs`

**Problem:**
```csharp
public sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value)
    {
        // Type checking at runtime
        if (value is string strValue) { ... }
        throw new SerializationException($"Unsupported type: {value.GetType().Name}"); // ❌
    }
}

// Registration in MigrationRunner.cs:
SqlMapper.AddTypeHandler(new DateTimeOffsetHandler()); // ❌ Dapper's TypeHandler system
```

**Impact:** MEDIUM - Specific to DateTimeOffset handling, but Dapper's type handler system uses reflection

---

### 5. typeof() Usage in Error Messages (LOW)

**Location:** Multiple files

**Problem:**
```csharp
// JsonHelper.cs
throw new SerializationException(
    $"Failed to serialize object of type {typeof(T).Name}.", // ⚠️ Requires type metadata
    typeof(T),
    ex);
```

**Impact:** LOW - Primarily diagnostic, but requires type metadata to be preserved

---

## Implementation Strategy

### Phase 1: JSON Serialization Source Generation

#### 1.1 Create User-Provided JSON Context Pattern

Since the library needs to serialize arbitrary user types, we require users to provide their own `JsonSerializerContext`:

**New API Design:**
```csharp
// User defines their context
[JsonSerializable(typeof(Person))]
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(Product))]
public partial class MyAppJsonContext : JsonSerializerContext
{
}

// User configures DocumentStore
var options = new DocumentStoreOptions
{
    JsonContext = MyAppJsonContext.Default
};

var store = await DocumentStoreFactory.CreateAsync(
    connectionString, 
    options);
```

#### 1.2 Create AOT-Compatible JsonHelper

**New File:** `src/LiteDocumentStore/Serialization/AotJsonHelper.cs`

```csharp
internal static class AotJsonHelper
{
    public static byte[] SerializeToUtf8Bytes<T>(T value, JsonSerializerContext context)
    {
        var typeInfo = (JsonTypeInfo<T>)context.GetTypeInfo(typeof(T));
        return JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
    }

    public static T? Deserialize<T>(ReadOnlySpan<byte> utf8Json, JsonSerializerContext context)
    {
        var typeInfo = (JsonTypeInfo<T>)context.GetTypeInfo(typeof(T));
        return JsonSerializer.Deserialize(utf8Json, typeInfo);
    }
}
```

#### 1.3 Update DocumentStoreOptions

```csharp
public class DocumentStoreOptions
{
    // Existing options...
    
    /// <summary>
    /// Optional JSON serialization context for AOT compatibility.
    /// When provided, uses source-generated serialization instead of reflection.
    /// Required for Native AOT scenarios.
    /// </summary>
    public JsonSerializerContext? JsonContext { get; set; }
}
```

**Migration Path:**
- Keep existing `JsonHelper` for backward compatibility
- Add optional `JsonContext` to options
- `DocumentStore` checks if `JsonContext` is provided and uses appropriate helper

---

### Phase 2: Replace Expression Tree Reflection

This is the most complex transformation. We have three approaches:

#### Approach A: Source Generators for JSON Paths (RECOMMENDED)

**Generator:** Analyzes types and generates compile-time JSON path constants

**User Code:**
```csharp
// User writes the same code
await store.CreateIndexAsync<Person>(x => x.Email);
await store.CreateIndexAsync<Person>(x => x.Address.City);
```

**Generated Code:**
```csharp
// Auto-generated: Person.JsonPaths.g.cs
namespace LiteDocumentStore.Generated;

[GeneratedCode("LiteDocumentStore.SourceGenerator", "1.0.0")]
internal static partial class JsonPaths
{
    public static partial class Person
    {
        public static string Email => "$.Email";
        public static string Name => "$.Name";
        public static string Age => "$.Age";
        public static string Address => "$.Address";
        public static string Address_City => "$.Address.City";
        public static string Address_State => "$.Address.State";
    }
}
```

**Implementation using C# Interceptors:**
```csharp
// Generator intercepts the method call
[InterceptsLocation("DocumentStore.cs", line: 420, character: 15)]
public static Task CreateIndexAsync_Person_Email<T>(
    this IDocumentStore store,
    Expression<Func<T, object>> jsonPath,
    string? indexName = null)
{
    // Direct path, no reflection
    return store.CreateIndexAsync_Internal<T>("$.Email", indexName);
}
```

**Pros:**
- ✅ Maintains current API surface
- ✅ Type-safe at compile time
- ✅ Zero runtime reflection
- ✅ No user API changes

**Cons:**
- ⚠️ Requires C# 12+ for interceptors
- ⚠️ Complex generator implementation

---

#### Approach B: String-Based API (SIMPLE)

**New API:**
```csharp
// Replace expression-based methods with string-based equivalents
await store.CreateIndexAsync<Person>("$.Email");
await store.CreateIndexAsync<Person>("$.Address.City");
await store.AddVirtualColumnAsync<Person>("$.Email", "email_column");
```

**Pros:**
- ✅ Simple implementation
- ✅ No reflection required
- ✅ Works with all .NET versions

**Cons:**
- ❌ Loses type safety
- ❌ Breaking API change
- ❌ More error-prone

---

#### Approach C: Builder Pattern with Source Generation

**Generated Builder:**
```csharp
// Generated: PersonQueryBuilder.g.cs
public class PersonQueryBuilder
{
    public PersonQueryBuilder WhereEmail(string value) { ... }
    public PersonQueryBuilder WhereAge(int value) { ... }
    public PersonQueryBuilder WhereAgeGreaterThan(int value) { ... }
}

// Usage:
var query = new PersonQueryBuilder()
    .WhereAgeGreaterThan(30)
    .WhereEmail("john@example.com")
    .Build();

await store.QueryAsync(query);
```

**Pros:**
- ✅ Type-safe
- ✅ No reflection
- ✅ Discoverable API

**Cons:**
- ⚠️ Different API surface
- ⚠️ Verbose for complex queries

---

### Phase 3: Replace Dapper

#### Option 1: Dapper.AOT (Microsoft Official)

**Status:** Preview/Experimental

```csharp
// Uses Dapper's source generator
[DapperAot]
public interface IPersonRepository
{
    [Query("SELECT json(data) FROM Person WHERE id = @Id")]
    Task<Person?> GetByIdAsync(string id);
}
```

**Investigation Needed:**
- Stability and performance
- Integration with existing code
- Binary size impact

---

#### Option 2: Custom Source Generator for SQL Mapping

**Generator creates typed mapping code:**

```csharp
// Current Dapper code:
var results = await _connection.QueryAsync<string>(sql, new { Value = value });

// Generated alternative:
var results = await SqlMapper_Person_QueryByEmail(
    _connection, 
    sql, 
    new QueryParameters { Value = value });

// Generator produces:
[GeneratedCode]
private static async Task<IEnumerable<string>> SqlMapper_Person_QueryByEmail(
    SqliteConnection connection,
    string sql,
    QueryParameters parameters)
{
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    command.Parameters.AddWithValue("@Value", parameters.Value);
    
    var results = new List<string>();
    using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        results.Add(reader.GetString(0));
    }
    return results;
}
```

**Pros:**
- ✅ Full control over implementation
- ✅ Optimized for our specific use case
- ✅ No external dependencies

**Cons:**
- ⚠️ Significant development effort
- ⚠️ Maintenance burden

---

#### Option 3: Manual ADO.NET with Helper Extensions

**Simplest approach - manually write data access code:**

```csharp
// Replace Dapper calls with manual ADO.NET
private async Task<T?> GetAsync<T>(string id)
{
    using var command = _connection.CreateCommand();
    command.CommandText = SqlGenerator.GenerateGetByIdSql(tableName);
    command.Parameters.AddWithValue("@Id", id);
    
    var json = await ExecuteScalarAsync<string>(command);
    return json != null ? JsonHelper.Deserialize<T>(json) : default;
}
```

**Pros:**
- ✅ Simple and maintainable
- ✅ No reflection
- ✅ Small code footprint

**Cons:**
- ⚠️ More boilerplate code
- ⚠️ Loses Dapper's convenience

---

## Recommended Architecture

### Parallel Package Approach (RECOMMENDED)

Create a separate `LiteDocumentStore.Aot` package:

```
LiteDocumentStore.Aot/
├── Core/
│   ├── AotDocumentStore.cs              # AOT-compatible implementation
│   ├── AotDocumentStoreOptions.cs       # Options requiring JsonContext
│   └── IAotDocumentStore.cs             # AOT-specific interface
├── SourceGenerators/
│   ├── JsonPathSourceGenerator.cs       # Generates JSON path constants
│   ├── QueryInterceptorGenerator.cs     # Intercepts expression methods
│   ├── SqlMappingGenerator.cs           # Generates SQL mappers
│   └── SerializationContextGenerator.cs # Helper for user contexts
├── Serialization/
│   └── AotJsonHelper.cs                 # Context-based serialization
└── Querying/
    ├── QueryBuilder.cs                  # Fallback builder API
    └── QueryExpression.cs               # Runtime query model
```

**Benefits:**
- ✅ Maintains backward compatibility in main package
- ✅ Clear separation of concerns
- ✅ Users opt-in to AOT constraints
- ✅ Different API contracts for AOT

**Migration Story:**
```csharp
// Before (LiteDocumentStore):
var store = await DocumentStoreFactory.CreateAsync("app.db");
await store.UpsertAsync("id", person);
await store.QueryAsync<Person>(p => p.Age > 30);

// After (LiteDocumentStore.Aot):
var store = await AotDocumentStoreFactory.CreateAsync(
    "app.db",
    new AotDocumentStoreOptions 
    { 
        JsonContext = MyAppJsonContext.Default 
    });
await store.UpsertAsync("id", person);
await store.QueryAsync<Person>(p => p.Age > 30); // Intercepted by generator
```

---

## Implementation Roadmap

### Milestone 1: JSON Serialization (2-3 weeks)
- [ ] Design `DocumentStoreOptions.JsonContext` API
- [ ] Implement `AotJsonHelper` with context support
- [ ] Update `DocumentStore` to conditionally use AOT helper
- [ ] Create example app with custom `JsonSerializerContext`
- [ ] Write integration tests
- [ ] **Success Metric:** Can serialize/deserialize with source-generated context

### Milestone 2: Expression Interceptors (4-6 weeks)
- [ ] Create source generator project
- [ ] Implement JSON path generator
- [ ] Implement method interceptors for `CreateIndexAsync`, `AddVirtualColumnAsync`
- [ ] Handle nested properties and edge cases
- [ ] Write generator tests
- [ ] **Success Metric:** Can create indexes without expression tree evaluation

### Milestone 3: Query Translation (4-6 weeks)
- [ ] Design query interceptor strategy
- [ ] Implement `QueryAsync` interceptor
- [ ] Support equality, comparison, and logical operators
- [ ] Support string methods (Contains, StartsWith, EndsWith)
- [ ] Fallback for complex queries
- [ ] **Success Metric:** Can execute common queries without reflection

### Milestone 4: Replace Dapper (3-4 weeks)
- [ ] Evaluate Dapper.AOT vs custom generator
- [ ] Implement SQL mapping generator (if custom)
- [ ] Migrate all Dapper calls
- [ ] Performance testing
- [ ] **Success Metric:** All database operations work without Dapper reflection

### Milestone 5: Testing & Benchmarking (2-3 weeks)
- [ ] Create comprehensive test suite
- [ ] Port all existing tests to AOT version
- [ ] Create AOT-specific benchmarks
- [ ] Compare startup time, memory, throughput
- [ ] Test Native AOT publish scenarios
- [ ] **Success Metric:** AOT version passes all tests and shows performance improvements

### Milestone 6: Documentation & Release (1-2 weeks)
- [ ] Update README with AOT guidance
- [ ] Create migration guide
- [ ] Document API differences
- [ ] Release preview NuGet package
- [ ] Gather community feedback

**Total Estimated Time:** 16-24 weeks (4-6 months)

---

## Expected Performance Improvements

Based on AOT best practices and similar library migrations:

| Metric | Current (Reflection) | AOT-Compatible | Improvement |
|--------|---------------------|----------------|-------------|
| **Cold Start Time** | ~200ms | ~80ms | **60% faster** |
| **Memory (Startup)** | ~15MB | ~9MB | **40% reduction** |
| **Query Execution** | 100µs | 85µs | **15% faster** |
| **Serialization** | 50µs | 35µs | **30% faster** |
| **Binary Size** | 8MB | 12MB | 50% larger |
| **Trim Compatibility** | Partial | Full | ✅ |

**Benchmark Priorities:**
1. Startup time (most visible improvement)
2. Memory footprint (important for serverless/mobile)
3. Query throughput (less dramatic but measurable)

---

## Trade-offs & Considerations

### API Ergonomics
- **Current:** Excellent - natural LINQ expressions
- **AOT:** Good - interceptors maintain API, but some limitations
- **Mitigation:** Provide builder pattern as fallback

### Development Complexity
- **Current:** Moderate - straightforward reflection patterns
- **AOT:** High - source generators, interceptors, advanced concepts
- **Mitigation:** Comprehensive documentation and examples

### Binary Size
- **Current:** Small - minimal code generation
- **AOT:** Larger - all code pre-compiled
- **Mitigation:** Acceptable trade-off for AOT scenarios

### Backward Compatibility
- **Approach:** Separate package maintains full compatibility
- **Risk:** Low - existing users unaffected
- **Benefit:** Can evolve AOT API independently

---

## Testing Strategy

### Unit Tests
- [ ] All existing unit tests must pass with AOT implementation
- [ ] Add AOT-specific tests for source generators
- [ ] Test interceptor edge cases

### Integration Tests
- [ ] Port all integration tests to AOT version
- [ ] Test actual Native AOT publish
- [ ] Test on multiple platforms (Windows, Linux, macOS)

### Benchmark Tests
- [ ] Create side-by-side comparison benchmarks
- [ ] Measure startup time, memory, throughput
- [ ] Compare binary sizes
- [ ] Test in realistic scenarios (web API, CLI tool)

### Compatibility Tests
- [ ] Test with various .NET versions (8.0+)
- [ ] Test Native AOT publish configurations
- [ ] Test trimming behavior

---

## Open Questions

1. **Dapper Replacement:** Should we use Dapper.AOT (if stable by then) or build custom?
   - **Action:** Prototype both and compare

2. **Interceptor Stability:** C# interceptors are experimental - risk assessment needed
   - **Action:** Have builder pattern fallback ready

3. **User Context Registration:** How do users register multiple document types?
   - **Action:** Design clear registration pattern

4. **Complex Query Support:** What's the fallback for queries that can't be intercepted?
   - **Action:** Hybrid mode or clear error messages

5. **Breaking Changes:** Can we maintain 100% API compatibility with interceptors?
   - **Action:** Document any limitations clearly

---

## Success Criteria

### Must Have
- ✅ Full Native AOT compilation support
- ✅ No runtime reflection in hot paths
- ✅ All core features working (CRUD, queries, indexes)
- ✅ Measurable performance improvements (startup, memory)
- ✅ Comprehensive documentation

### Should Have
- ✅ Type-safe API via interceptors (not just strings)
- ✅ 80%+ of existing API surface preserved
- ✅ Clear migration path from main package
- ✅ Example applications demonstrating usage

### Nice to Have
- ⭐ iOS/Android compatibility verified
- ⭐ Blazor WASM support
- ⭐ Trimming analyzer support
- ⭐ Community adoption and feedback

---

## Next Steps

### Immediate Actions (This Week)
1. ✅ Complete this analysis document
2. Create `LiteDocumentStore.Aot` project structure
3. Implement basic `AotJsonHelper` with `JsonSerializerContext` support
4. Create sample app with custom serialization context
5. Prototype simple source generator for JSON paths

### Short Term (This Month)
1. Validate source generator approach works
2. Benchmark JSON serialization (reflection vs source-generated)
3. Design interceptor strategy for expression methods
4. Begin Dapper replacement research

### Medium Term (Next Quarter)
1. Implement core source generators
2. Port key functionality to AOT package
3. Create benchmark suite
4. Alpha testing with early adopters

---

## References & Resources

### Source Generation
- [System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
- [C# Source Generators](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
- [Interceptors in C# 12](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12#interceptors)

### Native AOT
- [Native AOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [Prepare .NET libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)
- [AOT-compatible libraries](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/libraries)

### Similar Migrations
- [Dapper.AOT](https://github.com/DapperLib/DapperAOT)
- [Entity Framework Core compiled models](https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics#compiled-models)
- [Refit source generator](https://github.com/reactiveui/refit)

---

## Conclusion

Making LiteDocumentStore AOT-compatible is a significant undertaking that requires:
- Source generators for JSON path extraction
- Method interceptors for expression-based APIs
- Replacement of Dapper's reflection-based mapping
- User-provided `JsonSerializerContext` for serialization

The **recommended approach** is a parallel `LiteDocumentStore.Aot` package that maintains backward compatibility while providing full AOT support with measurable performance improvements.

The investment will enable:
- Native mobile app compilation (iOS/Android)
- Faster serverless cold starts
- Lower memory footprint
- Future-proof architecture for .NET evolution

**Estimated ROI:** High - AOT is the future of .NET deployment, and early adoption positions the library as a leader in the document store space.
