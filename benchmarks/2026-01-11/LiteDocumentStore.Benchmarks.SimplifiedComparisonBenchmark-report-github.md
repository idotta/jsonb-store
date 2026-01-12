```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.7462/24H2/2024Update/HudsonValley)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3
  Job-INMAZI : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3

IterationCount=5  RunStrategy=Throughput  WarmupCount=3  

```
| Method                            | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------------------- |-----------:|-----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| LiteDocumentStore_SingleInsert    |   5.201 μs |  0.1807 μs | 0.0280 μs |  1.00 |    0.01 | 0.1984 |      - |   2.45 KB |        1.00 |
| RawDapper_SingleInsert            |   4.917 μs |  0.0835 μs | 0.0129 μs |  0.95 |    0.01 | 0.1755 |      - |   2.24 KB |        0.92 |
| LiteDocumentStore_BulkInsert      | 213.521 μs |  4.6604 μs | 1.2103 μs | 41.06 |    0.29 | 8.0566 | 0.9766 |  99.79 KB |       40.81 |
| RawDapper_BulkInsert              | 218.645 μs | 27.3825 μs | 7.1111 μs | 42.04 |    1.27 | 9.2773 |      - | 114.36 KB |       46.77 |
| LiteDocumentStore_QueryById       |   2.762 μs |  0.2620 μs | 0.0680 μs |  0.53 |    0.01 | 0.1297 |      - |   1.59 KB |        0.65 |
| RawDapper_QueryById               |   2.514 μs |  0.0365 μs | 0.0095 μs |  0.48 |    0.00 | 0.1144 |      - |   1.41 KB |        0.58 |
| LiteDocumentStore_FullScan        |   2.426 μs |  0.3003 μs | 0.0780 μs |  0.47 |    0.01 | 0.1183 |      - |   1.48 KB |        0.61 |
| RawDapper_FullScan                |   2.245 μs |  0.0310 μs | 0.0081 μs |  0.43 |    0.00 | 0.1144 |      - |   1.41 KB |        0.58 |
| LiteDocumentStore_QueryByCategory |   4.104 μs |  0.1166 μs | 0.0303 μs |  0.79 |    0.01 | 0.3052 |      - |   3.84 KB |        1.57 |
| RawDapper_QueryByCategory         |   3.436 μs |  0.4574 μs | 0.0708 μs |  0.66 |    0.01 | 0.1640 |      - |   2.03 KB |        0.83 |
| LiteDocumentStore_Delete          |   1.920 μs |  0.0763 μs | 0.0118 μs |  0.37 |    0.00 | 0.0954 |      - |   1.17 KB |        0.48 |
| RawDapper_Delete                  |   1.822 μs |  0.0400 μs | 0.0104 μs |  0.35 |    0.00 | 0.0839 |      - |   1.03 KB |        0.42 |
| LiteDocumentStore_BulkDelete      |   9.402 μs |  1.5436 μs | 0.2389 μs |  1.81 |    0.04 | 0.6104 |      - |   7.59 KB |        3.10 |
| RawDapper_BulkDelete              |   8.158 μs |  0.2743 μs | 0.0712 μs |  1.57 |    0.01 | 0.5798 | 0.1068 |   7.15 KB |        2.92 |
| LiteDocumentStore_Update          |   5.149 μs |  0.0857 μs | 0.0223 μs |  0.99 |    0.01 | 0.1984 |      - |   2.45 KB |        1.00 |
| RawDapper_Update                  |   4.789 μs |  0.0220 μs | 0.0034 μs |  0.92 |    0.00 | 0.1831 |      - |   2.26 KB |        0.92 |
