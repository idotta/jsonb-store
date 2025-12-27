using Dapper;
using Xunit;

namespace JsonbStore.UnitTests;

public class RepositoryTests
{
    private readonly string _testDbPath;

    public RepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
    }

    [Fact]
    public void Constructor_WithDatabasePath_CreatesRepository()
    {
        // Arrange & Act
        using var repo = new Repository(_testDbPath);

        // Assert
        Assert.NotNull(repo);
        Assert.NotNull(repo.Connection);
        Assert.Equal(System.Data.ConnectionState.Open, repo.Connection.State);
        
        // Cleanup
        repo.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task GetTableName_ReturnsTypeName()
    {
        // This tests the private method indirectly through CreateTableAsync
        // We'll verify table creation works with the correct type name
        var testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        
        using (var repo = new Repository(testDbPath))
        {
            // Act - create table should use type name
            await repo.CreateTableAsync<TestPerson>();
            
            // Assert - verify table exists with correct name
            var checkSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='TestPerson'";
            var result = repo.Connection.QueryFirstOrDefault<string>(checkSql);
            Assert.Equal("TestPerson", result);
        }
        
        // Cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (File.Exists(testDbPath))
        {
            try { File.Delete(testDbPath); } catch { }
        }
    }

    [Fact]
    public void Dispose_DisposesConnection_WhenOwned()
    {
        // Arrange
        var repo = new Repository(_testDbPath);
        var connection = repo.Connection;

        // Act
        repo.Dispose();

        // Assert
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
        
        // Cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task DisposeAsync_DisposesConnection_WhenOwned()
    {
        // Arrange
        var repo = new Repository(_testDbPath);
        var connection = repo.Connection;

        // Act
        await repo.DisposeAsync();

        // Assert
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
        
        // Cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    // Helper class for testing
    public class TestPerson
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }
}
