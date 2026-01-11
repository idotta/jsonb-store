using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace LiteDocumentStore.Benchmarks;

/// <summary>
/// Benchmarks comparing full document retrieval vs projection queries.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 15)]
public class ProjectionQueryBenchmark
{
    private IDocumentStore _store = null!;
    private ServiceProvider _serviceProvider = null!;
    private const int DocumentCount = 1000;

    [GlobalSetup]
    public async Task Setup()
    {
        // Setup DI container with in-memory database
        var services = new ServiceCollection();
        services.AddLiteDocumentStore(options =>
        {
            options.ConnectionString = "Data Source=:memory:";
            options.EnableWalMode = false; // WAL not supported in :memory:
        });

        _serviceProvider = services.BuildServiceProvider();
        _store = _serviceProvider.GetRequiredService<IDocumentStore>();

        // Create table and seed with realistic data
        await _store.CreateTableAsync<LargeDocument>();

        var documents = new List<(string id, LargeDocument data)>();
        for (int i = 0; i < DocumentCount; i++)
        {
            var doc = new LargeDocument
            {
                Id = $"doc-{i}",
                Name = $"Document {i}",
                Email = $"user{i}@example.com",
                Description = $"This is a detailed description for document {i} with lots of text to make it more realistic and increase the payload size significantly.",
                Category = $"Category {i % 10}",
                Tags = [$"tag{i}", $"tag{i + 1}", $"tag{i + 2}"],
                Metadata = new Dictionary<string, string>
                {
                    ["CreatedBy"] = $"User {i % 100}",
                    ["Department"] = $"Dept {i % 20}",
                    ["Version"] = "1.0",
                    ["Status"] = i % 3 == 0 ? "Active" : "Inactive"
                },
                Content = new ContentBlock
                {
                    Title = $"Content Title {i}",
                    Body = string.Join(" ", Enumerable.Range(0, 50).Select(j => $"Word{j}")),
                    Author = $"Author {i % 50}",
                    PublishDate = DateTime.Now.AddDays(-i),
                    ViewCount = i * 10,
                    LikeCount = i * 2
                },
                Attachments = Enumerable.Range(0, 5).Select(j => new Attachment
                {
                    FileName = $"file{j}.pdf",
                    FileSize = 1024 * (j + 1),
                    MimeType = "application/pdf",
                    Url = $"https://example.com/files/file{j}.pdf"
                }).ToList(),
                Timestamps = new TimestampInfo
                {
                    CreatedAt = DateTime.Now.AddDays(-i),
                    UpdatedAt = DateTime.Now.AddDays(-i / 2),
                    LastAccessedAt = DateTime.Now
                },
                Stats = new Statistics
                {
                    TotalViews = i * 100,
                    UniqueVisitors = i * 50,
                    AvgTimeOnPage = TimeSpan.FromSeconds(i % 300),
                    BounceRate = i % 100 / 100.0
                }
            };

            documents.Add((doc.Id, doc));
        }

        // Bulk insert for faster setup
        await _store.UpsertManyAsync(documents);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_store != null)
        {
            await _store.DisposeAsync();
        }
        _serviceProvider?.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Full document retrieval (baseline)")]
    public async Task<int> GetAllAsync_FullDocuments()
    {
        var documents = await _store.GetAllAsync<LargeDocument>();
        return documents.Count();
    }

    [Benchmark(Description = "Projection query - 2 fields")]
    public async Task<int> SelectAsync_TwoFields()
    {
        var projections = await _store.SelectAsync<LargeDocument, TwoFieldProjection>(
            d => new TwoFieldProjection { Id = d.Id, Name = d.Name });
        return projections.Count();
    }

    [Benchmark(Description = "Projection query - 4 fields")]
    public async Task<int> SelectAsync_FourFields()
    {
        var projections = await _store.SelectAsync<LargeDocument, FourFieldProjection>(
            d => new FourFieldProjection
            {
                Id = d.Id,
                Name = d.Name,
                Email = d.Email,
                Category = d.Category
            });
        return projections.Count();
    }

    [Benchmark(Description = "Projection query - nested field")]
    public async Task<int> SelectAsync_NestedField()
    {
        var projections = await _store.SelectAsync<LargeDocument, NestedFieldProjection>(
            d => new NestedFieldProjection
            {
                Name = d.Name,
                ContentTitle = d.Content.Title,
                ContentAuthor = d.Content.Author
            });
        return projections.Count();
    }

    [Benchmark(Description = "Projection query with filter - 2 fields")]
    public async Task<int> SelectAsync_WithFilter_TwoFields()
    {
        var projections = await _store.SelectAsync<LargeDocument, TwoFieldProjection>(
            d => d.Category == "Category 5",
            d => new TwoFieldProjection { Id = d.Id, Name = d.Name });
        return projections.Count();
    }

    [Benchmark(Description = "Full documents with filter (for comparison)")]
    public async Task<int> QueryAsync_WithFilter_FullDocuments()
    {
        var documents = await _store.QueryAsync<LargeDocument>(
            d => d.Category == "Category 5");
        return documents.Count();
    }
}

/// <summary>
/// Large document type with many fields to simulate realistic scenarios.
/// Approximately 2-3 KB per document when serialized.
/// </summary>
public class LargeDocument
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
    public ContentBlock Content { get; set; } = new();
    public List<Attachment> Attachments { get; set; } = [];
    public TimestampInfo Timestamps { get; set; } = new();
    public Statistics Stats { get; set; } = new();
}

public class ContentBlock
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime PublishDate { get; set; }
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
}

public class Attachment
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class TimestampInfo
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
}

public class Statistics
{
    public int TotalViews { get; set; }
    public int UniqueVisitors { get; set; }
    public TimeSpan AvgTimeOnPage { get; set; }
    public double BounceRate { get; set; }
}

// Projection DTOs
public class TwoFieldProjection
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class FourFieldProjection
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class NestedFieldProjection
{
    public string Name { get; set; } = string.Empty;
    public string ContentTitle { get; set; } = string.Empty;
    public string ContentAuthor { get; set; } = string.Empty;
}
