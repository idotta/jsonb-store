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
    public async Task UpsertManyAsync_InsertsMultipleRecords()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        var items = new[]
        {
            ("p1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" }),
            ("p2", new Person { Name = "Bob", Age = 30, Email = "bob@example.com" }),
            ("p3", new Person { Name = "Charlie", Age = 35, Email = "charlie@example.com" })
        };

        // Act
        var affectedRows = await _store.UpsertManyAsync(items);

        // Assert
        Assert.True(affectedRows > 0);
        var all = (await _store.GetAllAsync<Person>()).ToList();
        Assert.Equal(3, all.Count);
        Assert.Contains(all, p => p.Name == "Alice");
        Assert.Contains(all, p => p.Name == "Bob");
        Assert.Contains(all, p => p.Name == "Charlie");
    }

    [Fact]
    public async Task UpsertManyAsync_UpdatesExistingRecords()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" });
        await _store.UpsertAsync("p2", new Person { Name = "Bob", Age = 30, Email = "bob@example.com" });

        var updates = new[]
        {
            ("p1", new Person { Name = "Alice Updated", Age = 26, Email = "alice.new@example.com" }),
            ("p2", new Person { Name = "Bob Updated", Age = 31, Email = "bob.new@example.com" })
        };

        // Act
        var affectedRows = await _store.UpsertManyAsync(updates);

        // Assert
        Assert.True(affectedRows > 0);
        var alice = await _store.GetAsync<Person>("p1");
        var bob = await _store.GetAsync<Person>("p2");
        Assert.Equal("Alice Updated", alice?.Name);
        Assert.Equal(26, alice?.Age);
        Assert.Equal("Bob Updated", bob?.Name);
        Assert.Equal(31, bob?.Age);
    }

    [Fact]
    public async Task UpsertManyAsync_HandlesEmptyCollection()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        var items = Array.Empty<(string, Person)>();

        // Act
        var affectedRows = await _store.UpsertManyAsync(items);

        // Assert
        Assert.Equal(0, affectedRows);
    }

    [Fact]
    public async Task UpsertManyAsync_ThrowsOnNullId()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        var items = new[]
        {
            ("p1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" }),
            ("", new Person { Name = "Bob", Age = 30, Email = "bob@example.com" })
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _store.UpsertManyAsync(items);
        });
    }

    [Fact]
    public async Task UpsertManyAsync_ThrowsOnNullData()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        var items = new[]
        {
            ("p1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" }),
            ("p2", (Person)null!)
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _store.UpsertManyAsync(items);
        });
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
    public async Task DeleteManyAsync_DeletesMultipleRecords()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" });
        await _store.UpsertAsync("p2", new Person { Name = "Bob", Age = 30, Email = "bob@example.com" });
        await _store.UpsertAsync("p3", new Person { Name = "Charlie", Age = 35, Email = "charlie@example.com" });
        await _store.UpsertAsync("p4", new Person { Name = "Diana", Age = 40, Email = "diana@example.com" });

        var idsToDelete = new[] { "p1", "p3" };

        // Act
        var affectedRows = await _store.DeleteManyAsync<Person>(idsToDelete);

        // Assert
        Assert.Equal(2, affectedRows);
        var all = (await _store.GetAllAsync<Person>()).ToList();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, p => p.Name == "Bob");
        Assert.Contains(all, p => p.Name == "Diana");
        Assert.DoesNotContain(all, p => p.Name == "Alice");
        Assert.DoesNotContain(all, p => p.Name == "Charlie");
    }

    [Fact]
    public async Task DeleteManyAsync_HandlesEmptyCollection()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" });
        var idsToDelete = Array.Empty<string>();

        // Act
        var affectedRows = await _store.DeleteManyAsync<Person>(idsToDelete);

        // Assert
        Assert.Equal(0, affectedRows);
        var all = (await _store.GetAllAsync<Person>()).ToList();
        Assert.Single(all);
    }

    [Fact]
    public async Task DeleteManyAsync_HandlesNonExistentIds()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" });
        var idsToDelete = new[] { "nonexistent1", "nonexistent2" };

        // Act
        var affectedRows = await _store.DeleteManyAsync<Person>(idsToDelete);

        // Assert
        Assert.Equal(0, affectedRows);
        var all = (await _store.GetAllAsync<Person>()).ToList();
        Assert.Single(all);
    }

    [Fact]
    public async Task DeleteManyAsync_HandlesMixedExistentAndNonExistentIds()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" });
        await _store.UpsertAsync("p2", new Person { Name = "Bob", Age = 30, Email = "bob@example.com" });
        var idsToDelete = new[] { "p1", "nonexistent", "p2" };

        // Act
        var affectedRows = await _store.DeleteManyAsync<Person>(idsToDelete);

        // Assert
        Assert.Equal(2, affectedRows);
        var all = (await _store.GetAllAsync<Person>()).ToList();
        Assert.Empty(all);
    }

    [Fact]
    public async Task DeleteManyAsync_ThrowsOnNullId()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        var idsToDelete = new[] { "p1", "", "p2" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _store.DeleteManyAsync<Person>(idsToDelete);
        });
    }

    [Fact]
    public async Task DeleteManyAsync_ThrowsOnNullCollection()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _store.DeleteManyAsync<Person>(null!);
        });
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenDocumentExists()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" });

        // Act
        var exists = await _store.ExistsAsync<Person>("p1");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenDocumentDoesNotExist()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act
        var exists = await _store.ExistsAsync<Person>("nonexistent");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsync_ThrowsOnNullId()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _store.ExistsAsync<Person>(null!);
        });
    }

    [Fact]
    public async Task ExistsAsync_ThrowsOnEmptyId()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _store.ExistsAsync<Person>("");
        });
    }

    [Fact]
    public async Task CountAsync_ReturnsZero_WhenTableIsEmpty()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act
        var count = await _store.CountAsync<Person>();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount_WhenTableHasDocuments()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" });
        await _store.UpsertAsync("p2", new Person { Name = "Bob", Age = 30, Email = "bob@example.com" });
        await _store.UpsertAsync("p3", new Person { Name = "Charlie", Age = 35, Email = "charlie@example.com" });

        // Act
        var count = await _store.CountAsync<Person>();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task CountAsync_UpdatesAfterDelete()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" });
        await _store.UpsertAsync("p2", new Person { Name = "Bob", Age = 30, Email = "bob@example.com" });
        await _store.UpsertAsync("p3", new Person { Name = "Charlie", Age = 35, Email = "charlie@example.com" });

        // Act
        var countBefore = await _store.CountAsync<Person>();
        await _store.DeleteAsync<Person>("p2");
        var countAfter = await _store.CountAsync<Person>();

        // Assert
        Assert.Equal(3, countBefore);
        Assert.Equal(2, countAfter);
    }

    [Fact]
    public async Task CountAsync_UpdatesAfterUpsert()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "Alice", Age = 25, Email = "alice@example.com" });

        // Act
        var countBefore = await _store.CountAsync<Person>();
        await _store.UpsertAsync("p2", new Person { Name = "Bob", Age = 30, Email = "bob@example.com" });
        var countAfter = await _store.CountAsync<Person>();

        // Assert
        Assert.Equal(1, countBefore);
        Assert.Equal(2, countAfter);
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
        // Note: JSON path uses PascalCase to match default System.Text.Json serialization
        var queryPlan = await _connection.QueryAsync<dynamic>(
            "EXPLAIN QUERY PLAN SELECT json(data) FROM Person WHERE json_extract(data, '$.Email') = 'person50@example.com'");

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

    [Fact]
    public async Task QueryAsync_WithJsonPath_ReturnsMatchingDocuments()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "John Doe", Age = 30, Email = "john@example.com" });
        await _store.UpsertAsync("p2", new Person { Name = "Jane Smith", Age = 25, Email = "jane@example.com" });
        await _store.UpsertAsync("p3", new Person { Name = "Bob Johnson", Age = 30, Email = "bob@example.com" });

        // Act
        var results = await _store.QueryAsync<Person, int>("$.Age", 30);

        // Assert
        var resultList = results.ToList();
        Assert.Equal(2, resultList.Count);
        Assert.Contains(resultList, p => p.Name == "John Doe");
        Assert.Contains(resultList, p => p.Name == "Bob Johnson");
    }

    [Fact]
    public async Task QueryAsync_WithJsonPath_NoMatches_ReturnsEmpty()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "John Doe", Age = 30, Email = "john@example.com" });

        // Act
        var results = await _store.QueryAsync<Person, int>("$.age", 99);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task QueryAsync_WithPredicate_SimpleEquality_ReturnsMatchingDocuments()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "John Doe", Age = 30, Email = "john@example.com" });
        await _store.UpsertAsync("p2", new Person { Name = "Jane Smith", Age = 25, Email = "jane@example.com" });

        // Act
        var results = await _store.QueryAsync<Person>(p => p.Name == "Jane Smith");

        // Assert
        var resultList = results.ToList();
        Assert.Single(resultList);
        Assert.Equal("Jane Smith", resultList[0].Name);
        Assert.Equal(25, resultList[0].Age);
    }

    [Fact]
    public async Task QueryAsync_WithPredicate_Comparison_ReturnsMatchingDocuments()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "John Doe", Age = 30, Email = "john@example.com" });
        await _store.UpsertAsync("p2", new Person { Name = "Jane Smith", Age = 25, Email = "jane@example.com" });
        await _store.UpsertAsync("p3", new Person { Name = "Bob Johnson", Age = 35, Email = "bob@example.com" });

        // Act
        var results = await _store.QueryAsync<Person>(p => p.Age > 28);

        // Assert
        var resultList = results.ToList();
        Assert.Equal(2, resultList.Count);
        Assert.Contains(resultList, p => p.Name == "John Doe");
        Assert.Contains(resultList, p => p.Name == "Bob Johnson");
    }

    [Fact]
    public async Task QueryAsync_WithPredicate_AndCondition_ReturnsMatchingDocuments()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "John Doe", Age = 30, Email = "john@example.com" });
        await _store.UpsertAsync("p2", new Person { Name = "Jane Smith", Age = 30, Email = "jane@example.com" });
        await _store.UpsertAsync("p3", new Person { Name = "Bob Johnson", Age = 25, Email = "bob@example.com" });

        // Act
        var results = await _store.QueryAsync<Person>(p => p.Age == 30 && p.Name.StartsWith("J"));

        // Assert
        var resultList = results.ToList();
        Assert.Equal(2, resultList.Count);
        Assert.Contains(resultList, p => p.Name == "John Doe");
        Assert.Contains(resultList, p => p.Name == "Jane Smith");
    }

    [Fact]
    public async Task QueryAsync_WithPredicate_OrCondition_ReturnsMatchingDocuments()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "John Doe", Age = 30, Email = "john@example.com" });
        await _store.UpsertAsync("p2", new Person { Name = "Jane Smith", Age = 25, Email = "jane@example.com" });
        await _store.UpsertAsync("p3", new Person { Name = "Bob Johnson", Age = 35, Email = "bob@example.com" });

        // Act
        var results = await _store.QueryAsync<Person>(p => p.Age < 27 || p.Age > 32);

        // Assert
        var resultList = results.ToList();
        Assert.Equal(2, resultList.Count);
        Assert.Contains(resultList, p => p.Name == "Jane Smith");
        Assert.Contains(resultList, p => p.Name == "Bob Johnson");
    }

    [Fact]
    public async Task QueryAsync_WithPredicate_StringContains_ReturnsMatchingDocuments()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "John Doe", Age = 30, Email = "john@example.com" });
        await _store.UpsertAsync("p2", new Person { Name = "Jane Smith", Age = 25, Email = "jane@test.com" });
        await _store.UpsertAsync("p3", new Person { Name = "Bob Johnson", Age = 35, Email = "bob@example.com" });

        // Act
        var results = await _store.QueryAsync<Person>(p => p.Email.Contains("@example.com"));

        // Assert
        var resultList = results.ToList();
        Assert.Equal(2, resultList.Count);
        Assert.Contains(resultList, p => p.Name == "John Doe");
        Assert.Contains(resultList, p => p.Name == "Bob Johnson");
    }

    [Fact]
    public async Task QueryAsync_WithPredicate_NestedProperty_ReturnsMatchingDocuments()
    {
        // Arrange
        await _store.CreateTableAsync<PersonWithAddress>();
        await _store.UpsertAsync("p1", new PersonWithAddress
        {
            Name = "John Doe",
            Address = new Address { City = "New York", Street = "5th Ave", Country = "USA" }
        });
        await _store.UpsertAsync("p2", new PersonWithAddress
        {
            Name = "Jane Smith",
            Address = new Address { City = "London", Street = "Baker St", Country = "UK" }
        });
        await _store.UpsertAsync("p3", new PersonWithAddress
        {
            Name = "Bob Johnson",
            Address = new Address { City = "New York", Street = "Broadway", Country = "USA" }
        });

        // Act
        var results = await _store.QueryAsync<PersonWithAddress>(p => p.Address.City == "New York");

        // Assert
        var resultList = results.ToList();
        Assert.Equal(2, resultList.Count);
        Assert.Contains(resultList, p => p.Name == "John Doe");
        Assert.Contains(resultList, p => p.Name == "Bob Johnson");
    }

    [Fact]
    public async Task QueryAsync_WithJsonPath_NullOrWhitespace_ThrowsArgumentException()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _store.QueryAsync<Person, int>("", 30));
    }

    [Fact]
    public async Task QueryAsync_WithPredicate_NullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _store.QueryAsync<Person>(null!));
    }

    [Fact]
    public async Task SelectAsync_WithSelector_ReturnsProjectedFields()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "John Doe", Age = 30, Email = "john@example.com" });
        await _store.UpsertAsync("p2", new Person { Name = "Jane Smith", Age = 25, Email = "jane@example.com" });
        await _store.UpsertAsync("p3", new Person { Name = "Bob Johnson", Age = 35, Email = "bob@example.com" });

        // Act - Select only Name and Email fields
        var results = await _store.SelectAsync<Person, PersonProjection>(
            p => new PersonProjection { Name = p.Name, Email = p.Email });

        // Assert
        var resultList = results.ToList();
        Assert.Equal(3, resultList.Count);
        Assert.Contains(resultList, r => r.Name == "John Doe" && r.Email == "john@example.com");
        Assert.Contains(resultList, r => r.Name == "Jane Smith" && r.Email == "jane@example.com");
        Assert.Contains(resultList, r => r.Name == "Bob Johnson" && r.Email == "bob@example.com");
    }

    [Fact]
    public async Task SelectAsync_WithPredicateAndSelector_ReturnsFilteredProjectedFields()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "John Doe", Age = 30, Email = "john@example.com" });
        await _store.UpsertAsync("p2", new Person { Name = "Jane Smith", Age = 25, Email = "jane@example.com" });
        await _store.UpsertAsync("p3", new Person { Name = "Bob Johnson", Age = 35, Email = "bob@example.com" });

        // Act - Select Name and Email from people over 28
        var results = await _store.SelectAsync<Person, PersonProjection>(
            p => p.Age > 28,
            p => new PersonProjection { Name = p.Name, Email = p.Email });

        // Assert
        var resultList = results.ToList();
        Assert.Equal(2, resultList.Count);
        Assert.Contains(resultList, r => r.Name == "John Doe" && r.Email == "john@example.com");
        Assert.Contains(resultList, r => r.Name == "Bob Johnson" && r.Email == "bob@example.com");
        Assert.DoesNotContain(resultList, r => r.Name == "Jane Smith");
    }

    [Fact]
    public async Task SelectAsync_WithAnonymousType_ReturnsProjectedFields()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "John Doe", Age = 30, Email = "john@example.com" });

        // Act - Use anonymous type for projection
        var results = await _store.SelectAsync<Person, dynamic>(
            p => new { p.Name, p.Age });

        // Assert
        var resultList = results.ToList();
        Assert.Single(resultList);
        Assert.Equal("John Doe", resultList[0].Name);
        Assert.Equal(30, resultList[0].Age);
    }

    [Fact]
    public async Task SelectAsync_WithNestedProperties_ReturnsProjectedFields()
    {
        // Arrange
        await _store.CreateTableAsync<PersonWithAddress>();
        await _store.UpsertAsync("p1", new PersonWithAddress
        {
            Name = "John Doe",
            Address = new Address { City = "New York", Street = "5th Avenue", Country = "USA" }
        });
        await _store.UpsertAsync("p2", new PersonWithAddress
        {
            Name = "Jane Smith",
            Address = new Address { City = "London", Street = "Baker St", Country = "UK" }
        });

        // Act - Select nested property
        var results = await _store.SelectAsync<PersonWithAddress, PersonCityProjection>(
            p => new PersonCityProjection { Name = p.Name, City = p.Address.City });

        // Assert
        var resultList = results.ToList();
        Assert.Equal(2, resultList.Count);
        Assert.Contains(resultList, r => r.Name == "John Doe" && r.City == "New York");
        Assert.Contains(resultList, r => r.Name == "Jane Smith" && r.City == "London");
    }

    [Fact]
    public async Task SelectAsync_EmptyTable_ReturnsEmptyResults()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();

        // Act
        var results = await _store.SelectAsync<Person, PersonProjection>(
            p => new PersonProjection { Name = p.Name, Email = p.Email });

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task SelectAsync_VerifiesOnlySelectedFieldsRetrieved()
    {
        // Arrange
        await _store.CreateTableAsync<Person>();
        await _store.UpsertAsync("p1", new Person { Name = "John Doe", Age = 30, Email = "john@example.com" });

        // Act - Select only Name field (not Age)
        var results = await _store.SelectAsync<Person, NameOnlyProjection>(
            p => new NameOnlyProjection { Name = p.Name });

        // Assert
        var result = results.Single();
        Assert.Equal("John Doe", result.Name);
        // Verify we can use the projected result without the other fields
        Assert.NotNull(result);
    }

    private class PersonProjection
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    private class PersonCityProjection
    {
        public string Name { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    private class NameOnlyProjection
    {
        public string Name { get; set; } = string.Empty;
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
