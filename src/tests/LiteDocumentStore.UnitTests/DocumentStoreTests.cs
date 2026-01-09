using Dapper;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LiteDocumentStore.UnitTests;

public class DocumentStoreTests
{
    private readonly string _testDbPath;

    public DocumentStoreTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
    }

    [Fact]
    public void Constructor_WithConnection_CreatesDocumentStore()
    {
        // Arrange & Act
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={_testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();
        using var connection = connectionFactory.CreateConnection(options);
        var store = new DocumentStore(connection);

        // Assert
        Assert.NotNull(store);
        Assert.NotNull(store.Connection);
        Assert.Equal(System.Data.ConnectionState.Open, store.Connection.State);

        // Cleanup
        connection.Close();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task GetTableName_ReturnsTypeName()
    {
        // This tests the interaction indirectly through CreateTableAsync
        var testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();

        using (var connection = connectionFactory.CreateConnection(options))
        {
            var store = new DocumentStore(connection);

            // Act - create table should use type name
            await store.CreateTableAsync<TestPerson>();

            // Assert - verify table exists with correct name
            var checkSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='TestPerson'";
            var result = store.Connection.QueryFirstOrDefault<string>(checkSql);
            Assert.Equal("TestPerson", result);
        }

        // Cleanup
        if (File.Exists(testDbPath))
        {
            try { File.Delete(testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task Store_WithOwnsConnection_DisposesConnection()
    {
        // Arrange
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={_testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();
        var connection = connectionFactory.CreateConnection(options);
        var store = new DocumentStore(connection, ownsConnection: true);

        // Act
        await store.DisposeAsync();

        // Assert - connection should be disposed
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);

        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task Store_WithoutOwnsConnection_DoesNotDisposeConnection()
    {
        // Arrange
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={_testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();
        var connection = connectionFactory.CreateConnection(options);
        var store = new DocumentStore(connection, ownsConnection: false);

        // Act
        await store.DisposeAsync();

        // Assert - connection should still be open
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);

        // Cleanup
        connection.Dispose();
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);

        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task Operations_OnClosedConnection_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={_testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();
        var connection = connectionFactory.CreateConnection(options);
        connection.Close(); // Close the connection
        var store = new DocumentStore(connection, ownsConnection: false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.CreateTableAsync<TestPerson>());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.UpsertAsync("test-id", new TestPerson { Name = "Test" }));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.GetAsync<TestPerson>("test-id"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.GetAllAsync<TestPerson>());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await store.DeleteAsync<TestPerson>("test-id"));

        // Cleanup
        connection.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    private class TestPerson
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
    }
}
