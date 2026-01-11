# Performance Investigations

Based on benchmark results from ComparisonBenchmark (January 2026), this document tracks performance investigations and optimization opportunities.

## Benchmark Results Summary

| Operation | LiteDocumentStore | Raw Dapper | LiteDB | Overhead vs Dapper |
|-----------|------------------|------------|--------|-------------------|
| Single Insert | 7,767 ns | 5,235 ns | 7,120 ns | **+48% (2,532ns)** |
| Bulk Insert (100 docs) | 730,924 ns | 490,044 ns | 400,536 ns | **+49% (240μs)** |
| Query By ID | 2,654 ns | 2,496 ns | 2,414 ns | **+6% (158ns)** ✅ |
| Full Scan | 2,389 ns | 2,285 ns | 699 ns | **+5% (104ns)** ✅ |
| Query with Filter | 4,663 ns | 3,394 ns | 3,262 ns | **+37% (1,269ns)** |
| Delete | 1,958 ns | 1,806 ns | 498 ns | **+8% (152ns)** ✅ |

**Memory Allocation:**
- Single Insert: 3.03 KB vs 2.24 KB (Dapper) - 35% more
- Bulk Insert: 246 KB vs 228 KB (Dapper) - 8% more ✅
- Query with Filter: 4.20 KB vs 2.03 KB (Dapper) - 107% more

---

## Investigation 1: Bulk Insert Performance Gap

**Status**: 🔴 Not Started  
**Priority**: HIGH  
**Current Gap**: 49% slower than raw Dapper (240μs overhead for 100 documents = 2.4μs per doc)

### Hypothesis

The bulk insert implementation may have inefficiencies in:
1. SQL generation happening per batch instead of being cached
2. Unnecessary object allocations during parameter preparation
3. Transaction overhead or connection state management
4. Serialization inefficiencies

### Investigation Steps

- [ ] **Profile bulk insert with dotTrace or Visual Studio Profiler**
  - Identify hot path and allocation sources
  - Check if SQL generation is called repeatedly
  
- [ ] **Review `SqlGenerator.GenerateBulkUpsertSql` implementation**
  - File: [LiteDocumentStore/Core/SqlGenerator.cs](src/LiteDocumentStore/Core/SqlGenerator.cs)
  - Check if string concatenation is optimal
  - Verify parameter binding efficiency

- [ ] **Review `UpsertManyAsync` implementation**
  - File: [LiteDocumentStore/Core/DocumentStore.cs](src/LiteDocumentStore/Core/DocumentStore.cs)
  - Check for unnecessary LINQ materializations
  - Verify serialization is only happening once per document

- [ ] **Compare raw Dapper code path vs DocumentStore**
  - Add logging/tracing to identify specific overhead sources
  - Measure time for: SQL generation, serialization, Dapper execution

- [ ] **Test with prepared statement approach**
  - Although rejected earlier for simple queries, bulk operations might benefit

### Success Criteria

- Reduce overhead to <20% (target: 600μs for 100 documents)
- Keep memory allocation delta under 10%

---

## Investigation 2: Single Insert Overhead

**Status**: 🔴 Not Started  
**Priority**: MEDIUM  
**Current Gap**: 48% slower than raw Dapper (2.5μs overhead per operation)

### Hypothesis

Single insert overhead likely comes from:
1. Method call overhead from abstraction layers
2. Table name resolution via `ITableNamingConvention`
3. SQL generation not being cached for simple operations
4. Additional allocations in the wrapper layer

### Investigation Steps

- [ ] **Profile single insert operation**
  - Measure time spent in: naming convention, SQL generation, serialization, Dapper call
  
- [ ] **Review `UpsertAsync` implementation**
  - File: [LiteDocumentStore/Core/DocumentStore.cs](src/LiteDocumentStore/Core/DocumentStore.cs)
  - Check parameter validation overhead
  - Verify logging doesn't add significant cost

- [ ] **Analyze memory allocations**
  - Current: 3.03 KB vs 2.24 KB (790 bytes overhead)
  - Identify allocation sources (strings? objects? arrays?)

- [ ] **Consider SQL caching for common operations**
  - Cache generated SQL per type (table name)
  - Balance memory usage vs performance gain

### Success Criteria

- Reduce overhead to <25% (target: 6,500ns per insert)
- Reduce memory allocation to <2.8 KB

---

## Investigation 3: Filtered Query Performance

**Status**: 🔴 Not Started  
**Priority**: HIGH  
**Current Gap**: 37% slower than raw Dapper, 107% more memory

### Hypothesis

Expression tree translation has significant overhead:
1. `ExpressionToJsonPath` translation happening per query
2. Complex expression visitor allocations
3. Generated SQL not being cached for common patterns
4. Result materialization inefficiencies

### Investigation Steps

- [ ] **Profile expression tree translation**
  - File: [LiteDocumentStore/Core/ExpressionToJsonPath.cs](src/LiteDocumentStore/Core/ExpressionToJsonPath.cs)
  - Measure translation time vs actual query execution time
  
- [ ] **Review `QueryAsync<T>(Expression<Func<T, bool>>)` implementation**
  - File: [LiteDocumentStore/Core/DocumentStore.cs](src/LiteDocumentStore/Core/DocumentStore.cs)
  - Check if expression compilation is happening multiple times
  - Verify result materialization path

- [ ] **Analyze memory allocations (4.20 KB vs 2.03 KB)**
  - Profile to identify specific allocation sources
  - Check for unnecessary LINQ operations (ToList, Select, etc.)

