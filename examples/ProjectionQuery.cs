#!/usr/bin/env dotnet run
// Projection Query Example - Select only needed fields for better performance
//
// Run this example with: dotnet run ProjectionQuery.cs
//
// Projection queries extract specific JSON fields instead of deserializing entire documents.
// This reduces memory usage and improves performance, especially with large or nested objects.

#:package Dapper@2.1.66
#:package Microsoft.Extensions.DependencyInjection@10.0.1
#:package Microsoft.Extensions.Logging@10.0.1
#:package Microsoft.Extensions.Logging.Console@10.0.1

#:project ../src/LiteDocumentStore/LiteDocumentStore.csproj

#:property PublishAot=false

using Dapper;
using System.Diagnostics;
using LiteDocumentStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Enable reflection-based JSON serialization for .NET 10+
AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

// Create store
logger.LogInformation("Creating customer database...");
var options = new DocumentStoreOptionsBuilder()
    .UseInMemory()
    .WithWalMode(false)
    .Build();

services.AddLiteDocumentStore(options);
serviceProvider = services.BuildServiceProvider();
var store = serviceProvider.GetRequiredService<IDocumentStore>();

// Create table and seed data
await store.CreateTableAsync<Customer>();

logger.LogInformation("Seeding customers with rich nested data...");
await store.ExecuteInTransactionAsync(async () =>
{
    for (int i = 1; i <= 1000; i++)
    {
        await store.UpsertAsync(
            $"c{i}",
            new Customer(
                $"c{i}",
                $"Customer {i}",
                $"customer{i}@example.com",
                25 + (i % 50),
                new Address(
                    $"{i} Main Street",
                    i % 10 == 0 ? "New York" : i % 10 == 1 ? "Los Angeles" : "Chicago",
                    "NY",
                    $"{10000 + i}",
                    "USA"
                ),
                new ContactInfo(
                    $"555-{i:D4}",
                    $"555-{i + 1000:D4}",
                    $"555-{i + 2000:D4}",
                    $"555-{i + 3000:D4}"
                ),
                new Preferences(
                    EmailNotifications: i % 2 == 0,
                    SmsNotifications: i % 3 == 0,
                    Language: "en-US",
                    Timezone: "America/New_York"
                ),
                new OrderHistory(
                    OrderIds: Enumerable.Range(1, i % 20).Select(o => $"order-{i}-{o}").ToList(),
                    TotalSpent: 100m * (i % 10),
                    LastOrderDate: DateTime.UtcNow.AddDays(-i % 365)
                ),
                Metadata: new Dictionary<string, string>
                {
                    ["source"] = "web",
                    ["campaign"] = $"campaign-{i % 5}",
                    ["tags"] = string.Join(",", Enumerable.Range(1, 5).Select(t => $"tag{t}"))
                }
            )
        );
    }
});

Console.WriteLine($"Inserted {await store.CountAsync<Customer>()} customers");

// Benchmark 1: GetAllAsync - retrieves ENTIRE documents
logger.LogInformation("\nBenchmark 1: GetAllAsync (full deserialization)...");
var sw = Stopwatch.StartNew();
var allCustomers = (await store.GetAllAsync<Customer>()).ToList();
sw.Stop();
Console.WriteLine($"Retrieved {allCustomers.Count} full customers in {sw.ElapsedMilliseconds}ms");
Console.WriteLine($"Memory: Each customer includes Address, Contact, Preferences, OrderHistory, Metadata");

// Benchmark 2: SelectAsync - only Name and Email
logger.LogInformation("\nBenchmark 2: SelectAsync for CustomerSummary (Name, Email only)...");
sw.Restart();
var summaries = await store.SelectAsync<Customer, CustomerSummary>(
    c => new CustomerSummary { Name = c.Name, Email = c.Email }
);
sw.Stop();
var summariesList = summaries.ToList();
Console.WriteLine($"Retrieved {summariesList.Count} summaries in {sw.ElapsedMilliseconds}ms");
Console.WriteLine($"Sample: {summariesList[0].Name} ({summariesList[0].Email})");

