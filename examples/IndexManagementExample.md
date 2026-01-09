# Index Management Example

This example demonstrates how to use the index management features in LiteDocumentStore to optimize query performance.

## Basic Index Creation

Create an index on a single JSON property:

```csharp
// Create a document store
var options = new DocumentStoreOptions 
{ 
    ConnectionString = "Data Source=myapp.db" 
};
var factory = new DocumentStoreFactory();
using var store = await factory.CreateAsync(options);

// Create table
await store.CreateTableAsync<Customer>();

// Create an index on the Email property
await store.CreateIndexAsync<Customer>(c => c.Email);

// Or with a custom index name
await store.CreateIndexAsync<Customer>(c => c.Email, "idx_customer_email");
```

## Nested Property Indexing

Create indexes on nested JSON properties:

```csharp
public class Customer
{
    public string Name { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string City { get; set; }
    public string Country { get; set; }
}

// Create index on nested property
await store.CreateIndexAsync<Customer>(c => c.Address.City);
```

## Composite Index Creation

Create a composite index on multiple properties for multi-column queries:

```csharp
// Create composite index on Name and Age
await store.CreateCompositeIndexAsync<Customer>(
    new Expression<Func<Customer, object>>[] 
    { 
        c => c.Name, 
        c => c.Age 
    },
    "idx_customer_name_age"
);

// Or let the system auto-generate the index name
await store.CreateCompositeIndexAsync<Customer>(
    new Expression<Func<Customer, object>>[] 
    { 
        c => c.Address.City, 
        c => c.Address.Country 
    }
);
```

## Index Existence Checking

The index creation methods automatically check if an index already exists before creating it:

```csharp
// This is safe to call multiple times
await store.CreateIndexAsync<Customer>(c => c.Email, "idx_customer_email");
await store.CreateIndexAsync<Customer>(c => c.Email, "idx_customer_email"); // No error
```

## Performance Benefits

Indexes significantly improve query performance on JSON properties:

```csharp
// Without index: Full table scan
var customer = await store.Connection.QueryFirstOrDefaultAsync<string>(
    "SELECT json(data) FROM Customer WHERE json_extract(data, '$.email') = @Email",
    new { Email = "john@example.com" }
);

// With index: Uses index for fast lookup
await store.CreateIndexAsync<Customer>(c => c.Email);
var customer = await store.Connection.QueryFirstOrDefaultAsync<string>(
    "SELECT json(data) FROM Customer WHERE json_extract(data, '$.email') = @Email",
    new { Email = "john@example.com" }
);
```

## Best Practices

1. **Create indexes on frequently queried properties**: Focus on properties used in WHERE clauses
2. **Use composite indexes for multi-column queries**: Better than multiple single-column indexes
3. **Index early**: Create indexes right after table creation for best results
4. **Don't over-index**: Too many indexes can slow down writes

## Complete Example

```csharp
using LiteDocumentStore;
using Microsoft.Data.Sqlite;

// Setup
var options = new DocumentStoreOptions 
{ 
    ConnectionString = "Data Source=customers.db" 
};
var factory = new DocumentStoreFactory();
using var store = await factory.CreateAsync(options);

// Create table and indexes
await store.CreateTableAsync<Customer>();
await store.CreateIndexAsync<Customer>(c => c.Email);
await store.CreateIndexAsync<Customer>(c => c.LastName);
await store.CreateCompositeIndexAsync<Customer>(
    new Expression<Func<Customer, object>>[] 
    { 
        c => c.Address.City, 
        c => c.Address.Country 
    }
);

// Insert data
await store.UpsertAsync("customer1", new Customer 
{
    Email = "john@example.com",
    FirstName = "John",
    LastName = "Doe",
    Address = new Address 
    {
        City = "New York",
        Country = "USA"
    }
});

// Query using indexed properties (fast!)
var result = await store.Connection.QueryFirstOrDefaultAsync<string>(
    @"SELECT json(data) 
      FROM Customer 
      WHERE json_extract(data, '$.email') = @Email",
    new { Email = "john@example.com" }
);
```

## SQLite Index Internals

The index creation uses SQLite's `json_extract()` function:

```sql
-- Single property index
CREATE INDEX idx_customer_email 
ON Customer (json_extract(data, '$.email'));

-- Composite index
CREATE INDEX idx_customer_name_age 
ON Customer (
    json_extract(data, '$.name'), 
    json_extract(data, '$.age')
);
```

This allows SQLite to efficiently query JSON documents without deserializing the entire JSONB blob.