- [ ] **Consider expression caching**
  - Cache translated expressions using `Expression` as key
  - Use `ConcurrentDictionary` for thread safety
  - Measure memory vs performance tradeoff

- [ ] **Benchmark alternative implementations**
  - Direct JSON path string parameter
  - Pre-compiled expression approach

### Success Criteria

- Reduce overhead to <20% (target: 4,000ns)
- Reduce memory allocation to <3.0 KB
- Consider offering both convenience (Expression) and performance (direct SQL) options

---

## Investigation 4: Understanding LiteDB's Superior Performance

**Status**: 🟡 Observation Only  
**Priority**: LOW (informational)  
**LiteDB Advantages**: 3-4x faster on full scan and delete operations

### Observations

LiteDB significantly outperforms SQLite for:
- **Full Table Scan**: 699ns vs 2,389ns (3.4x faster)
- **Delete**: 498ns vs 1,958ns (3.9x faster)
- **Bulk Insert**: 401μs vs 731μs (1.8x faster)

However, LiteDB uses 2-4x more memory (10.48 KB vs 3.03 KB for single insert).

### Investigation Steps

- [ ] **Analyze LiteDB architecture**
  - Understand in-memory data structure optimizations
  - Review how it handles sequential access patterns
  - Check if results are "pre-deserialized" in memory

- [ ] **Consider hybrid approach**
  - Could DocumentStore detect sequential scan patterns?
  - Is there an intermediate caching layer that would help?

- [ ] **Document tradeoffs**
  - LiteDB trades memory for speed
  - SQLite/JSONB provides durability and relational capabilities
  - Make tradeoff explicit in documentation

### Outcome

Document when users should prefer:
- **LiteDocumentStore**: Hybrid relational + document, ACID transactions, SQL access
- **LiteDB**: Pure document store, in-memory speed, simpler use cases

---

## Investigation 5: Memory Allocation Patterns

**Status**: 🔴 Not Started  
**Priority**: MEDIUM

### Current Allocations vs Raw Dapper

| Operation | LiteDocumentStore | Raw Dapper | Overhead |
|-----------|------------------|------------|----------|
| Single Insert | 3.03 KB | 2.24 KB | +35% |
| Bulk Insert | 246 KB | 228 KB | +8% ✅ |
| Query By ID | 1.63 KB | 1.41 KB | +16% |
| Query Filtered | 4.20 KB | 2.03 KB | +107% 🔴 |
| Full Scan | 1.58 KB | 1.41 KB | +12% |
| Delete | 1.21 KB | 1.03 KB | +17% |

### Investigation Steps

- [ ] **Run benchmarks with allocation profiler**
  - Use BenchmarkDotNet's `[MemoryDiagnoser(displayGenColumns: true)]`
  - Identify Gen0/Gen1/Gen2 collection patterns

- [ ] **Review object pooling opportunities**
  - `JsonSerializerOptions` pooling
  - StringBuilder pooling for SQL generation
  - Byte array pooling for serialization buffers

- [ ] **Analyze string allocations**
  - Table name strings
  - SQL query strings
  - JSON path strings

- [ ] **Consider struct over class**
  - Internal DTOs that don't need reference semantics
  - Parameter objects for Dapper

### Success Criteria

- Keep allocation overhead under 20% for all operations
- Eliminate allocations in hot paths where possible

---

## Investigation 6: Virtual Column Index Performance Validation

**Status**: 🟢 Already Benchmarked (VirtualColumnBenchmark.cs)  
**Priority**: LOW (validation only)

### Existing Results

Virtual column benchmarks already demonstrate significant benefits of indexing JSON fields. Review existing benchmark results to document:

- [ ] Performance improvement with indexes (expected: 10-100x for large datasets)
- [ ] Memory tradeoffs
- [ ] When to recommend virtual columns to users

---

## Optimization Priority Matrix

| Investigation | Impact | Effort | Priority |
|---------------|--------|--------|----------|
| #1 Bulk Insert | High | Medium | **HIGH** ⭐ |
| #3 Filtered Query | High | Medium-High | **HIGH** ⭐ |
| #2 Single Insert | Medium | Low-Medium | **MEDIUM** |
| #5 Memory Patterns | Medium | Medium | **MEDIUM** |
| #4 LiteDB Comparison | Low | Low | **LOW** |
| #6 Virtual Columns | Low | Low | **LOW** |

---

## Performance Goals

### Phase 1: Quick Wins (Target: 1-2 weeks)
- Identify and eliminate obvious allocation sources
- Add SQL caching for common operations
- Profile hot paths to find low-hanging fruit

**Target**: Reduce write overhead to 25-30%, maintain <10% read overhead

### Phase 2: Deep Optimization (Target: 1 month)
- Optimize expression tree translation
- Implement smart caching strategies
- Consider object pooling for frequently allocated objects

**Target**: Reduce write overhead to 15-20%, maintain <10% read overhead

### Phase 3: Advanced Techniques (Target: 2-3 months)
- Investigate async I/O patterns
- Consider source generators for compile-time SQL generation
- Explore zero-allocation serialization paths

**Target**: Approach raw Dapper performance (5-10% overhead)

---

## Notes

- Always measure before and after optimizations
- Don't optimize prematurely - profile first
- Document any tradeoffs (memory vs speed, API convenience vs performance)
- Consider offering both "fast path" and "convenient path" APIs for power users
- The `Connection` property already provides an escape hatch for performance-critical code

---

## Tracking

**Created**: January 11, 2026  
**Last Updated**: January 11, 2026  
**Benchmark Version**: ComparisonBenchmark v1.0  
**Next Review**: After Investigation #1 and #3 are complete
