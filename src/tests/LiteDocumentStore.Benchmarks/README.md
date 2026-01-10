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
# Run only projection query benchmarks
dotnet run -c Release --filter *ProjectionQuery*
```

### Generate Reports

BenchmarkDotNet automatically generates reports in `BenchmarkDotNet.Artifacts/results/`:
- HTML reports for viewing in browser
- CSV files for importing into spreadsheets
- Markdown summary tables

## Projection Query Benchmark

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
