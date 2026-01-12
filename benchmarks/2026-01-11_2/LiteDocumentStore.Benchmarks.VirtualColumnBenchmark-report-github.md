```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.7462/24H2/2024Update/HudsonValley)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3
  Job-MNMNNY : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3

IterationCount=15  RunStrategy=Throughput  

```
| Method                                                | Mean           | Error         | StdDev        | Ratio  | RatioSD | Gen0      | Gen1      | Gen2      | Allocated   | Alloc Ratio |
|------------------------------------------------------ |---------------:|--------------:|--------------:|-------:|--------:|----------:|----------:|----------:|------------:|------------:|
| &#39;Query by category WITHOUT virtual column&#39;            |  11,891.610 μs |   113.2174 μs |    94.5416 μs |  1.000 |    0.01 |  125.0000 |   93.7500 |         - |  1536.58 KB |       1.000 |
| &#39;Query by category WITH virtual column and index&#39;     |   3,798.686 μs |    67.2992 μs |    52.5428 μs |  0.319 |    0.00 |  125.0000 |   93.7500 |         - |  1536.39 KB |       1.000 |
| &#39;Query by price WITHOUT virtual column&#39;               |  10,135.764 μs |    52.8025 μs |    44.0925 μs |  0.852 |    0.01 |         - |         - |         - |     3.95 KB |       0.003 |
| &#39;Query by price WITH virtual column and index&#39;        | 146,248.902 μs | 3,178.2773 μs | 2,972.9626 μs | 12.299 |    0.26 | 4333.3333 | 3333.3333 | 1000.0000 | 45429.96 KB |      29.566 |
| &#39;Query by SKU WITHOUT virtual column&#39;                 |  10,541.055 μs |   327.1313 μs |   305.9988 μs |  0.886 |    0.03 |         - |         - |         - |     5.31 KB |       0.003 |
| &#39;Query by SKU WITH virtual column and index&#39;          |       8.255 μs |     0.0749 μs |     0.0700 μs |  0.001 |    0.00 |    0.4120 |         - |         - |     5.13 KB |       0.003 |
| &#39;Query nested property WITHOUT virtual column&#39;        |  13,749.342 μs |   264.7320 μs |   247.6305 μs |  1.156 |    0.02 |   62.5000 |   31.2500 |         - |   765.73 KB |       0.498 |
| &#39;Query nested property WITH virtual column and index&#39; |   1,946.694 μs |    84.5957 μs |    74.9918 μs |  0.164 |    0.01 |   58.5938 |   15.6250 |         - |   765.51 KB |       0.498 |
| &#39;Raw SQL: Category query (indexed)&#39;                   |   2,502.182 μs |    34.6057 μs |    32.3702 μs |  0.210 |    0.00 |   15.6250 |    3.9063 |         - |   212.68 KB |       0.138 |
| &#39;Raw SQL: Category query (no index)&#39;                  |  10,542.902 μs |   237.8290 μs |   198.5980 μs |  0.887 |    0.02 |   15.6250 |         - |         - |    212.7 KB |       0.138 |
| &#39;Raw SQL: Price query (indexed)&#39;                      |  76,107.038 μs | 1,523.1085 μs | 1,424.7167 μs |  6.400 |    0.13 |  571.4286 |  428.5714 |  142.8571 |  6425.51 KB |       4.182 |
| &#39;Raw SQL: Price query (no index)&#39;                     |  19,386.379 μs |   382.9179 μs |   358.1816 μs |  1.630 |    0.03 |  500.0000 |  468.7500 |  218.7500 |  6425.14 KB |       4.181 |
| &#39;Raw SQL: SKU query (indexed)&#39;                        |       5.060 μs |     0.1089 μs |     0.1019 μs |  0.000 |    0.00 |    0.1068 |         - |         - |     1.37 KB |       0.001 |
| &#39;Raw SQL: SKU query (no index)&#39;                       |  10,297.303 μs |    66.0207 μs |    51.5446 μs |  0.866 |    0.01 |         - |         - |         - |     1.39 KB |       0.001 |
| &#39;Add virtual column (column creation overhead)&#39;       |  47,832.566 μs |   915.3973 μs |   714.6820 μs |  4.023 |    0.07 | 1000.0000 | 1000.0000 | 1000.0000 |  3004.09 KB |       1.955 |
