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
| **LiteDocumentStore_BulkInsert**      | **?**     | **?**          | **686,321.8 ns** | **4,551.78 ns** | **4,035.03 ns** |     **?** |       **?** | **16.6016** | **2.9297** |  **209.78 KB** |           **?** |
| RawDapper_BulkInsert              | ?     | ?          | 419,683.4 ns | 2,960.65 ns | 2,769.39 ns |     ? |       ? | 18.5547 |      - |   227.5 KB |           ? |
| LiteDB_BulkInsert                 | ?     | ?          | 391,443.4 ns | 5,071.03 ns | 4,495.34 ns |     ? |       ? | 89.8438 | 3.9063 | 1102.69 KB |           ? |
| LiteDocumentStore_FullScan        | ?     | ?          |   2,388.9 ns |    23.58 ns |    22.06 ns |     ? |       ? |  0.1259 |      - |    1.58 KB |           ? |
| RawDapper_FullScan                | ?     | ?          |   2,247.5 ns |    31.11 ns |    29.10 ns |     ? |       ? |  0.1144 |      - |    1.41 KB |           ? |
| LiteDB_FullScan                   | ?     | ?          |     670.7 ns |     8.81 ns |     7.81 ns |     ? |       ? |  0.1926 |      - |    2.37 KB |           ? |
| LiteDocumentStore_QueryByCategory | ?     | ?          |   4,172.4 ns |    88.06 ns |    82.37 ns |     ? |       ? |  0.3204 |      - |    3.95 KB |           ? |
| RawDapper_QueryByCategory         | ?     | ?          |   3,229.7 ns |    34.58 ns |    32.34 ns |     ? |       ? |  0.1640 |      - |    2.03 KB |           ? |
| LiteDB_QueryByCategory            | ?     | ?          |   3,275.6 ns |    87.99 ns |    82.31 ns |     ? |       ? |  0.6409 |      - |    7.95 KB |           ? |
|                                   |       |            |              |             |             |       |         |         |        |            |             |
| **LiteDocumentStore_SingleInsert**    | **0**     | **?**          |   **7,459.4 ns** |    **50.09 ns** |    **41.83 ns** |  **1.00** |    **0.01** |  **0.2213** |      **-** |    **2.78 KB** |        **1.00** |
| RawDapper_SingleInsert            | 0     | ?          |   4,784.1 ns |    37.26 ns |    34.86 ns |  0.64 |    0.01 |  0.1755 |      - |    2.24 KB |        0.81 |
| LiteDB_SingleInsert               | 0     | ?          |   6,728.8 ns |    89.57 ns |    83.78 ns |  0.90 |    0.01 |  0.8545 | 0.1526 |   10.48 KB |        3.77 |
|                                   |       |            |              |             |             |       |         |         |        |            |             |
| **LiteDocumentStore_SingleInsert**    | **50**    | **?**          |   **7,527.6 ns** |    **44.81 ns** |    **39.73 ns** |  **1.00** |    **0.01** |  **0.2136** |      **-** |    **2.79 KB** |        **1.00** |
| RawDapper_SingleInsert            | 50    | ?          |   4,830.5 ns |    44.14 ns |    39.13 ns |  0.64 |    0.01 |  0.1831 |      - |    2.26 KB |        0.81 |
| LiteDB_SingleInsert               | 50    | ?          |   6,752.9 ns |    58.36 ns |    48.73 ns |  0.90 |    0.01 |  0.8545 | 0.1526 |   10.54 KB |        3.78 |
|                                   |       |            |              |             |             |       |         |         |        |            |             |
| **LiteDocumentStore_QueryById**       | **?**     | **doc-000000** |   **2,545.9 ns** |    **31.70 ns** |    **29.65 ns** |     **?** |       **?** |  **0.1297** |      **-** |    **1.63 KB** |           **?** |
| RawDapper_QueryById               | ?     | doc-000000 |   2,499.2 ns |    31.19 ns |    27.65 ns |     ? |       ? |  0.1144 |      - |    1.41 KB |           ? |
| LiteDB_QueryById                  | ?     | doc-000000 |   2,409.0 ns |    26.12 ns |    20.39 ns |     ? |       ? |  0.4921 | 0.0038 |    6.06 KB |           ? |
|                                   |       |            |              |             |             |       |         |         |        |            |             |
| **LiteDocumentStore_QueryById**       | **?**     | **doc-000050** |   **2,569.0 ns** |    **51.31 ns** |    **47.99 ns** |     **?** |       **?** |  **0.1297** |      **-** |    **1.63 KB** |           **?** |
| RawDapper_QueryById               | ?     | doc-000050 |   2,439.4 ns |    36.86 ns |    32.68 ns |     ? |       ? |  0.1144 |      - |    1.41 KB |           ? |
| LiteDB_QueryById                  | ?     | doc-000050 |   2,367.8 ns |    31.54 ns |    27.96 ns |     ? |       ? |  0.4807 | 0.0038 |     5.9 KB |           ? |
|                                   |       |            |              |             |             |       |         |         |        |            |             |
| **LiteDocumentStore_Delete**          | **?**     | **doc-000099** |   **1,924.7 ns** |    **31.88 ns** |    **29.82 ns** |     **?** |       **?** |  **0.0954** |      **-** |    **1.21 KB** |           **?** |
| RawDapper_Delete                  | ?     | doc-000099 |   1,774.4 ns |     8.32 ns |     7.78 ns |     ? |       ? |  0.0839 |      - |    1.03 KB |           ? |
| LiteDB_Delete                     | ?     | doc-000099 |     498.8 ns |     3.40 ns |     3.01 ns |     ? |       ? |  0.2041 | 0.0010 |    2.51 KB |           ? |
