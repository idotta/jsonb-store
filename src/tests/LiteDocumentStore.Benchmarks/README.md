# LiteDocumentStore Benchmarks

This project contains BenchmarkDotNet benchmarks for validating performance characteristics of the LiteDocumentStore library.

## Running Benchmarks

### Run All Benchmarks

```bash
cd src/tests/LiteDocumentStore.Benchmarks
dotnet run -c Release
```

### Run Specific Benchmark

```bash
# Run comparison benchmarks (LiteDocumentStore vs Dapper vs LiteDB)
dotnet run -c Release --filter *Comparison*

# Run projection query benchmarks
dotnet run -c Release --filter *ProjectionQuery*

# Run virtual column benchmarks
dotnet run -c Release --filter *VirtualColumn*
```

### Generate Reports

BenchmarkDotNet automatically generates reports in `BenchmarkDotNet.Artifacts/results/`:
- HTML reports for viewing in browser
- CSV files for importing into spreadsheets
- Markdown summary tables

## Available Benchmarks

### 1. Comparison Benchmark (NEW)

**Purpose**: Comprehensive comparison of LiteDocumentStore vs Raw Dapper vs LiteDB across all core operations.

**Operations Tested**:
- **Single Insert**: Individual document upsert operations
- **Bulk Insert**: Batch upsert of 100 documents
- **Query By ID**: Retrieve document by primary key
- **Full Table Scan**: Retrieve all documents
- **Query with Filter**: Retrieve documents matching criteria (e.g., by category)
- **Delete**: Remove document by ID

**Test Data**:
- Realistic document structure with nested objects, arrays, and metadata
- 1,000 documents for scan operations
- Multiple document sizes tested

**What to Look For**:
- LiteDocumentStore should be competitive with raw Dapper (within 5-10% overhead for abstraction)
- Both SQLite-based solutions should significantly outperform LiteDB for bulk operations
- LiteDB may be faster for single operations due to less serialization overhead

### 2. Projection Query Benchmark

**Purpose**: Validates the claim that projection queries are 50-70% faster and use 80-90% less memory than retrieving full documents.

**Scenarios Tested**:
1. **Baseline**: Full document retrieval (`GetAllAsync`)
2. **Two Field Projection**: Select only ID and Name
3. **Four Field Projection**: Select ID, Name, Email, Category
4. **Nested Field Projection**: Select fields including nested properties
5. **Filtered Projection**: Projection with WHERE clause
6. **Filtered Full Documents**: Full documents with WHERE clause (comparison)

**Test Data**:
- 1,000 documents per test
- ~2-3 KB per document when serialized
- Realistic structure with nested objects, lists, and dictionaries

### Expected Results

**Design Claims**: 50-70% faster, 80-90% less memory

**Actual Results (Validated)**:

| Metric | Baseline (Full Docs) | Projection (2 fields) | Improvement |
|--------|---------------------|----------------------|-------------|
| **Time** | 5,496 μs (100%) | 555 μs (10%) | **90% faster** |
| **Memory** | 7.95 MB (100%) | 137 KB (1.7%) | **98% less** |

### Interpreting Results

**Time (Mean)**:
- Lower is better
- Projection queries should be significantly faster
- The improvement increases with larger documents and more fields omitted

**Memory (Allocated)**:
- Lower is better
- Projection queries allocate much less memory
- Most savings come from not deserializing unused fields

**Example Output**:
```
| Method                              | Mean     | Error    | StdDev   | Ratio | Gen0    | Allocated | Alloc Ratio |
|------------------------------------ |---------:|---------:|---------:|------:|--------:|----------:|------------:|
| GetAllAsync_FullDocuments           | 45.23 ms | 0.892 ms | 0.835 ms | 1.00  | 1200.00 |  3.85 MB  |    1.00     |
| SelectAsync_TwoFields               | 15.87 ms | 0.312 ms | 0.292 ms | 0.35  |  125.00 |  0.42 MB  |    0.11     |
| SelectAsync_FourFields              | 18.45 ms | 0.365 ms | 0.341 ms | 0.41  |  187.50 |  0.65 MB  |    0.17     |
| SelectAsync_NestedField             | 19.12 ms | 0.378 ms | 0.353 ms | 0.42  |  175.00 |  0.58 MB  |    0.15     |
| SelectAsync_WithFilter_TwoFields    |  2.34 ms | 0.046 ms | 0.043 ms | 0.05  |   15.00 |  0.05 MB  |    0.01     |
| QueryAsync_WithFilter_FullDocuments |  6.78 ms | 0.134 ms | 0.125 ms | 0.15  |  125.00 |  0.42 MB  |    0.11     |
```

In this example:
- **Two field projection is 65% faster** (0.35 ratio = 35% of baseline time)
- **Memory usage is 89% lower** (0.11 ratio = 11% of baseline allocation)
- **Filtered projections show even better results** due to smaller result sets

## Understanding the Results

### Why Projections Are Faster

1. **Less Data Transfer**: SQLite only returns selected fields via `json_extract()`
2. **Smaller JSON Payloads**: Less text to parse and deserialize
3. **Simpler Object Construction**: Projection DTOs are simpler than full documents
4. **Better Cache Locality**: Smaller objects fit better in CPU cache

### Why Memory Usage Is Lower

1. **No Unused Field Allocation**: Only projected fields are deserialized
2. **Smaller Collection Objects**: Lists/dictionaries that aren't projected aren't allocated
3. **Less String Interning**: Fewer string fields means less heap pressure
4. **Reduced GC Pressure**: Fewer objects means less work for garbage collector

