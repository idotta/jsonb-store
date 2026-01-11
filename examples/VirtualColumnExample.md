# Virtual Column Example

This example demonstrates how to use virtual columns to dramatically improve query performance on frequently accessed JSON properties.

## What Are Virtual Columns?

Virtual columns are SQLite generated columns that extract and index JSON properties. When you add a virtual column, SQLite:

1. Creates a computed column that extracts the JSON value
2. Optionally creates an index on that column
3. Allows `QueryAsync<T>` to use the indexed column automatically

## Basic Virtual Column

Create a virtual column for a frequently queried property:

```csharp
using LiteDocumentStore;

// Create store
var options = DocumentStoreOptionsBuilder
    .CreateForFile("products.db")
    .Build();

var factory = new DocumentStoreFactory();
await using var store = await factory.CreateAsync(options);

// Create table and insert data
await store.CreateTableAsync<Product>();
await store.UpsertAsync("p1", new Product { Name = "Widget", Category = "Electronics", Sku = "SKU-001" });
await store.UpsertAsync("p2", new Product { Name = "Gadget", Category = "Electronics", Sku = "SKU-002" });
await store.UpsertAsync("p3", new Product { Name = "Tool", Category = "Hardware", Sku = "SKU-003" });

// Add virtual column with index
await store.AddVirtualColumnAsync<Product>(p => p.Category, "category", createIndex: true);

// Now QueryAsync automatically uses the indexed column!
var electronics = await store.QueryAsync<Product>(p => p.Category == "Electronics");
// Generates: SELECT json(data) FROM Product WHERE [category] = @p0
// Instead of: SELECT json(data) FROM Product WHERE json_extract(data, '$.Category') = @p0
```

## Performance Benefits

Virtual columns provide **dramatic speedups** for point queries:

| Query Type | Without Virtual Column | With Virtual Column | Speedup |
|------------|----------------------|---------------------|---------|
| SKU lookup (exact match) | 10,647 μs | 8 μs | **1,300x** |
| Category filter | 13,320 μs | 4,013 μs | **3.3x** |
| Nested property (Brand) | 13,621 μs | 2,053 μs | **6.6x** |

## Column Types

Specify the appropriate SQLite column type for better query performance:

```csharp
// TEXT (default) - for strings
await store.AddVirtualColumnAsync<Product>(p => p.Category, "category", createIndex: true);

// INTEGER - for whole numbers
await store.AddVirtualColumnAsync<Product>(p => p.Stock, "stock", createIndex: true, columnType: "INTEGER");

// REAL - for decimals/floats
await store.AddVirtualColumnAsync<Product>(p => p.Price, "price", createIndex: true, columnType: "REAL");
```

## Nested Property Virtual Columns

Create virtual columns for nested JSON properties:

```csharp
public class Product
{
    public string Name { get; set; }
    public ProductMetadata Metadata { get; set; }
}

public class ProductMetadata
{
    public string Brand { get; set; }
    public string Country { get; set; }
}

// Add virtual column on nested property
await store.AddVirtualColumnAsync<Product>(p => p.Metadata.Brand, "brand", createIndex: true);

// Query uses the virtual column automatically
var acmeProducts = await store.QueryAsync<Product>(p => p.Metadata.Brand == "Acme");
```

## Multiple Virtual Columns

Add multiple virtual columns for different query patterns:

```csharp
// Create virtual columns for common query fields
await store.AddVirtualColumnAsync<Product>(p => p.Category, "category", createIndex: true);
await store.AddVirtualColumnAsync<Product>(p => p.Sku, "sku", createIndex: true);
await store.AddVirtualColumnAsync<Product>(p => p.Metadata.Brand, "brand", createIndex: true);

// All these queries now use indexes
var electronics = await store.QueryAsync<Product>(p => p.Category == "Electronics");
var product = await store.QueryAsync<Product>(p => p.Sku == "SKU-12345");
var acme = await store.QueryAsync<Product>(p => p.Metadata.Brand == "Acme");

// Combined queries use available indexes
var acmeElectronics = await store.QueryAsync<Product>(
    p => p.Category == "Electronics" && p.Metadata.Brand == "Acme");
```

## Automatic Schema Discovery

Virtual columns are automatically discovered when a new `DocumentStore` connects:

