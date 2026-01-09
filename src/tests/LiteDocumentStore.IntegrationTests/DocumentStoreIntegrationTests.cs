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
        await _store.UpsertAsync("person1", person);

        // Assert
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
        await _store.UpsertAsync("person1", person2);

        // Assert
        var retrieved = await _store.GetAsync<Person>("person1");
        Assert.NotNull(retrieved);
        Assert.Equal(31, retrieved.Age);
        Assert.Equal("john.doe@example.com", retrieved.Email);
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

    private class Person
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Email { get; set; } = string.Empty;
    }
}
