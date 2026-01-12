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
| &#39;Full document retrieval (baseline)&#39;          | 5,281.0 μs | 109.61 μs | 102.53 μs |  1.00 |    0.03 | 648.4375 | 640.6250 | 7946.77 KB |       1.000 |
| &#39;Projection query - 2 fields&#39;                 |   547.1 μs |   2.57 μs |   2.40 μs |  0.10 |    0.00 |  10.7422 |   2.9297 |  137.38 KB |       0.017 |
| &#39;Projection query - 4 fields&#39;                 | 1,019.0 μs |   8.17 μs |   7.64 μs |  0.19 |    0.00 |  21.4844 |   9.7656 |  264.49 KB |       0.033 |
| &#39;Projection query - nested field&#39;             |   885.8 μs |   3.64 μs |   3.23 μs |  0.17 |    0.00 |  15.6250 |   3.9063 |  201.64 KB |       0.025 |
| &#39;Projection query with filter - 2 fields&#39;     |   212.7 μs |   1.20 μs |   1.00 μs |  0.04 |    0.00 |   1.4648 |        - |   20.27 KB |       0.003 |
| &#39;Full documents with filter (for comparison)&#39; |   656.6 μs |   9.29 μs |   8.69 μs |  0.12 |    0.00 |  64.4531 |  21.4844 |  798.25 KB |       0.100 |
