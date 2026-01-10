# Testing Instructions

## Testing Framework

- **xUnit** - Test framework
- **Moq** - Mocking library (if needed)

## Test Project Structure

```
tests/
├── LiteDocumentStore.UnitTests/           # Fast, isolated tests
│   └── RepositoryTests.cs
└── LiteDocumentStore.IntegrationTests/    # Real database tests
    └── RepositoryIntegrationTests.cs
```

## Unit Tests

Unit tests should be fast and isolated. Mock external dependencies.

```csharp
public class RepositoryTests
{
    [Fact]
    public void GetTableName_ReturnsTypeName()
    {
        // Arrange & Act
        var result = Repository.GetTableName<Customer>();
        
        // Assert
        result.Should().Be("Customer");
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetAsync_WithInvalidId_ThrowsArgumentException(string id)
    {
        // Arrange
        using var repo = new Repository(":memory:");
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => repo.GetAsync<Customer>(id));
    }
}
```

## Integration Tests

Integration tests use real SQLite databases. Prefer in-memory for speed.

```csharp
public class RepositoryIntegrationTests : IAsyncLifetime
{
    private Repository _repository = null!;
    
    public async Task InitializeAsync()
    {
        // Use in-memory database for speed
        _repository = new Repository(":memory:");
        await _repository.CreateTableAsync<Customer>();
    }
    
    public async Task DisposeAsync()
    {
        await _repository.DisposeAsync();
    }
    
    [Fact]
    public async Task UpsertAndGet_RoundTrip_PreservesData()
    {
        // Arrange
        var customer = new Customer { Name = "John", Email = "john@test.com" };
        
        // Act
        await _repository.UpsertAsync("cust-1", customer);
        var retrieved = await _repository.GetAsync<Customer>("cust-1");
        
        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("John");
        retrieved.Email.Should().Be("john@test.com");
    }
}
```

## Test Naming Convention

Use descriptive names following the pattern:
`MethodName_Scenario_ExpectedBehavior`

```csharp
[Fact]
public async Task GetAsync_WhenIdNotFound_ReturnsNull()

[Fact]
public async Task UpsertAsync_WithExistingId_UpdatesRecord()

[Fact]
public async Task DeleteAsync_WhenRecordExists_ReturnsTrue()
```

## Test Categories

Use traits to categorize tests:

```csharp
[Trait("Category", "Unit")]
public class RepositoryTests { }

[Trait("Category", "Integration")]
public class RepositoryIntegrationTests { }

[Trait("Category", "Performance")]
public class RepositoryBenchmarks { }
```

## What to Test

### Unit Tests
- Input validation
- Edge cases (null, empty, special characters)
- Error conditions
- Pure logic methods

### Integration Tests
- CRUD operations round-trip
- Transaction behavior
- Concurrent access patterns
- WAL mode behavior
- JSONB serialization/deserialization
- Index creation and usage
- Large dataset handling

## Test Data

Create test fixtures for reusable test data:

```csharp
public static class TestData
{
    public static Customer CreateCustomer(string id = "cust-1") => new()
    {
        Id = id,
        Name = "Test Customer",
        Email = "test@example.com",
        CreatedAt = DateTime.UtcNow
    };
    
    public static IEnumerable<Customer> CreateCustomers(int count) =>
        Enumerable.Range(1, count)
            .Select(i => CreateCustomer($"cust-{i}"));
}
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test --filter "Category=Unit"

# Run only integration tests
dotnet test --filter "Category=Integration"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```
