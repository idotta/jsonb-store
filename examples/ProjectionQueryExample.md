# Projection Query Example

This example demonstrates how to use projection queries to select specific fields from JSON documents for improved performance.

## Basic Projection

Select only specific fields instead of retrieving entire documents:

```csharp
using LiteDocumentStore;

// Create store
var options = DocumentStoreOptionsBuilder
    .CreateForFile("customers.db")
    .Build();

var factory = new DocumentStoreFactory();
await using var store = await factory.CreateAsync(options);

// Create table and insert sample data
await store.CreateTableAsync<Customer>();
await store.UpsertAsync("1", new Customer 
{ 
    Id = "1", 
    Name = "John Doe", 
    Email = "john@example.com",
    Phone = "555-1234",
    Address = new Address 
    {
        Street = "123 Main St",
        City = "New York",
        State = "NY",
        ZipCode = "10001"
    },
    Orders = new List<Order>() // Lots of nested data
});

// Traditional approach - retrieves entire document
var fullCustomer = await store.GetAsync<Customer>("1");
// This deserializes ALL fields: Name, Email, Phone, Address, Orders, etc.

// Projection approach - only retrieves needed fields
var projection = await store.SelectAsync<Customer, CustomerSummary>(
    c => new CustomerSummary { Name = c.Name, Email = c.Email });

// SQL generated: SELECT json_extract(data, '$.Name') as Name, 
//                       json_extract(data, '$.Email') as Email 
//                FROM Customer
```

## Filtered Projection

Combine filtering and projection for maximum efficiency:

```csharp
// Get name and email of active customers only
var activeCustomers = await store.SelectAsync<Customer, CustomerSummary>(
    c => c.Active == true,
    c => new CustomerSummary { Name = c.Name, Email = c.Email });

// SQL generated: SELECT json_extract(data, '$.Name') as Name,
//                       json_extract(data, '$.Email') as Email
//                FROM Customer
//                WHERE json_extract(data, '$.Active') = @p0
```

## Anonymous Type Projection

Use anonymous types for quick projections:

```csharp
var results = await store.SelectAsync<Customer, dynamic>(
    c => new { c.Name, c.Email, c.Phone });

foreach (var result in results)
{
    Console.WriteLine($"{result.Name}: {result.Email}, {result.Phone}");
}
```

## Nested Field Projection

Select nested fields from complex documents:

```csharp
// Select customer name and their city
var customerLocations = await store.SelectAsync<Customer, CustomerLocation>(
    c => new CustomerLocation 
    { 
        Name = c.Name, 
        City = c.Address.City,
        State = c.Address.State 
    });

// SQL generated: SELECT json_extract(data, '$.Name') as Name,
//                       json_extract(data, '$.Address.City') as City,
//                       json_extract(data, '$.Address.State') as State
//                FROM Customer
```

## Performance Benefits

Projection queries provide exceptional performance improvements:

1. **Reduced Data Transfer**: Only selected fields are retrieved from SQLite
2. **Less Deserialization**: Smaller JSON payloads to deserialize
3. **Lower Memory Usage**: Projection types are typically smaller than full documents
4. **Index Utilization**: Can still use indexes on filtered fields

### Benchmark Results (1,000 documents)

**Validated with BenchmarkDotNet** (see `tests/LiteDocumentStore.Benchmarks`):

| Scenario | Speed Improvement | Memory Reduction | Baseline → Result |
|----------|------------------|------------------|-------------------|
| 2-Field Projection | **90% faster** | **98% less memory** | 5.5ms → 0.6ms, 7.9MB → 137KB |
| 4-Field Projection | **81% faster** | **97% less memory** | 5.5ms → 1.1ms, 7.9MB → 264KB |
| Nested Fields | **83% faster** | **98% less memory** | 5.5ms → 0.9ms, 7.9MB → 202KB |
| With Filter | **96% faster** | **99% less memory** | 5.5ms → 0.2ms, 7.9MB → 20KB |

### Example Code

```csharp
// Full document retrieval
var sw = Stopwatch.StartNew();
var fullDocs = await store.GetAllAsync<LargeDocument>();
Console.WriteLine($"Full documents: {sw.ElapsedMilliseconds}ms, {fullDocs.Sum(d => d.EstimatedSize())} bytes");

// Projection query
sw.Restart();
var projections = await store.SelectAsync<LargeDocument, SmallProjection>(
    d => new SmallProjection { Id = d.Id, Name = d.Name });
Console.WriteLine($"Projections: {sw.ElapsedMilliseconds}ms, {projections.Sum(p => p.EstimatedSize())} bytes");

// Real results: 90% faster, 98% less memory! 🚀
```

## Use Cases

### 1. List Views

Display lists with minimal information:

```csharp
// Show customer list with just name and email
var customerList = await store.SelectAsync<Customer, CustomerListItem>(
    c => new CustomerListItem { Id = c.Id, Name = c.Name, Email = c.Email });
```

### 2. Export/Reporting

Generate reports with specific columns:

```csharp
// Export customer data for marketing
var marketingData = await store.SelectAsync<Customer, MarketingExport>(
    c => c.MarketingOptIn == true,
    c => new MarketingExport 
    { 
        Email = c.Email, 
        Name = c.Name, 
        LastPurchaseDate = c.LastOrderDate 
    });

// Write to CSV
using var writer = new StreamWriter("marketing.csv");
foreach (var data in marketingData)
{
    writer.WriteLine($"{data.Name},{data.Email},{data.LastPurchaseDate}");
}
```

### 3. API Responses

Return only necessary data to clients:

```csharp
// API endpoint returns summary data
[HttpGet("api/customers")]
public async Task<IActionResult> GetCustomers()
{
    var summaries = await _store.SelectAsync<Customer, CustomerApiResponse>(
        c => new CustomerApiResponse 
        { 
            Id = c.Id,
            Name = c.Name,
            Email = c.Email,
            TotalOrders = c.OrderCount 
        });
    
    return Ok(summaries);
}
```

## Best Practices

1. **Index Filtered Fields**: Create indexes on fields used in predicates
   ```csharp
   await store.CreateIndexAsync<Customer>(c => c.Active);
   ```

2. **Reuse Projection Types**: Define common projection DTOs
   ```csharp
   public class CustomerSummary 
   { 
       public string Name { get; set; }
       public string Email { get; set; }
   }
   ```

3. **Combine with Raw SQL**: For complex aggregations, use raw SQL with projections
   ```csharp
   var stats = await store.Connection.QueryAsync<CustomerStats>(
       @"SELECT json_extract(data, '$.Name') as Name,
                COUNT(*) as OrderCount
         FROM Customer
         GROUP BY Name");
   ```

## Model Classes

```csharp
public class Customer
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public Address Address { get; set; }
    public bool Active { get; set; }
    public bool MarketingOptIn { get; set; }
    public DateTime LastOrderDate { get; set; }
    public int OrderCount { get; set; }
    public List<Order> Orders { get; set; } = new();
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
}

public class Order
{
    public string Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
    // ... many more fields
}

// Projection DTOs
public class CustomerSummary
{
    public string Name { get; set; }
    public string Email { get; set; }
}

public class CustomerLocation
{
    public string Name { get; set; }
    public string City { get; set; }
    public string State { get; set; }
}

public class CustomerListItem
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

public class MarketingExport
{
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime LastPurchaseDate { get; set; }
}

public class CustomerApiResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public int TotalOrders { get; set; }
}
```