```csharp
// First run - create virtual columns
{
    await using var store = await factory.CreateAsync(options);
    await store.AddVirtualColumnAsync<Product>(p => p.Sku, "sku", createIndex: true);
}

// Later run - virtual columns are discovered from schema
{
    await using var store = await factory.CreateAsync(options); // New connection
    
    // Query automatically uses the virtual column - no need to call AddVirtualColumnAsync again
    var product = await store.QueryAsync<Product>(p => p.Sku == "SKU-12345");
}
```

## When to Use Virtual Columns

### ✅ Recommended For

| Use Case | Why |
|----------|-----|
| **Unique identifiers** (SKU, Email, UserId) | Point lookups are extremely fast |
| **Category/Type fields** | Equality checks benefit greatly from indexing |
| **Foreign key-like fields** | Joining on indexed values is efficient |
| **Nested properties you query often** | Avoid repeated json_extract overhead |

### ⚠️ Use With Caution

| Use Case | Why |
|----------|-----|
| **Range queries returning many rows** (e.g., `Price > 50` returning 50%) | Full scan may be faster than index scan |
| **Boolean fields with ~50/50 distribution** | Low selectivity doesn't benefit from indexing |
| **Fields you rarely query** | Index maintenance overhead without benefit |

### Example: Range Query Consideration

```csharp
// This benefits from indexing (returns few rows)
await store.AddVirtualColumnAsync<Product>(p => p.Price, "price", createIndex: true, columnType: "REAL");
var premiumProducts = await store.QueryAsync<Product>(p => p.Price > 1000); // High selectivity

// This may NOT benefit (returns many rows)
var cheapProducts = await store.QueryAsync<Product>(p => p.Price > 10); // Returns 90% of data
// For low-selectivity queries, SQLite may perform a full scan which could be faster
```

## Idempotent Creation

Virtual column creation is idempotent - safe to call multiple times:

```csharp
// Safe to run on every application startup
await store.AddVirtualColumnAsync<Product>(p => p.Sku, "sku", createIndex: true);
await store.AddVirtualColumnAsync<Product>(p => p.Sku, "sku", createIndex: true); // No error
```

## Raw SQL Access

Virtual columns are also available for raw SQL queries:

```csharp
// Query using virtual column directly
var results = await store.Connection.QueryAsync<dynamic>(
    "SELECT id, sku, category FROM Product WHERE category = @Category ORDER BY sku",
    new { Category = "Electronics" });

// Combine with json_extract for other fields
var results = await store.Connection.QueryAsync<dynamic>(
    @"SELECT id, sku, json_extract(data, '$.Name') as Name 
      FROM Product 
      WHERE category = @Category",
    new { Category = "Electronics" });
```

## Complete Example

```csharp
using LiteDocumentStore;
using Microsoft.Extensions.DependencyInjection;

// Setup with DI
var services = new ServiceCollection();
services.AddLiteDocumentStore(options =>
{
    options.ConnectionString = "Data Source=products.db";
    options.EnableWalMode = true;
});

using var provider = services.BuildServiceProvider();
await using var store = provider.GetRequiredService<IDocumentStore>();

// Create table
await store.CreateTableAsync<Product>();

// Add virtual columns for frequently queried fields
await store.AddVirtualColumnAsync<Product>(p => p.Sku, "sku", createIndex: true);
await store.AddVirtualColumnAsync<Product>(p => p.Category, "category", createIndex: true);
await store.AddVirtualColumnAsync<Product>(p => p.Metadata.Brand, "brand", createIndex: true);

// Insert products
for (int i = 0; i < 50000; i++)
{
    await store.UpsertAsync($"prod-{i}", new Product
    {
        Name = $"Product {i}",
        Category = $"Category {i % 50}",
        Sku = $"SKU-{i:D6}",
        Price = 10.0m + (i % 100),
        Metadata = new ProductMetadata { Brand = $"Brand {i % 100}" }
    });
}

// Fast queries using virtual columns
var widget = await store.QueryAsync<Product>(p => p.Sku == "SKU-025000"); // ~8μs
var electronics = await store.QueryAsync<Product>(p => p.Category == "Category 25"); // ~4ms
var acme = await store.QueryAsync<Product>(p => p.Metadata.Brand == "Brand 42"); // ~2ms
```

## Model Definitions

```csharp
public class Product
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public ProductMetadata Metadata { get; set; } = new();
}

public class ProductMetadata
{
    public string Brand { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
```
