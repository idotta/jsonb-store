```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.7462/24H2/2024Update/HudsonValley)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3
  Job-MNMNNY : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3

IterationCount=15  RunStrategy=Throughput  

```
| Method                            | index | id         | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0    | Gen1   | Allocated  | Alloc Ratio |
|---------------------------------- |------ |----------- |-------------:|------------:|------------:|------:|--------:|--------:|-------:|-----------:|------------:|
| **LiteDocumentStore_BulkInsert**      | **?**     | **?**          | **687,900.1 ns** | **4,446.65 ns** | **4,159.40 ns** |     **?** |       **?** | **17.5781** | **3.9063** |   **220.8 KB** |           **?** |
| RawDapper_BulkInsert              | ?     | ?          | 426,687.3 ns | 5,079.55 ns | 4,751.42 ns |     ? |       ? | 18.5547 |      - |  227.52 KB |           ? |
| LiteDB_BulkInsert                 | ?     | ?          | 382,744.3 ns | 5,362.48 ns | 5,016.06 ns |     ? |       ? | 89.8438 | 3.9063 | 1102.69 KB |           ? |
| LiteDocumentStore_FullScan        | ?     | ?          |   2,390.2 ns |    46.89 ns |    43.86 ns |     ? |       ? |  0.1259 |      - |    1.58 KB |           ? |
| RawDapper_FullScan                | ?     | ?          |   2,247.6 ns |    28.96 ns |    27.09 ns |     ? |       ? |  0.1144 |      - |    1.41 KB |           ? |
| LiteDB_FullScan                   | ?     | ?          |     699.5 ns |     8.35 ns |     7.81 ns |     ? |       ? |  0.1926 |      - |    2.37 KB |           ? |
| LiteDocumentStore_QueryByCategory | ?     | ?          |   4,218.3 ns |    60.44 ns |    56.53 ns |     ? |       ? |  0.3357 |      - |    4.13 KB |           ? |
| RawDapper_QueryByCategory         | ?     | ?          |   3,316.5 ns |    45.89 ns |    42.93 ns |     ? |       ? |  0.1640 |      - |    2.03 KB |           ? |
| LiteDB_QueryByCategory            | ?     | ?          |   3,129.3 ns |    20.01 ns |    16.71 ns |     ? |       ? |  0.6409 |      - |    7.95 KB |           ? |
|                                   |       |            |              |             |             |       |         |         |        |            |             |
| **LiteDocumentStore_SingleInsert**    | **0**     | **?**          |   **7,527.0 ns** |    **48.62 ns** |    **45.48 ns** |  **1.00** |    **0.01** |  **0.2213** |      **-** |    **2.78 KB** |        **1.00** |
| RawDapper_SingleInsert            | 0     | ?          |   4,794.5 ns |    18.24 ns |    17.06 ns |  0.64 |    0.00 |  0.1755 |      - |    2.24 KB |        0.81 |
| LiteDB_SingleInsert               | 0     | ?          |   6,698.2 ns |   116.82 ns |   109.27 ns |  0.89 |    0.01 |  0.8545 | 0.1526 |   10.48 KB |        3.77 |
|                                   |       |            |              |             |             |       |         |         |        |            |             |
| **LiteDocumentStore_SingleInsert**    | **50**    | **?**          |   **7,480.9 ns** |    **78.36 ns** |    **73.30 ns** |  **1.00** |    **0.01** |  **0.2213** |      **-** |    **2.79 KB** |        **1.00** |
| RawDapper_SingleInsert            | 50    | ?          |   4,757.2 ns |    28.48 ns |    26.64 ns |  0.64 |    0.01 |  0.1831 |      - |    2.26 KB |        0.81 |
| LiteDB_SingleInsert               | 50    | ?          |   7,042.2 ns |   419.72 ns |   372.08 ns |  0.94 |    0.05 |  0.8545 | 0.1526 |   10.54 KB |        3.78 |
|                                   |       |            |              |             |             |       |         |         |        |            |             |
| **LiteDocumentStore_QueryById**       | **?**     | **doc-000000** |   **2,579.0 ns** |    **49.60 ns** |    **43.97 ns** |     **?** |       **?** |  **0.1297** |      **-** |    **1.63 KB** |           **?** |
| RawDapper_QueryById               | ?     | doc-000000 |   2,439.4 ns |    34.85 ns |    32.60 ns |     ? |       ? |  0.1144 |      - |    1.41 KB |           ? |
| LiteDB_QueryById                  | ?     | doc-000000 |   2,292.3 ns |    17.64 ns |    15.63 ns |     ? |       ? |  0.4807 | 0.0038 |     5.9 KB |           ? |
|                                   |       |            |              |             |             |       |         |         |        |            |             |
| **LiteDocumentStore_QueryById**       | **?**     | **doc-000050** |   **2,519.7 ns** |    **43.96 ns** |    **41.12 ns** |     **?** |       **?** |  **0.1297** |      **-** |    **1.63 KB** |           **?** |
| RawDapper_QueryById               | ?     | doc-000050 |   2,413.6 ns |    34.78 ns |    32.53 ns |     ? |       ? |  0.1144 |      - |    1.41 KB |           ? |
| LiteDB_QueryById                  | ?     | doc-000050 |   2,338.1 ns |    18.35 ns |    17.16 ns |     ? |       ? |  0.4807 | 0.0038 |     5.9 KB |           ? |
|                                   |       |            |              |             |             |       |         |         |        |            |             |
| **LiteDocumentStore_Delete**          | **?**     | **doc-000099** |   **1,889.2 ns** |    **19.06 ns** |    **17.83 ns** |     **?** |       **?** |  **0.0973** |      **-** |    **1.21 KB** |           **?** |
| RawDapper_Delete                  | ?     | doc-000099 |   1,768.5 ns |     9.77 ns |     9.14 ns |     ? |       ? |  0.0839 |      - |    1.03 KB |           ? |
| LiteDB_Delete                     | ?     | doc-000099 |     487.4 ns |     3.14 ns |     2.78 ns |     ? |       ? |  0.2041 | 0.0010 |    2.51 KB |           ? |