// Benchmark 3: SelectAsync with nested properties
logger.LogInformation("\nBenchmark 3: SelectAsync with nested properties (Name, City, State)...");
sw.Restart();
var locations = (await store.SelectAsync<Customer, CustomerLocation>(
    c => new CustomerLocation { Name = c.Name, City = c.Address.City, State = c.Address.State }
)).ToList();
sw.Stop();
Console.WriteLine($"Retrieved {locations.Count} locations in {sw.ElapsedMilliseconds}ms");
var nyCustomers = locations.Count(l => l.City == "New York");
Console.WriteLine($"Customers in New York: {nyCustomers}");

// Benchmark 4: SelectAsync with predicate (filtered projection)
logger.LogInformation("\nBenchmark 4: SelectAsync with predicate (New York customers only)...");
sw.Restart();
var nyLocations = (await store.SelectAsync<Customer, CustomerLocation>(
    c => c.Address.City == "New York",
    c => new CustomerLocation { Name = c.Name, City = c.Address.City, State = c.Address.State }
)).ToList();
sw.Stop();
Console.WriteLine($"Retrieved {nyLocations.Count} New York customers in {sw.ElapsedMilliseconds}ms");

// Benchmark 5: SelectAsync with multiple nested paths
logger.LogInformation("\nBenchmark 5: SelectAsync with deep nesting (Name, Email, Phone)...");
sw.Restart();
var contacts = await store.SelectAsync<Customer, CustomerContact>(
    c => new CustomerContact { Name = c.Name, Email = c.Email, PrimaryPhone = c.Contact.PrimaryPhone }
);
sw.Stop();
var contactsList = contacts.ToList();
Console.WriteLine($"Retrieved {contactsList.Count} contacts in {sw.ElapsedMilliseconds}ms");
Console.WriteLine($"Sample: {contactsList[0].Name} - {contactsList[0].PrimaryPhone}");

// Demonstrate raw SQL for complex projections
logger.LogInformation("\nBonus: Raw SQL for complex aggregations...");
var query = @"
    SELECT 
        json_extract(data, '$.Address.City') as City,
        COUNT(*) as CustomerCount,
        AVG(CAST(json_extract(data, '$.Age') as INTEGER)) as AvgAge
    FROM Customer
    GROUP BY json_extract(data, '$.Address.City')
    ORDER BY CustomerCount DESC";

var cityStats = await store.Connection.QueryAsync<(string City, int CustomerCount, double AvgAge)>(query);
Console.WriteLine("\nCustomers by City:");
foreach (var (city, count, avgAge) in cityStats)
{
    Console.WriteLine($"  {city}: {count} customers (avg age: {avgAge:F1})");
}

Console.WriteLine("\n✓ Projection Query example completed!");
Console.WriteLine("\nKey Takeaways:");
Console.WriteLine("  • Use SelectAsync<TSource, TResult> to project specific fields");
Console.WriteLine("  • Reduces deserialization overhead and memory usage");
Console.WriteLine("  • Supports nested property access (c.Address.City)");
Console.WriteLine("  • Can combine with predicates for filtered projections");
Console.WriteLine("  • For complex aggregations, use raw SQL with json_extract()");

// Define models
record Address(string Street, string City, string State, string ZipCode, string Country);
record ContactInfo(string PrimaryPhone, string SecondaryPhone, string Fax, string Mobile);
record Preferences(bool EmailNotifications, bool SmsNotifications, string Language, string Timezone);
record OrderHistory(List<string> OrderIds, decimal TotalSpent, DateTime LastOrderDate);

record Customer(
    string Id,
    string Name,
    string Email,
    int Age,
    Address Address,
    ContactInfo Contact,
    Preferences Preferences,
    OrderHistory OrderHistory,
    Dictionary<string, string> Metadata
);

// Projection DTOs
record CustomerSummary
{
    public string Name { get; init; } = null!;
    public string Email { get; init; } = null!;
}

record CustomerLocation
{
    public string Name { get; init; } = null!;
    public string City { get; init; } = null!;
    public string State { get; init; } = null!;
}

record CustomerContact
{
    public string Name { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string PrimaryPhone { get; init; } = null!;
}
