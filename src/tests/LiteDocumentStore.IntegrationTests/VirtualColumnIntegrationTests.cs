using Dapper;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LiteDocumentStore.IntegrationTests;

/// <summary>
/// Integration tests for virtual column functionality.
/// Tests cover virtual column creation, caching, and query optimization.
/// </summary>
public class VirtualColumnIntegrationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnection _connection;
    private readonly DocumentStore _store;

    public VirtualColumnIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_virtualcol_{Guid.NewGuid()}.db");
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={_testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();

        _connection = connectionFactory.CreateConnection(options);
        _store = new DocumentStore(_connection);
    }

    public void Dispose()
    {
        _connection.Dispose();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); }
            catch (IOException) { /* ignore */ }
        }
    }

    #region Virtual Column Creation Tests

    [Fact]
    public async Task AddVirtualColumnAsync_Debug_SQLiteSyntax()
    {
        // Debug test to verify SQLite virtual column syntax works
        using var memConnection = new SqliteConnection("Data Source=:memory:");
        await memConnection.OpenAsync();

        var version = await memConnection.QueryFirstAsync<string>("SELECT sqlite_version()");

        await memConnection.ExecuteAsync(@"
            CREATE TABLE Test1 (
                id INTEGER PRIMARY KEY,
                a INTEGER,
                b INTEGER,
                c INTEGER GENERATED ALWAYS AS (a + b)
            )");

        var cols1 = (await memConnection.QueryAsync("PRAGMA table_xinfo(Test1)")).ToList();
        var colNames = cols1.Select(c => (string)c.name).ToList();

        await memConnection.ExecuteAsync("INSERT INTO Test1 (id, a, b) VALUES (1, 10, 20)");
        var result = await memConnection.QueryFirstOrDefaultAsync<dynamic>("SELECT id, a, b, c FROM Test1 WHERE id = 1");

        Assert.True(colNames.Contains("c") || result?.c != null,
            $"Generated column 'c' not found. Columns: [{string.Join(", ", colNames)}]. SQLite version: {version}");
    }

    [Fact]
    public async Task AddVirtualColumnAsync_CreatesVirtualColumn()
    {
        // Arrange
        await _store.CreateTableAsync<Product>();
        await _store.UpsertAsync("p1", new Product { Name = "Widget", Category = "Electronics", Price = 29.99m });

        // Act
        await _store.AddVirtualColumnAsync<Product>(x => x.Category, "category");

        // Assert
        var introspector = new SchemaIntrospector(_connection);
        var columns = await introspector.GetColumnsAsync("Product");
        Assert.Contains(columns, c => c.Name == "category");
    }

    [Fact]
    public async Task AddVirtualColumnAsync_WithIndex_CreatesColumnAndIndex()
    {
        // Arrange
        await _store.CreateTableAsync<Product>();
        await _store.UpsertAsync("p1", new Product { Name = "Widget", Category = "Electronics", Price = 29.99m });

        // Act
        await _store.AddVirtualColumnAsync<Product>(x => x.Price, "price", createIndex: true, columnType: "REAL");

        // Assert
        var introspector = new SchemaIntrospector(_connection);
        var columns = await introspector.GetColumnsAsync("Product");
        Assert.Contains(columns, c => c.Name == "price");

        var indexExists = await introspector.IndexExistsAsync("idx_Product_price");
        Assert.True(indexExists);
    }

    [Fact]
    public async Task AddVirtualColumnAsync_NestedProperty_CreatesColumn()
    {
        // Arrange
        await _store.CreateTableAsync<ProductWithMetadata>();
        await _store.UpsertAsync("p1", new ProductWithMetadata
        {
            Name = "Widget",
            Metadata = new ProductMetadata { Brand = "Acme", Country = "USA" }
        });

        // Act
        await _store.AddVirtualColumnAsync<ProductWithMetadata>(x => x.Metadata.Brand, "brand", createIndex: true);

        // Assert
        var introspector = new SchemaIntrospector(_connection);
        var columns = await introspector.GetColumnsAsync("ProductWithMetadata");
        Assert.Contains(columns, c => c.Name == "brand");

        var indexExists = await introspector.IndexExistsAsync("idx_ProductWithMetadata_brand");
        Assert.True(indexExists);
    }

    [Fact]
    public async Task AddVirtualColumnAsync_ColumnAlreadyExists_DoesNotThrow()
    {
        // Arrange
        await _store.CreateTableAsync<Product>();
        await _store.AddVirtualColumnAsync<Product>(x => x.Category, "category");

        // Act & Assert - should not throw
        await _store.AddVirtualColumnAsync<Product>(x => x.Category, "category");

        var introspector = new SchemaIntrospector(_connection);
        var columns = await introspector.GetColumnsAsync("Product");
        Assert.Contains(columns, c => c.Name == "category");
    }

    [Fact]
    public async Task AddVirtualColumnAsync_VirtualColumnValues_AreCorrect()
    {
        // Arrange
        await _store.CreateTableAsync<Product>();
        await _store.UpsertAsync("p1", new Product { Name = "Widget", Category = "Electronics", Price = 29.99m });
        await _store.UpsertAsync("p2", new Product { Name = "Gadget", Category = "Electronics", Price = 49.99m });
        await _store.UpsertAsync("p3", new Product { Name = "Tool", Category = "Hardware", Price = 19.99m });

        // Act
        await _store.AddVirtualColumnAsync<Product>(x => x.Category, "category");
        await _store.AddVirtualColumnAsync<Product>(x => x.Price, "price", columnType: "REAL");

        // Assert - Query using virtual columns directly
        var results = await _connection.QueryAsync<dynamic>(
            "SELECT id, category, price FROM Product WHERE category = 'Electronics' ORDER BY price");
        var resultList = results.ToList();

        Assert.Equal(2, resultList.Count);
        Assert.Equal("Electronics", (string)resultList[0].category);
    }

    #endregion

    #region QueryAsync with Virtual Columns Tests

    [Fact]
    public async Task QueryAsync_WithVirtualColumn_UsesVirtualColumnInSql()
    {
        // Arrange
        await _store.CreateTableAsync<Product>();
        await _store.UpsertAsync("p1", new Product { Name = "Widget", Category = "Electronics", Price = 29.99m });
        await _store.UpsertAsync("p2", new Product { Name = "Gadget", Category = "Toys", Price = 49.99m });
        await _store.UpsertAsync("p3", new Product { Name = "Tool", Category = "Hardware", Price = 19.99m });

        // Add virtual column
        await _store.AddVirtualColumnAsync<Product>(p => p.Category, "category", createIndex: true);

        // Act - QueryAsync should now use the virtual column
        var results = await _store.QueryAsync<Product>(p => p.Category == "Electronics");

        // Assert
        Assert.Single(results);
        Assert.Equal("Widget", results.First().Name);
    }

    [Fact]
    public async Task QueryAsync_WithVirtualColumn_SupportsRangeQueries()
    {
        // Arrange
        await _store.CreateTableAsync<Product>();
        await _store.UpsertAsync("p1", new Product { Name = "Cheap", Category = "A", Price = 10.00m });
        await _store.UpsertAsync("p2", new Product { Name = "Medium", Category = "B", Price = 50.00m });
        await _store.UpsertAsync("p3", new Product { Name = "Expensive", Category = "C", Price = 100.00m });

        // Add virtual column for Price
        await _store.AddVirtualColumnAsync<Product>(p => p.Price, "price", createIndex: true, columnType: "REAL");

        // Act - QueryAsync should use virtual column for range query
        var results = await _store.QueryAsync<Product>(p => p.Price > 30);

        // Assert
        Assert.Equal(2, results.Count());
        Assert.Contains(results, p => p.Name == "Medium");
        Assert.Contains(results, p => p.Name == "Expensive");
    }

    [Fact]
    public async Task QueryAsync_WithVirtualColumn_SupportsStringMethods()
    {
        // Arrange
        await _store.CreateTableAsync<Product>();
        await _store.UpsertAsync("p1", new Product { Name = "Widget Pro", Category = "Electronics", Price = 29.99m });
        await _store.UpsertAsync("p2", new Product { Name = "Widget Basic", Category = "Electronics", Price = 19.99m });
        await _store.UpsertAsync("p3", new Product { Name = "Gadget", Category = "Toys", Price = 9.99m });

        // Add virtual column
        await _store.AddVirtualColumnAsync<Product>(p => p.Name, "name_vc", createIndex: true);

        // Act - QueryAsync with StartsWith
        var results = await _store.QueryAsync<Product>(p => p.Name.StartsWith("Widget"));

        // Assert
        Assert.Equal(2, results.Count());
        Assert.All(results, p => Assert.StartsWith("Widget", p.Name));
    }

    [Fact]
    public async Task QueryAsync_WithMultipleVirtualColumns_UsesAllColumns()
    {
        // Arrange
        await _store.CreateTableAsync<Product>();
        await _store.UpsertAsync("p1", new Product { Name = "Widget", Category = "Electronics", Price = 29.99m });
        await _store.UpsertAsync("p2", new Product { Name = "Gadget", Category = "Electronics", Price = 49.99m });
        await _store.UpsertAsync("p3", new Product { Name = "Tool", Category = "Hardware", Price = 19.99m });

        // Add multiple virtual columns
        await _store.AddVirtualColumnAsync<Product>(p => p.Category, "category", createIndex: true);
        await _store.AddVirtualColumnAsync<Product>(p => p.Price, "price", createIndex: true, columnType: "REAL");

        // Act - Query with both conditions
        var results = await _store.QueryAsync<Product>(p => p.Category == "Electronics" && p.Price > 30);

        // Assert
        Assert.Single(results);
        Assert.Equal("Gadget", results.First().Name);
    }

    #endregion

    #region Virtual Column Cache Tests

    [Fact]
    public async Task QueryAsync_LoadsVirtualColumnsFromSchema()
    {
        // Arrange - Create virtual column with initial store
        await _store.CreateTableAsync<Product>();
        await _store.UpsertAsync("p1", new Product { Name = "Test", Category = "TestCat", Price = 10.00m });
        await _store.AddVirtualColumnAsync<Product>(p => p.Name, "name_vc", createIndex: true);

        // Create a NEW store instance (simulating app restart)
        using var newStore = new DocumentStore(_connection);

        // Act - Query should discover and use the virtual column from schema
        var results = await newStore.QueryAsync<Product>(p => p.Name == "Test");

        // Assert
        Assert.Single(results);
        Assert.Equal("TestCat", results.First().Category);
    }

    [Fact]
    public async Task QueryAsync_CacheUpdatedWhenVirtualColumnAdded()
    {
        // Arrange
        await _store.CreateTableAsync<Product>();
        await _store.UpsertAsync("p1", new Product { Name = "Widget", Category = "Electronics", Price = 29.99m });

        // First query - no virtual columns, uses json_extract
        var results1 = await _store.QueryAsync<Product>(p => p.Category == "Electronics");
        Assert.Single(results1);

        // Add virtual column
        await _store.AddVirtualColumnAsync<Product>(p => p.Category, "category", createIndex: true);

        // Second query - should now use virtual column
        var results2 = await _store.QueryAsync<Product>(p => p.Category == "Electronics");
        Assert.Single(results2);
    }

    [Fact]
    public async Task QueryAsync_NestedVirtualColumn_IsDiscoveredFromSchema()
    {
        // Arrange
        await _store.CreateTableAsync<ProductWithMetadata>();
        await _store.UpsertAsync("p1", new ProductWithMetadata
        {
            Name = "Widget",
            Metadata = new ProductMetadata { Brand = "Acme", Country = "USA" }
        });
        await _store.AddVirtualColumnAsync<ProductWithMetadata>(p => p.Metadata.Brand, "brand");

        // Create new store
        using var newStore = new DocumentStore(_connection);

        // Act
        var results = await newStore.QueryAsync<ProductWithMetadata>(p => p.Metadata.Brand == "Acme");

        // Assert
        Assert.Single(results);
        Assert.Equal("Widget", results.First().Name);
    }

    #endregion

    #region Index Usage Verification

    [Fact]
    public async Task AddVirtualColumnAsync_IndexCanBeUsed_InRawQuery()
    {
        // Arrange
        await _store.CreateTableAsync<Product>();
        for (int i = 0; i < 100; i++)
        {
            await _store.UpsertAsync($"p{i}", new Product
            {
                Name = $"Product {i}",
                Category = $"Category {i % 10}",
                Price = 10 + i
            });
        }

        await _store.AddVirtualColumnAsync<Product>(x => x.Category, "category", createIndex: true);

        // Act - Query using index
        var result = await _connection.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM Product WHERE category = @Category",
            new { Category = "Category 5" });

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task QueryAsync_WithIndex_ReturnsCorrectResults()
    {
        // Arrange
        await _store.CreateTableAsync<Product>();
        var expectedProducts = new List<string>();

        for (int i = 0; i < 50; i++)
        {
            var category = i % 5 == 0 ? "Target" : $"Other{i}";
            if (category == "Target") expectedProducts.Add($"Product {i}");

            await _store.UpsertAsync($"p{i}", new Product
            {
                Name = $"Product {i}",
                Category = category,
                Price = 10 + i
            });
        }

        await _store.AddVirtualColumnAsync<Product>(p => p.Category, "category", createIndex: true);

        // Act
        var results = await _store.QueryAsync<Product>(p => p.Category == "Target");

        // Assert
        Assert.Equal(expectedProducts.Count, results.Count());
        Assert.All(results, p => Assert.Equal("Target", p.Category));
    }

    #endregion

    #region Test Models

    private class Product
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private class ProductWithMetadata
    {
        public string Name { get; set; } = string.Empty;
        public ProductMetadata Metadata { get; set; } = new();
    }

    private class ProductMetadata
    {
        public string Brand { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }

    #endregion
}
