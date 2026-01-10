# Quick Start: Running Projection Query Benchmarks

## Prerequisites

- .NET 10.0 SDK installed
- Windows (for BenchmarkDotNet.Diagnostics.Windows)

## Run the Benchmark

```bash
cd src/tests/LiteDocumentStore.Benchmarks
dotnet run -c Release
```

## What Gets Measured

The benchmark tests **1,000 documents** (~2-3 KB each) with these scenarios:

1. **Baseline**: Retrieve all fields (`GetAllAsync`)
2. **2-Field Projection**: Select only `Id` and `Name`
3. **4-Field Projection**: Select `Id`, `Name`, `Email`, `Category`
4. **Nested Field Projection**: Select fields including nested properties
5. **Filtered 2-Field Projection**: With WHERE clause
6. **Filtered Full Documents**: Full documents with WHERE clause

## Expected Results

### Time Improvement ✅ VALIDATED
- **2-Field Projection**: **90% faster** (10% of baseline time)
- **4-Field Projection**: **81% faster** (19% of baseline time)
- **Nested Fields**: **83% faster** (17% of baseline time)
- **With Filter**: **96% faster** (4% of baseline time)
- **Original Target**: 50-70% faster - **EXCEEDED!**

### Memory Improvement ✅ VALIDATED
- **2-Field Projection**: **98% less memory** (1.7% of baseline)
- **4-Field Projection**: **97% less memory** (3.3% of baseline)
- **Nested Fields**: **98% less memory** (2.5% of baseline)
- **With Filter**: **99% less memory** (0.3% of baseline)
- **Original Target**: 80-90% less - **EXCEEDED!**

## Actual Results

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.7462/24H2/2024Update/HudsonValley)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.101

| Method                              | Mean       | Ratio | Allocated  | Alloc Ratio |
|------------------------------------ |-----------:|------:|-----------:|------------:|
| GetAllAsync_FullDocuments           | 5,495.9 μs |  1.00 | 7946.65 KB |       1.000 |
| SelectAsync_TwoFields               |   555.1 μs |  0.10 |  137.38 KB |       0.017 |
| SelectAsync_FourFields              | 1,057.9 μs |  0.19 |  264.49 KB |       0.033 |
| SelectAsync_NestedField             |   914.1 μs |  0.17 |  201.64 KB |       0.025 |
| SelectAsync_WithFilter_TwoFields    |   218.7 μs |  0.04 |   20.18 KB |       0.003 |
| QueryAsync_WithFilter_FullDocuments |   676.3 μs |  0.12 |  798.25 KB |       0.100 |
```

**Analysis**:
- ✅ Two-field projection is **90% faster** (0.10 ratio)
- ✅ Memory usage is **98% lower** (0.017 ratio)
- ✅ Claims exceeded! Real performance is even better than documented!

## Interpreting the Results

### Ratio Column
- Shows performance relative to baseline
- **0.35 = 35%** of baseline time (65% faster)
- **Lower is better**

### Alloc Ratio Column  
- Shows memory allocation relative to baseline
- **0.11 = 11%** of baseline memory (89% reduction)
- **Lower is better**

## View Detailed Reports

After running, check `BenchmarkDotNet.Artifacts/results/`:
- `*-report.html` - Interactive HTML report
- `*-report.csv` - CSV for spreadsheet analysis
- `*-report-github.md` - Markdown summary

## Troubleshooting

### "Process terminated"
- Ensure you're running in Release mode (`-c Release`)
- Close other memory-intensive applications

### Slow first run
- JIT compilation and warmup take time
- BenchmarkDotNet runs multiple iterations automatically
- First iteration is warmup (not measured)

### Different results
- CPU/memory varies by machine
- **Relative ratios** (0.35, 0.11) are what matter, not absolute times
- Trend should be consistent: projections much faster with less memory

## Next Steps

1. Run the benchmark: `dotnet run -c Release`
2. Review HTML report in BenchmarkDotNet.Artifacts/results/
3. Compare ratios to verify 50-70% speed improvement and 80-90% memory reduction
4. Share results to validate performance claims!
