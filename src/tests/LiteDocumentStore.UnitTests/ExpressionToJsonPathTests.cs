using System.Linq.Expressions;
using Xunit;

namespace LiteDocumentStore.UnitTests;

public class ExpressionToJsonPathTests
{
    private class TestModel
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public int Age { get; set; }
        public Address Address { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }

    private class Address
    {
        public string Street { get; set; } = "";
        public string City { get; set; } = "";
        public string ZipCode { get; set; } = "";
    }

    [Fact]
    public void Translate_SimpleProperty_ReturnsCorrectJsonPath()
    {
        // Arrange
        Expression<Func<TestModel, object>> expr = x => x.Name;

        // Act
        var result = ExpressionToJsonPath.Translate(expr);

        // Assert
        Assert.Equal("$.Name", result);
    }

    [Fact]
    public void Translate_NestedProperty_ReturnsCorrectJsonPath()
    {
        // Arrange
        Expression<Func<TestModel, object>> expr = x => x.Address.City;

        // Act
        var result = ExpressionToJsonPath.Translate(expr);

        // Assert
        Assert.Equal("$.Address.City", result);
    }

    [Fact]
    public void Translate_DeeplyNestedProperty_ReturnsCorrectJsonPath()
    {
        // Arrange
        Expression<Func<TestModel, object>> expr = x => x.Address.ZipCode;

        // Act
        var result = ExpressionToJsonPath.Translate(expr);

        // Assert
        Assert.Equal("$.Address.ZipCode", result);
    }

    [Fact]
    public void Translate_ArrayIndexer_ReturnsCorrectJsonPath()
    {
        // Arrange
        Expression<Func<TestModel, object>> expr = x => x.Tags[0];

        // Act
        var result = ExpressionToJsonPath.Translate(expr);

        // Assert
        Assert.Equal("$.Tags[0]", result);
    }

    [Fact]
    public void Translate_ArrayIndexerWithVariable_ReturnsCorrectJsonPath()
    {
        // Arrange
        int index = 2;
        Expression<Func<TestModel, object>> expr = x => x.Tags[index];

        // Act
        var result = ExpressionToJsonPath.Translate(expr);

        // Assert
        Assert.Equal("$.Tags[2]", result);
    }

    [Fact]
    public void Translate_NullExpression_ThrowsArgumentNullException()
    {
        // Arrange
        Expression<Func<TestModel, object>> expr = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ExpressionToJsonPath.Translate(expr));
    }

    [Fact]
    public void TranslatePredicate_SimpleEquality_ReturnsCorrectWhereClause()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Name == "John";

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate);

        // Assert
        Assert.Equal("json_extract(data, '$.Name') = @p0", whereClause);
        Assert.True(parameters.ContainsKey("p0"));
        Assert.Equal("John", parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_Inequality_ReturnsCorrectWhereClause()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Age != 30;

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate);

        // Assert
        Assert.Equal("json_extract(data, '$.Age') != @p0", whereClause);
        Assert.True(parameters.ContainsKey("p0"));
        Assert.Equal(30, parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_GreaterThan_ReturnsCorrectWhereClause()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Age > 18;

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate);

        // Assert
        Assert.Equal("json_extract(data, '$.Age') > @p0", whereClause);
        Assert.True(parameters.ContainsKey("p0"));
        Assert.Equal(18, parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_LessThanOrEqual_ReturnsCorrectWhereClause()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Age <= 65;

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate);

        // Assert
        Assert.Equal("json_extract(data, '$.Age') <= @p0", whereClause);
        Assert.True(parameters.ContainsKey("p0"));
        Assert.Equal(65, parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_AndCondition_ReturnsCorrectWhereClause()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Age > 18 && x.Name == "John";

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate);

        // Assert
        Assert.Equal("(json_extract(data, '$.Age') > @p0 AND json_extract(data, '$.Name') = @p1)", whereClause);
        Assert.Equal(2, parameters.Count);
        Assert.Equal(18, parameters["p0"]);
        Assert.Equal("John", parameters["p1"]);
    }

    [Fact]
    public void TranslatePredicate_OrCondition_ReturnsCorrectWhereClause()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Name == "John" || x.Name == "Jane";

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate);

        // Assert
        Assert.Equal("(json_extract(data, '$.Name') = @p0 OR json_extract(data, '$.Name') = @p1)", whereClause);
        Assert.Equal(2, parameters.Count);
        Assert.Equal("John", parameters["p0"]);
        Assert.Equal("Jane", parameters["p1"]);
    }

    [Fact]
    public void TranslatePredicate_NestedProperty_ReturnsCorrectWhereClause()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Address.City == "New York";

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate);

        // Assert
        Assert.Equal("json_extract(data, '$.Address.City') = @p0", whereClause);
        Assert.True(parameters.ContainsKey("p0"));
        Assert.Equal("New York", parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_StringContains_ReturnsCorrectWhereClause()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Email.Contains("@example.com");

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate);

        // Assert
        Assert.Equal("json_extract(data, '$.Email') LIKE @p0", whereClause);
        Assert.True(parameters.ContainsKey("p0"));
        Assert.Equal("%@example.com%", parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_StringStartsWith_ReturnsCorrectWhereClause()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Name.StartsWith("J");

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate);

        // Assert
        Assert.Equal("json_extract(data, '$.Name') LIKE @p0", whereClause);
        Assert.True(parameters.ContainsKey("p0"));
        Assert.Equal("J%", parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_StringEndsWith_ReturnsCorrectWhereClause()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Email.EndsWith(".com");

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate);

        // Assert
        Assert.Equal("json_extract(data, '$.Email') LIKE @p0", whereClause);
        Assert.True(parameters.ContainsKey("p0"));
        Assert.Equal("%.com", parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_WithClosureVariable_ReturnsCorrectWhereClause()
    {
        // Arrange
        string searchName = "Alice";
        Expression<Func<TestModel, bool>> predicate = x => x.Name == searchName;

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate);

        // Assert
        Assert.Equal("json_extract(data, '$.Name') = @p0", whereClause);
        Assert.True(parameters.ContainsKey("p0"));
        Assert.Equal("Alice", parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_ComplexCondition_ReturnsCorrectWhereClause()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x =>
            (x.Age > 18 && x.Age < 65) || x.Name == "Admin";

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate);

        // Assert
        Assert.Equal("((json_extract(data, '$.Age') > @p0 AND json_extract(data, '$.Age') < @p1) OR json_extract(data, '$.Name') = @p2)", whereClause);
        Assert.Equal(3, parameters.Count);
        Assert.Equal(18, parameters["p0"]);
        Assert.Equal(65, parameters["p1"]);
        Assert.Equal("Admin", parameters["p2"]);
    }

    [Fact]
    public void TranslatePredicate_NullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ExpressionToJsonPath.TranslatePredicate(predicate));
    }
}

