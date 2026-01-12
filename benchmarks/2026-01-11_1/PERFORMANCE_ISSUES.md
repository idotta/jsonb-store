# Performance Issues - January 11, 2026

Based on benchmark analysis and code review of LiteDocumentStore.

## Critical Issues (P0)

### 1. ❌ Expression.Compile() on Every Query
**File:** `src/LiteDocumentStore/Core/ExpressionToJsonPath.cs` (Lines 310-314)

**Problem:**
```csharp
var lambda = Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object)));
var compiled = lambda.Compile();
return compiled();
```

`lambda.Compile()` is extremely expensive (~1-10ms per call) and allocates heavily. This happens on every query for captured variables that aren't simple constants or fields.

**Impact:** ~10ms overhead per query with captured variables

**Solution:** 
- Use reflection to get values directly instead of compiling
- Or use `lambda.Compile(preferInterpretation: true)` for faster interpretation
- Or cache compiled delegates by expression structure

---

### 2. ❌ No ArrayPool for Serialization
**File:** `src/LiteDocumentStore/Serialization/JsonHelper.cs` (Line 37)

**Problem:**
```csharp
return JsonSerializer.SerializeToUtf8Bytes(value, Options);
```

Every serialization allocates a new `byte[]` that goes to GC. For bulk operations (100 docs), this is 100 allocations.

**Impact:** Heavy GC pressure in bulk operations (220KB allocated for 100 docs)

**Solution:** Use `ArrayBufferWriter<byte>` with `ArrayPool<byte>.Shared`:
```csharp
var buffer = new ArrayBufferWriter<byte>(256);
JsonSerializer.Serialize(new Utf8JsonWriter(buffer), value, Options);
return buffer.WrittenSpan.ToArray(); // or return the pooled buffer
```

---

## High Priority Issues (P1)

### 3. ❌ No SQL Caching in SqlGenerator
**File:** `src/LiteDocumentStore/Core/SqlGenerator.cs`

**Problem:** Every call to `GenerateUpsertSql`, `GenerateGetByIdSql`, etc. allocates a new string. These are hot paths called on every operation.

**Impact:** Redundant string allocations on every DB operation

**Solution:** Cache generated SQL per table name:
```csharp
private static readonly ConcurrentDictionary<string, string> _upsertSqlCache = new();

public static string GenerateUpsertSql(string tableName)
{
    return _upsertSqlCache.GetOrAdd(tableName, t => $@"INSERT INTO [{t}] ...");
}
```

---

### 4. ❌ LINQ Chains in Query Results
**File:** `src/LiteDocumentStore/Core/DocumentStore.cs` (Lines 367-371)

**Problem:**
```csharp
var documents = jsonResults
    .Select(json => JsonHelper.Deserialize<T>(json))
    .Where(doc => doc != null)
    .Select(doc => doc!)
    .ToList();
```

Two `.Select()` calls = two delegate allocations and two enumerator allocations.

**Impact:** Extra allocations per query

**Solution:** Single pass with pre-sized list:
```csharp
var results = new List<T>();
foreach (var json in jsonResults)
{
    if (JsonHelper.Deserialize<T>(json) is { } item)
        results.Add(item);
}
```

---

### 5. ❌ Bulk SQL Generation Uses List + Join
**File:** `src/LiteDocumentStore/Core/SqlGenerator.cs` (Lines 108-112)

**Problem:**
```csharp
var valuesClauses = new List<string>(count);
for (int i = 0; i < count; i++)
{
    valuesClauses.Add($"(@Id{i}, jsonb(@Data{i}), strftime('%s', 'now'))");
}
return $"... VALUES {string.Join(", ", valuesClauses)} ...";
```

Creates `count` strings + 1 list + final `string.Join`. For 100 items = 101 string allocations.

**Impact:** O(n) string allocations for bulk operations

**Solution:** Use `StringBuilder`:
```csharp
var sb = new StringBuilder(count * 60);
for (int i = 0; i < count; i++)
{
    if (i > 0) sb.Append(", ");
    sb.Append("(@Id").Append(i).Append(", jsonb(@Data").Append(i).Append("), strftime('%s', 'now'))");
}
```

---

## Medium Priority Issues (P2)

### 6. ⚠️ No List Capacity in GetAllAsync
**File:** `src/LiteDocumentStore/Core/DocumentStore.cs` (Line 211)

**Problem:**
```csharp
var results = new List<T>();  // No capacity hint
```

For large collections, causes multiple internal array resizes.

**Solution:** Pre-size when possible or use a reasonable default:
```csharp
var jsonList = jsonResults.ToList();
var results = new List<T>(jsonList.Count);
```

---

### 7. ⚠️ LINQ in VirtualColumnCache Schema Loading
**File:** `src/LiteDocumentStore/Core/VirtualColumnCache.cs` (Lines 99-100)

**Problem:**
```csharp
var hiddenColumns = columns.Where(c => c.IsHidden).ToList();
var tableInfo = tables.FirstOrDefault(t => string.Equals(...));
```

Multiple LINQ allocations on cache miss.

**Impact:** Minor - only runs once per table on first query

---

### 8. ⚠️ UpsertManyAsync Double Enumeration
**File:** `src/LiteDocumentStore/Core/DocumentStore.cs` (Line 130)

**Problem:**
```csharp
var itemsList = items.ToList();  // Materializes entire collection
```

Then iterates again in loop, creating byte[] for each item.

**Impact:** Extra memory for list + all serialized bytes held simultaneously

---

## Benchmark Summary

| Benchmark | LiteDocumentStore | Raw Dapper | Gap |
|-----------|-------------------|------------|-----|
| Single Insert | 7,527 ns | 4,795 ns | 57% slower |
| Bulk Insert (100) | 687,900 ns | 426,687 ns | 61% slower |
| Query by ID | 2,579 ns | 2,439 ns | 6% slower |
| Query by Category | 4,218 ns | 3,316 ns | 27% slower |

**Target:** Close the gap to <20% for all operations.

---

## Progress Tracker

- [x] Fix Expression.Compile() issue ✅ (replaced with reflection-based evaluation)
- [ ] Add ArrayPool to JsonHelper
- [ ] Add SQL caching to SqlGenerator
- [x] Optimize LINQ chains in DocumentStore ✅ (single-pass loop with pre-sized list)
- [x] Use StringBuilder in bulk SQL generation ✅ (replaced List+Join pattern)
- [ ] Add list capacity hints
- [ ] Optimize VirtualColumnCache LINQ
- [ ] Review UpsertManyAsync memory pattern
