```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.7462/24H2/2024Update/HudsonValley)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3
  Job-MNMNNY : .NET 10.0.1 (10.0.1, 10.0.125.57005), X64 RyuJIT x86-64-v3

IterationCount=15  RunStrategy=Throughput  

```
| Method                                        | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|---------------------------------------------- |-----------:|----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| &#39;Full document retrieval (baseline)&#39;          | 5,495.9 μs | 134.70 μs | 126.00 μs |  1.00 |    0.03 | 648.4375 | 640.6250 | 7946.65 KB |       1.000 |
| &#39;Projection query - 2 fields&#39;                 |   555.1 μs |   3.95 μs |   3.50 μs |  0.10 |    0.00 |  10.7422 |   2.9297 |  137.38 KB |       0.017 |
| &#39;Projection query - 4 fields&#39;                 | 1,057.9 μs |   7.40 μs |   6.93 μs |  0.19 |    0.00 |  21.4844 |   9.7656 |  264.49 KB |       0.033 |
| &#39;Projection query - nested field&#39;             |   914.1 μs |   3.73 μs |   3.49 μs |  0.17 |    0.00 |  15.6250 |   3.9063 |  201.64 KB |       0.025 |
| &#39;Projection query with filter - 2 fields&#39;     |   218.7 μs |   1.52 μs |   1.35 μs |  0.04 |    0.00 |   1.4648 |        - |   20.18 KB |       0.003 |
| &#39;Full documents with filter (for comparison)&#39; |   676.3 μs |   4.46 μs |   4.18 μs |  0.12 |    0.00 |  64.4531 |  42.9688 |  798.25 KB |       0.100 |
