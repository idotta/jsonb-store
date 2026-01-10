using Dapper;
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

    [Fact]
    public async Task IsHealthyAsync_WithOpenConnection_ReturnsTrue()
    {
        // Arrange
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={_testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();
        using var connection = connectionFactory.CreateConnection(options);
        var store = new DocumentStore(connection, ownsConnection: false);

        // Act
        var isHealthy = await store.IsHealthyAsync();

        // Assert
        Assert.True(isHealthy);

        // Cleanup
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task IsHealthyAsync_WithClosedConnection_ReturnsFalse()
    {
        // Arrange
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={_testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();
        var connection = connectionFactory.CreateConnection(options);
        connection.Close();
        var store = new DocumentStore(connection, ownsConnection: false);

        // Act
        var isHealthy = await store.IsHealthyAsync();

        // Assert
        Assert.False(isHealthy);

        // Cleanup
        connection.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task IsHealthyAsync_OnDisposedStore_ReturnsFalse()
    {
        // Arrange
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={_testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();
        var connection = connectionFactory.CreateConnection(options);
        var store = new DocumentStore(connection, ownsConnection: false);

        // Act
        await store.DisposeAsync();
        var isHealthy = await store.IsHealthyAsync();

        // Assert
        Assert.False(isHealthy);

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

    private class TestCustomer
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Age { get; set; }
        public bool Active { get; set; }
    }

    private class CustomerProjection
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    [Fact]
    public async Task SelectAsync_WithSelector_ReturnsProjectedFields()
    {
        // Arrange
        var testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();

        using (var connection = connectionFactory.CreateConnection(options))
        {
            var store = new DocumentStore(connection);
            await store.CreateTableAsync<TestCustomer>();

            // Insert test data
            await store.UpsertAsync("1", new TestCustomer
            {
                Id = "1",
                Name = "John Doe",
                Email = "john@example.com",
                Age = 30,
                Active = true
            });
            await store.UpsertAsync("2", new TestCustomer
            {
                Id = "2",
                Name = "Jane Smith",
                Email = "jane@example.com",
                Age = 25,
                Active = true
            });

            // Act - Select only Name and Email
            var results = await store.SelectAsync<TestCustomer, CustomerProjection>(
                x => new CustomerProjection { Name = x.Name, Email = x.Email });

            // Assert
            var resultList = results.ToList();
            Assert.Equal(2, resultList.Count);
            Assert.Contains(resultList, r => r.Name == "John Doe" && r.Email == "john@example.com");
            Assert.Contains(resultList, r => r.Name == "Jane Smith" && r.Email == "jane@example.com");
        }

        // Cleanup
        if (File.Exists(testDbPath))
        {
            try { File.Delete(testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task SelectAsync_WithPredicateAndSelector_ReturnsFilteredProjectedFields()
    {
        // Arrange
        var testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();

        using (var connection = connectionFactory.CreateConnection(options))
        {
            var store = new DocumentStore(connection);
            await store.CreateTableAsync<TestCustomer>();

            // Insert test data
            await store.UpsertAsync("1", new TestCustomer
            {
                Id = "1",
                Name = "John Doe",
                Email = "john@example.com",
                Age = 30,
                Active = true
            });
            await store.UpsertAsync("2", new TestCustomer
            {
                Id = "2",
                Name = "Jane Smith",
                Email = "jane@example.com",
                Age = 25,
                Active = false
            });
            await store.UpsertAsync("3", new TestCustomer
            {
                Id = "3",
                Name = "Bob Johnson",
                Email = "bob@example.com",
                Age = 35,
                Active = true
            });

            // Act - Select Name and Email from only active customers
            var results = await store.SelectAsync<TestCustomer, CustomerProjection>(
                x => x.Active == true,
                x => new CustomerProjection { Name = x.Name, Email = x.Email });

            // Assert
            var resultList = results.ToList();
            Assert.Equal(2, resultList.Count);
            Assert.Contains(resultList, r => r.Name == "John Doe");
            Assert.Contains(resultList, r => r.Name == "Bob Johnson");
            Assert.DoesNotContain(resultList, r => r.Name == "Jane Smith");
        }

        // Cleanup
        if (File.Exists(testDbPath))
        {
            try { File.Delete(testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task SelectAsync_WithAnonymousType_ReturnsProjectedFields()
    {
        // Arrange
        var testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();

        using (var connection = connectionFactory.CreateConnection(options))
        {
            var store = new DocumentStore(connection);
            await store.CreateTableAsync<TestCustomer>();

            // Insert test data
            await store.UpsertAsync("1", new TestCustomer
            {
                Id = "1",
                Name = "John Doe",
                Email = "john@example.com",
                Age = 30,
                Active = true
            });

            // Act - Select using anonymous type
            var results = await store.SelectAsync<TestCustomer, dynamic>(
                x => new { x.Name, x.Email });

            // Assert
            var resultList = results.ToList();
            Assert.Single(resultList);
            Assert.Equal("John Doe", resultList[0].Name);
            Assert.Equal("john@example.com", resultList[0].Email);
        }

        // Cleanup
        if (File.Exists(testDbPath))
        {
            try { File.Delete(testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task SelectAsync_WithNullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new DocumentStoreOptions { ConnectionString = $"Data Source={_testDbPath}" };
        var connectionFactory = new DefaultConnectionFactory();

        using (var connection = connectionFactory.CreateConnection(options))
        {
            var store = new DocumentStore(connection);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await store.SelectAsync<TestCustomer, CustomerProjection>(null!));
        }

        // Cleanup
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}
