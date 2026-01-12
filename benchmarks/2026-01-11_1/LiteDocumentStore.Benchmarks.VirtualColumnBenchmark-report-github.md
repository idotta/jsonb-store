```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.7462/24H2/2024Update/HudsonValley)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3
  Job-MNMNNY : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3

IterationCount=15  RunStrategy=Throughput  

```
| Method                                                | Mean           | Error          | StdDev         | Median         | Ratio  | RatioSD | Gen0      | Gen1      | Gen2      | Allocated   | Alloc Ratio |
|------------------------------------------------------ |---------------:|---------------:|---------------:|---------------:|-------:|--------:|----------:|----------:|----------:|------------:|------------:|
| &#39;Query by category WITHOUT virtual column&#39;            |  11,845.220 μs |    193.2651 μs |    171.3245 μs |  11,799.940 μs |  1.000 |    0.02 |  125.0000 |  109.3750 |         - |  1536.72 KB |       1.000 |
| &#39;Query by category WITH virtual column and index&#39;     |   3,885.780 μs |    169.9684 μs |    141.9313 μs |   3,865.772 μs |  0.328 |    0.01 |  125.0000 |  109.3750 |         - |  1536.57 KB |       1.000 |
| &#39;Query by price WITHOUT virtual column&#39;               |  10,231.184 μs |    232.5639 μs |    194.2014 μs |  10,165.688 μs |  0.864 |    0.02 |         - |         - |         - |     4.13 KB |       0.003 |
| &#39;Query by price WITH virtual column and index&#39;        | 145,613.702 μs |  3,038.4501 μs |  2,842.1681 μs | 145,310.625 μs | 12.295 |    0.29 | 4500.0000 | 3500.0000 | 1000.0000 | 45427.91 KB |      29.562 |
| &#39;Query by SKU WITHOUT virtual column&#39;                 |  10,539.065 μs |    271.6209 μs |    240.7848 μs |  10,444.916 μs |  0.890 |    0.02 |         - |         - |         - |     5.47 KB |       0.004 |
| &#39;Query by SKU WITH virtual column and index&#39;          |       8.526 μs |      0.3116 μs |      0.2915 μs |       8.461 μs |  0.001 |    0.00 |    0.4272 |         - |         - |     5.32 KB |       0.003 |
| &#39;Query nested property WITHOUT virtual column&#39;        |  13,655.097 μs |    259.1253 μs |    242.3860 μs |  13,614.769 μs |  1.153 |    0.03 |   62.5000 |   31.2500 |         - |   765.91 KB |       0.498 |
| &#39;Query nested property WITH virtual column and index&#39; |   1,932.391 μs |     45.7197 μs |     38.1781 μs |   1,929.862 μs |  0.163 |    0.00 |   54.6875 |   15.6250 |         - |    765.7 KB |       0.498 |
| &#39;Raw SQL: Category query (indexed)&#39;                   |   2,566.287 μs |    102.9766 μs |     85.9901 μs |   2,558.164 μs |  0.217 |    0.01 |   15.6250 |    3.9063 |         - |   212.68 KB |       0.138 |
| &#39;Raw SQL: Category query (no index)&#39;                  |  10,711.039 μs |    367.9478 μs |    344.1786 μs |  10,673.506 μs |  0.904 |    0.03 |   15.6250 |         - |         - |    212.7 KB |       0.138 |
| &#39;Raw SQL: Price query (indexed)&#39;                      |  75,931.040 μs |  1,505.4813 μs |  1,408.2281 μs |  76,245.671 μs |  6.411 |    0.15 |  571.4286 |  428.5714 |  142.8571 |  6425.67 KB |       4.181 |
| &#39;Raw SQL: Price query (no index)&#39;                     |  19,184.264 μs |    251.6769 μs |    235.4187 μs |  19,247.331 μs |  1.620 |    0.03 |  500.0000 |  468.7500 |  218.7500 |  6425.11 KB |       4.181 |
| &#39;Raw SQL: SKU query (indexed)&#39;                        |       5.035 μs |      0.0966 μs |      0.0903 μs |       5.085 μs |  0.000 |    0.00 |    0.1068 |         - |         - |     1.37 KB |       0.001 |
| &#39;Raw SQL: SKU query (no index)&#39;                       |  10,272.874 μs |     38.9244 μs |     34.5055 μs |  10,276.459 μs |  0.867 |    0.01 |         - |         - |         - |     1.39 KB |       0.001 |
| &#39;Add virtual column (column creation overhead)&#39;       |  71,259.376 μs | 50,913.4544 μs | 39,749.8762 μs |  54,574.955 μs |  6.017 |    3.22 | 1000.0000 | 1000.0000 | 1000.0000 |  3004.09 KB |       1.955 |
