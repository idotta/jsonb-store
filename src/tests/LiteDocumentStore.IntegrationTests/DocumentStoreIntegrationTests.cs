using Dapper;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LiteDocumentStore.IntegrationTests;

public class DocumentStoreIntegrationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnection _connection;
    private readonly DocumentStore _store;

    public DocumentStoreIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_integration_{Guid.NewGuid()}.db");
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={_testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();

        // Manual connection management
        _connection = connectionFactory.CreateConnection(options);
        _store = new DocumentStore(_connection);
    }

    public void Dispose()
    {
        _connection.Dispose();

        // Force garbage collection to ensure connection is fully closed
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        if (File.Exists(_testDbPath))
        {
            try
            {
                File.Delete(_testDbPath);
            }
            catch (IOException)
            {
                // Sometimes the file is still locked, ignore for tests
            }
        }
    }

    [Fact]
    public async Task CreateTableAsync_CreatesTable()
    {
        // Act
        await _store.CreateTableAsync<Person>();

        // Assert
        var checkSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='Person'";
        var result = _store.Connection.QueryFirstOrDefault<string>(checkSql);
        Assert.Equal("Person", result);
    }

    [Fact]
    public async Task UpsertAsync_InsertsNewRecord()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        var person = new Person { Name = "John Doe", Age = 30, Email = "john@example.com" };

        // Act
        var affectedRows = await _store.UpsertAsync("person1", person);

        // Assert
        Assert.True(affectedRows > 0);
        var retrieved = await _store.GetAsync<Person>("person1");
        Assert.NotNull(retrieved);
        Assert.Equal("John Doe", retrieved.Name);
        Assert.Equal(30, retrieved.Age);
        Assert.Equal("john@example.com", retrieved.Email);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingRecord()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        var person1 = new Person { Name = "John Doe", Age = 30, Email = "john@example.com" };
        await _store.UpsertAsync("person1", person1);

        // Act
        var person2 = new Person { Name = "John Doe", Age = 31, Email = "john.doe@example.com" };
        var affectedRows = await _store.UpsertAsync("person1", person2);

        // Assert
        Assert.True(affectedRows > 0);
        var retrieved = await _store.GetAsync<Person>("person1");
        Assert.NotNull(retrieved);
        Assert.Equal(31, retrieved.Age);
        Assert.Equal("john.doe@example.com", retrieved.Email);
    }

    [Fact]
    public async Task UpsertAsync_ReturnsAffectedRowsCount()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        var person = new Person { Name = "Test", Age = 25, Email = "test@example.com" };

        // Act - First upsert (insert)
        var insertResult = await _store.UpsertAsync("test1", person);

        // Assert - Insert should affect rows
        Assert.True(insertResult > 0, "Insert should return affected rows count > 0");

        // Act - Second upsert on same ID (update)
        person.Age = 26;
        var updateResult = await _store.UpsertAsync("test1", person);

        // Assert - Update should also affect rows
        Assert.True(updateResult > 0, "Update should return affected rows count > 0");
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenRecordNotFound()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act
        var result = await _store.GetAsync<Person>("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRecords()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("1", new Person { Name = "A" });
        await _store.UpsertAsync("2", new Person { Name = "B" });
        await _store.UpsertAsync("3", new Person { Name = "C" });

        // Act
        var results = await _store.GetAllAsync<Person>();

        // Assert
        Assert.Equal(3, results.Count());
        Assert.Contains(results, p => p.Name == "A");
        Assert.Contains(results, p => p.Name == "B");
        Assert.Contains(results, p => p.Name == "C");
    }

    [Fact]
    public async Task DeleteAsync_RemovesRecord()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("1", new Person { Name = "A" });

        // Act
        var deleted = await _store.DeleteAsync<Person>("1");
        var result = await _store.GetAsync<Person>("1");

        // Assert
        Assert.True(deleted);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenRecordNotFound()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act
        var deleted = await _store.DeleteAsync<Person>("nonexistent");

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_CommitsChanges_WhenSuccessful()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act
        await _store.ExecuteInTransactionAsync(async () =>
        {
            await _store.UpsertAsync("1", new Person { Name = "A" });
            await _store.UpsertAsync("2", new Person { Name = "B" });
        });

        // Assert
        var results = await _store.GetAllAsync<Person>();
        Assert.Equal(2, results.Count());
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RollsBackChanges_WhenExceptionOccurs()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("1", new Person { Name = "Initial" });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _store.ExecuteInTransactionAsync(async () =>
            {
                await _store.UpsertAsync("2", new Person { Name = "Should not exist" });
                throw new InvalidOperationException("Force rollback");
            });
        });

        // Assert
        var results = await _store.GetAllAsync<Person>();
        Assert.Single(results);
        Assert.Equal("Initial", results.First().Name);
        var p2 = await _store.GetAsync<Person>("2");
        Assert.Null(p2);
    }

    [Fact]
    public async Task IsHealthyAsync_WithValidConnection_ReturnsTrue()
    {
        // Act
        var isHealthy = await _store.IsHealthyAsync();

        // Assert
        Assert.True(isHealthy);
    }

    [Fact]
    public async Task IsHealthyAsync_ValidatesSqliteVersion()
    {
        // Act
        var isHealthy = await _store.IsHealthyAsync();

        // Assert
        Assert.True(isHealthy);

        // Verify SQLite version is 3.45+
        var version = await _connection.QueryFirstOrDefaultAsync<string>("SELECT sqlite_version()");
        Assert.NotNull(version);
        Assert.True(Version.TryParse(version, out var sqliteVersion));
        Assert.True(sqliteVersion >= new Version(3, 45, 0),
            $"SQLite version {sqliteVersion} should be 3.45.0 or higher for JSONB support");
    }

    [Fact]
    public async Task IsHealthyAsync_CanExecuteBasicQuery()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("test", new Person { Name = "Test" });

        // Act
        var isHealthy = await _store.IsHealthyAsync();

        // Assert
        Assert.True(isHealthy);

        // Verify we can still perform operations after health check
        var person = await _store.GetAsync<Person>("test");
        Assert.NotNull(person);
        Assert.Equal("Test", person.Name);
    }

    [Fact]
    public async Task CreateIndexAsync_CreatesIndex_OnJsonPath()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act
        await _store.CreateIndexAsync<Person>(p => p.Email, "idx_person_email");

        // Assert
        var checkSql = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_person_email'";
        var count = await _connection.QueryFirstOrDefaultAsync<int>(checkSql);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateIndexAsync_WithAutoGeneratedName_CreatesIndex()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act
        await _store.CreateIndexAsync<Person>(p => p.Name);

        // Assert - check that an index was created (name is auto-generated)
        var checkSql = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name LIKE 'idx_Person_name%'";
        var count = await _connection.QueryFirstOrDefaultAsync<int>(checkSql);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateIndexAsync_IndexAlreadyExists_DoesNotThrow()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.CreateIndexAsync<Person>(p => p.Email, "idx_person_email");

        // Act - creating the same index again should not throw
        await _store.CreateIndexAsync<Person>(p => p.Email, "idx_person_email");

        // Assert
        var checkSql = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_person_email'";
        var count = await _connection.QueryFirstOrDefaultAsync<int>(checkSql);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateIndexAsync_NestedProperty_CreatesIndex()
    {
        // Arrange
        await _store.CreateTableAsync<PersonWithAddress>();

        // Act
        await _store.CreateIndexAsync<PersonWithAddress>(p => p.Address.City, "idx_person_address_city");

        // Assert
        var checkSql = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_person_address_city'";
        var count = await _connection.QueryFirstOrDefaultAsync<int>(checkSql);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateCompositeIndexAsync_CreatesIndex_OnMultipleJsonPaths()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act
        await _store.CreateCompositeIndexAsync<Person>(
            new System.Linq.Expressions.Expression<Func<Person, object>>[]
            {
                p => p.Name,
                p => p.Age
            },
            "idx_person_name_age");

        // Assert
        var checkSql = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_person_name_age'";
        var count = await _connection.QueryFirstOrDefaultAsync<int>(checkSql);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateCompositeIndexAsync_WithAutoGeneratedName_CreatesIndex()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act
        await _store.CreateCompositeIndexAsync<Person>(
            new System.Linq.Expressions.Expression<Func<Person, object>>[]
            {
                p => p.Name,
                p => p.Email
            });

        // Assert - check that a composite index was created
        var checkSql = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name LIKE 'idx_Person_composite_%'";
        var count = await _connection.QueryFirstOrDefaultAsync<int>(checkSql);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateCompositeIndexAsync_IndexAlreadyExists_DoesNotThrow()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.CreateCompositeIndexAsync<Person>(
            new System.Linq.Expressions.Expression<Func<Person, object>>[]
            {
                p => p.Name,
                p => p.Age
            },
            "idx_person_name_age");

        // Act - creating the same index again should not throw
        await _store.CreateCompositeIndexAsync<Person>(
            new System.Linq.Expressions.Expression<Func<Person, object>>[]
            {
                p => p.Name,
                p => p.Age
            },
            "idx_person_name_age");

        // Assert
        var checkSql = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_person_name_age'";
        var count = await _connection.QueryFirstOrDefaultAsync<int>(checkSql);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateIndexAsync_ImprovesQueryPerformance()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Insert test data
        for (int i = 0; i < 100; i++)
        {
            await _store.UpsertAsync($"person{i}", new Person
            {
                Name = $"Person {i}",
                Age = 20 + (i % 50),
                Email = $"person{i}@example.com"
            });
        }

        // Act - Create index
        await _store.CreateIndexAsync<Person>(p => p.Email);

        // Assert - Verify the index exists and can be used
        var queryPlan = await _connection.QueryAsync<dynamic>(
            "EXPLAIN QUERY PLAN SELECT json(data) FROM Person WHERE json_extract(data, '$.email') = 'person50@example.com'");

        // The query plan should mention the index
        var planText = string.Join(" ", queryPlan.Select(p => p.detail));
        Assert.Contains("idx_", planText.ToLower());
    }

    [Fact]
    public async Task CreateCompositeIndexAsync_WithEmptyArray_ThrowsArgumentException()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _store.CreateCompositeIndexAsync<Person>(Array.Empty<System.Linq.Expressions.Expression<Func<Person, object>>>());
        });
    }

    private class Person
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    private class PersonWithAddress
    {
        public string Name { get; set; } = string.Empty;
        public Address Address { get; set; } = new Address();
    }

    private class Address
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }
}
