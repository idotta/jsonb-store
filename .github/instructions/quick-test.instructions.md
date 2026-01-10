# .NET 10 Quick Testing Instructions

## Overview

You can leverage .NET 10's simplified execution model to quickly validate implementations without creating full project structures. The `dotnet run` command can execute single C# files directly, making it ideal for rapid testing during development.

## Basic Usage

```bash
dotnet run app.cs
```

This command compiles and executes a standalone C# file immediately, perfect for:
- Validating algorithm implementations
- Testing specific methods or classes
- Verifying edge cases
- Quick proof-of-concept code
- Checking API behavior

## Quick Test File Structure

### Minimal Example

```csharp
// test.cs
Console.WriteLine("Hello from quick test!");
```

Run with: `dotnet run test.cs`

### Testing a Class Implementation

```csharp
// validate-user.cs
public class User
{
    public string Name { get; set; }
    public int Age { get; set; }
    
    public bool IsAdult() => Age >= 18;
}

// Quick test
var user1 = new User { Name = "Alice", Age = 25 };
var user2 = new User { Name = "Bob", Age = 16 };

Console.WriteLine($"{user1.Name} is adult: {user1.IsAdult()}"); // Should be True
Console.WriteLine($"{user2.Name} is adult: {user2.IsAdult()}"); // Should be False
```

### Testing with Multiple Scenarios

```csharp
// test-calculator.cs
public static class Calculator
{
    public static int Add(int a, int b) => a + b;
    public static int Multiply(int a, int b) => a * b;
}

// Test scenarios
var tests = new[]
{
    (a: 2, b: 3, expected: 5, operation: "Add"),
    (a: -1, b: 5, expected: 4, operation: "Add"),
    (a: 0, b: 10, expected: 0, operation: "Multiply"),
    (a: 5, b: 5, expected: 25, operation: "Multiply")
};

foreach (var test in tests)
{
    var result = test.operation == "Add" 
        ? Calculator.Add(test.a, test.b)
        : Calculator.Multiply(test.a, test.b);
    
    var status = result == test.expected ? "✓ PASS" : "✗ FAIL";
    Console.WriteLine($"{status}: {test.operation}({test.a}, {test.b}) = {result} (expected: {test.expected})");
}
```

## When to Create Quick Test Files

### ✅ Good Use Cases

1. **Algorithm Validation**: Test sorting, searching, or mathematical operations
2. **Edge Case Verification**: Validate null handling, empty collections, boundary values
3. **API Method Testing**: Check if methods return expected results
4. **Data Structure Behavior**: Verify custom collections or data structures
5. **String Manipulation**: Test parsing, formatting, or regex patterns
6. **Date/Time Logic**: Validate date calculations or timezone handling

### ❌ Not Ideal For

1. Full integration tests (use proper test projects)
2. Tests requiring external dependencies or databases
3. Tests needing complex setup or teardown
4. Performance benchmarking (use BenchmarkDotNet)
5. Tests that should persist in version control

## Advanced Testing Patterns

### Testing with Assertions

```csharp
// test-with-assertions.cs
public static void Assert(bool condition, string message)
{
    if (!condition)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"✗ FAILED: {message}");
        Console.ResetColor();
        Environment.Exit(1);
    }
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"✓ PASSED: {message}");
    Console.ResetColor();
}

// Your implementation
public static int Fibonacci(int n)
{
    if (n <= 1) return n;
    return Fibonacci(n - 1) + Fibonacci(n - 2);
}

// Tests
Assert(Fibonacci(0) == 0, "Fibonacci(0) should be 0");
Assert(Fibonacci(1) == 1, "Fibonacci(1) should be 1");
Assert(Fibonacci(5) == 5, "Fibonacci(5) should be 5");
Assert(Fibonacci(10) == 55, "Fibonacci(10) should be 55");
```

### Testing Async Code

```csharp
// test-async.cs
public static async Task<string> FetchDataAsync()
{
    await Task.Delay(100); // Simulate async operation
    return "Data retrieved";
}

// Test
var result = await FetchDataAsync();
Console.WriteLine($"Result: {result}");
Console.WriteLine(result == "Data retrieved" ? "✓ Test passed" : "✗ Test failed");
```

### Testing Exceptions

```csharp
// test-exceptions.cs
public static void ValidateAge(int age)
{
    if (age < 0)
        throw new ArgumentException("Age cannot be negative");
}

// Test expected exception
try
{
    ValidateAge(-5);
    Console.WriteLine("✗ FAIL: Should have thrown exception");
}
catch (ArgumentException)
{
    Console.WriteLine("✓ PASS: Exception thrown as expected");
}

// Test valid input
try
{
    ValidateAge(25);
    Console.WriteLine("✓ PASS: Valid age accepted");
}
catch
{
    Console.WriteLine("✗ FAIL: Should not throw for valid age");
}
```

## Best Practices

1. **Keep Tests Focused**: Each test file should validate one specific feature or scenario
2. **Use Descriptive Filenames**: Name files like `test-user-validation.cs` or `verify-sorting.cs`
3. **Include Expected Output**: Add comments showing what output to expect
4. **Clean Up**: Delete test files after verification, or move to proper test projects
5. **Add Visual Feedback**: Use console colors or symbols to make results clear

## Example Workflow

```bash
# 1. Create quick test during implementation
dotnet run test-login-logic.cs

# 2. Verify output matches expectations
# ✓ PASS: Valid credentials accepted
# ✓ PASS: Invalid credentials rejected
# ✓ PASS: Empty password rejected

# 3. If all tests pass, integrate into main implementation
# 4. Delete or archive the test file
```

## Tips for Effective Quick Testing

- **Start Simple**: Begin with the happy path, then add edge cases
- **Make Tests Obvious**: Use clear pass/fail indicators in output
- **Test Incrementally**: Create small test files for each piece of functionality
- **Document Assumptions**: Add comments explaining what you're testing and why
- **Exit Codes**: Return non-zero exit codes on failure for script integration

## Summary

The `dotnet run app.cs` approach provides a lightweight, fast way to validate code during development. Use it for quick checks and immediate feedback, then transition to proper test projects for long-term test maintenance.