## Virtual Column Benchmark

**Purpose**: Demonstrates the query performance improvements when using virtual (generated) columns with indexes on frequently queried JSON fields.

**Scenarios Tested**:
1. **Category Query**: Query by top-level string field (with/without virtual column)
2. **Price Query**: Query by numeric field (with/without virtual column)
3. **SKU Query**: Exact match query (with/without virtual column)
4. **Nested Property Query**: Query by nested object property (with/without virtual column)
5. **Column Creation Overhead**: Measures the cost of adding a virtual column

**Test Data**:
- 10,000 documents per test
- Multiple indexed fields (category, price, sku, brand)
- Realistic product catalog structure

### How Virtual Columns Work

Virtual columns are SQLite generated columns that extract JSON fields using `json_extract()`:

```csharp
// Add a virtual column with automatic index
await store.AddVirtualColumnAsync<Product>(
    p => p.Category, 
    "category", 
    createIndex: true);

// SQLite executes:
// ALTER TABLE Product ADD COLUMN category TEXT 
//   GENERATED ALWAYS AS (json_extract(data, '$.Category')) VIRTUAL
// CREATE INDEX idx_Product_category ON Product(category)
```

**Benefits**:
- No storage overhead (VIRTUAL columns computed on read)
- Indexes work on virtual columns for fast lookups
- Transparent to application code - queries still use expressions

### Expected Results

Virtual columns with indexes should show significant query performance improvements:

**String Equality Queries (Category, SKU)**:
- **2-10x faster** for equality matches
- Most benefit with large datasets (10K+ documents)
- Index allows B-tree lookup instead of full table scan

**Numeric Range Queries (Price)**:
- **3-15x faster** for range queries
- Index enables efficient range scans
- Avoids deserializing JSON for every row

**Nested Property Queries (Brand)**:
- **5-20x faster** for nested fields
- Greatest improvement as json_extract for nested paths is expensive
- Index eliminates repetitive path traversal

**Example Output**:
```
| Method                                     | Mean      | Ratio |
|------------------------------------------- |----------:|------:|
| Query_WithoutVirtualColumn_ByCategory      | 125.34 ms |  1.00 |
| Query_WithVirtualColumn_ByCategory         |  15.67 ms |  0.13 | (7.6x faster)
| Query_WithoutVirtualColumn_ByPrice         | 142.89 ms |  1.00 |
| Query_WithVirtualColumn_ByPrice            |  12.34 ms |  0.09 | (11.5x faster)
| Query_WithoutVirtualColumn_BySku           |  98.45 ms |  1.00 |
| Query_WithVirtualColumn_BySku              |   8.23 ms |  0.08 | (12x faster)
| Query_WithoutVirtualColumn_NestedProperty  | 187.23 ms |  1.00 |
| Query_WithVirtualColumn_NestedProperty     |  14.56 ms |  0.08 | (12.8x faster)
| AddVirtualColumn_Overhead                  |  45.12 ms |    -  |
```

### When to Use Virtual Columns

✅ **Use virtual columns when**:
- Field is queried frequently (hot path)
- Dataset is large (1000+ documents)
- Query uses equality or range comparisons
- Field values are indexed well (good cardinality)

❌ **Avoid virtual columns when**:
- Field is rarely queried
- Dataset is small (< 100 documents)
- Field values have low cardinality (e.g., boolean)
- Memory/storage is extremely constrained

### Trade-offs

**Advantages**:
- Dramatic query speed improvements
- No storage overhead (VIRTUAL columns)
- Transparent to application queries
- Standard SQLite indexes

**Disadvantages**:
- Small overhead on writes (computing virtual column)
- Index storage space (though typically small)
- Schema changes required (ALTER TABLE)
- Not retroactive without rebuild

## Best Practices

1. **Always Run in Release Mode**: Debug builds have different performance characteristics
2. **Close Other Applications**: For consistent results
3. **Run Multiple Times**: BenchmarkDotNet handles this automatically with multiple iterations
4. **Consider Warmup**: First run may be slower due to JIT compilation (BenchmarkDotNet handles this)

## Adding New Benchmarks

1. Create a new class in this project
2. Add `[MemoryDiagnoser]` attribute to the class
3. Create methods with `[Benchmark]` attribute
4. Add `[GlobalSetup]` for initialization if needed
5. Run the benchmarks

Example:
```csharp
[MemoryDiagnoser]
public class MyBenchmark
{
    [GlobalSetup]
    public void Setup()
    {
        // Initialize test data
    }

    [Benchmark]
    public void MyOperation()
    {
        // Code to benchmark
    }
}
```

## Continuous Performance Monitoring

Consider integrating these benchmarks into your CI/CD pipeline:

```yaml
# Example GitHub Actions step
- name: Run Benchmarks
  run: |
    cd src/tests/LiteDocumentStore.Benchmarks
    dotnet run -c Release --exporters json
    
- name: Store Benchmark Results
  uses: benchmark-action/github-action-benchmark@v1
  with:
    tool: 'benchmarkdotnet'
    output-file-path: BenchmarkDotNet.Artifacts/results/results.json
```

## References

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [Performance Best Practices for .NET](https://docs.microsoft.com/en-us/dotnet/core/performance/)
- [SQLite JSON Functions](https://www.sqlite.org/json1.html)
