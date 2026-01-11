using System.Linq.Expressions;
using Xunit;

namespace LiteDocumentStore.UnitTests;

/// <summary>
/// Unit tests for virtual column translation in ExpressionToJsonPath.
/// Tests verify that when virtual columns are provided, the SQL generator
/// uses column references instead of json_extract() for optimal index usage.
/// </summary>
public class VirtualColumnTranslationTests
{
    private class TestModel
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public int Age { get; set; }
        public Address Address { get; set; } = new();
    }

    private class Address
    {
        public string Street { get; set; } = "";
        public string City { get; set; } = "";
    }

    #region Equality Comparisons

    [Fact]
    public void TranslatePredicate_WithVirtualColumn_UsesColumnReference()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Name == "John";
        var virtualColumns = new Dictionary<string, VirtualColumnInfo>
        {
            ["$.Name"] = new VirtualColumnInfo("$.Name", "name_vc", "TEXT")
        };

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, virtualColumns);

        // Assert
        Assert.Equal("[name_vc] = @p0", whereClause);
        Assert.True(parameters.ContainsKey("p0"));
        Assert.Equal("John", parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_WithVirtualColumn_NestedProperty_UsesColumnReference()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Address.City == "New York";
        var virtualColumns = new Dictionary<string, VirtualColumnInfo>
        {
            ["$.Address.City"] = new VirtualColumnInfo("$.Address.City", "city", "TEXT")
        };

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, virtualColumns);

        // Assert
        Assert.Equal("[city] = @p0", whereClause);
        Assert.Equal("New York", parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_WithVirtualColumn_InequalityOperator_UsesColumnReference()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Age != 30;
        var virtualColumns = new Dictionary<string, VirtualColumnInfo>
        {
            ["$.Age"] = new VirtualColumnInfo("$.Age", "age", "INTEGER")
        };

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, virtualColumns);

        // Assert
        Assert.Equal("[age] != @p0", whereClause);
        Assert.Equal(30, parameters["p0"]);
    }

    #endregion

    #region Comparison Operators

    [Fact]
    public void TranslatePredicate_WithVirtualColumn_GreaterThan_UsesColumnReference()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Age > 18;
        var virtualColumns = new Dictionary<string, VirtualColumnInfo>
        {
            ["$.Age"] = new VirtualColumnInfo("$.Age", "age", "INTEGER")
        };

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, virtualColumns);

        // Assert
        Assert.Equal("[age] > @p0", whereClause);
        Assert.Equal(18, parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_WithVirtualColumn_LessThanOrEqual_UsesColumnReference()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Age <= 65;
        var virtualColumns = new Dictionary<string, VirtualColumnInfo>
        {
            ["$.Age"] = new VirtualColumnInfo("$.Age", "age", "INTEGER")
        };

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, virtualColumns);

        // Assert
        Assert.Equal("[age] <= @p0", whereClause);
        Assert.Equal(65, parameters["p0"]);
    }

    #endregion

    #region String Methods

    [Fact]
    public void TranslatePredicate_WithVirtualColumn_StringContains_UsesColumnReference()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Email.Contains("@example");
        var virtualColumns = new Dictionary<string, VirtualColumnInfo>
        {
            ["$.Email"] = new VirtualColumnInfo("$.Email", "email", "TEXT")
        };

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, virtualColumns);

        // Assert
        Assert.Equal("[email] LIKE @p0", whereClause);
        Assert.Equal("%@example%", parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_WithVirtualColumn_StringStartsWith_UsesColumnReference()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Name.StartsWith("Jo");
        var virtualColumns = new Dictionary<string, VirtualColumnInfo>
        {
            ["$.Name"] = new VirtualColumnInfo("$.Name", "name_vc", "TEXT")
        };

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, virtualColumns);

        // Assert
        Assert.Equal("[name_vc] LIKE @p0", whereClause);
        Assert.Equal("Jo%", parameters["p0"]);
    }

    [Fact]
    public void TranslatePredicate_WithVirtualColumn_StringEndsWith_UsesColumnReference()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Email.EndsWith(".com");
        var virtualColumns = new Dictionary<string, VirtualColumnInfo>
        {
            ["$.Email"] = new VirtualColumnInfo("$.Email", "email", "TEXT")
        };

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, virtualColumns);

        // Assert
        Assert.Equal("[email] LIKE @p0", whereClause);
        Assert.Equal("%.com", parameters["p0"]);
    }

    #endregion

    #region Mixed Virtual and Non-Virtual

    [Fact]
    public void TranslatePredicate_MixedVirtualAndNonVirtual_UsesCorrectReferences()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Name == "John" && x.Age > 18;
        var virtualColumns = new Dictionary<string, VirtualColumnInfo>
        {
            // Only Name has a virtual column, Age does not
            ["$.Name"] = new VirtualColumnInfo("$.Name", "name_vc", "TEXT")
        };

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, virtualColumns);

        // Assert
        // Name uses virtual column, Age uses json_extract
        Assert.Equal("([name_vc] = @p0 AND json_extract(data, '$.Age') > @p1)", whereClause);
        Assert.Equal("John", parameters["p0"]);
        Assert.Equal(18, parameters["p1"]);
    }

    [Fact]
    public void TranslatePredicate_OrCondition_WithVirtualColumns_UsesCorrectReferences()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Name == "John" || x.Email == "jane@example.com";
        var virtualColumns = new Dictionary<string, VirtualColumnInfo>
        {
            ["$.Name"] = new VirtualColumnInfo("$.Name", "name_vc", "TEXT"),
            ["$.Email"] = new VirtualColumnInfo("$.Email", "email", "TEXT")
        };

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, virtualColumns);

        // Assert
        Assert.Equal("([name_vc] = @p0 OR [email] = @p1)", whereClause);
        Assert.Equal("John", parameters["p0"]);
        Assert.Equal("jane@example.com", parameters["p1"]);
    }

    #endregion

    #region Fallback Behavior

    [Fact]
    public void TranslatePredicate_WithNullVirtualColumns_UsesJsonExtract()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Name == "John";

        // Act - passing null explicitly
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, null);

        // Assert
        Assert.Equal("json_extract(data, '$.Name') = @p0", whereClause);
    }

    [Fact]
    public void TranslatePredicate_WithEmptyVirtualColumns_UsesJsonExtract()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Name == "John";
        var virtualColumns = new Dictionary<string, VirtualColumnInfo>();

        // Act
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate, virtualColumns);

        // Assert
        Assert.Equal("json_extract(data, '$.Name') = @p0", whereClause);
    }

    [Fact]
    public void TranslatePredicate_WithoutVirtualColumnsParam_UsesJsonExtract()
    {
        // Arrange
        Expression<Func<TestModel, bool>> predicate = x => x.Name == "John";

        // Act - not passing virtual columns at all (default parameter)
        var (whereClause, parameters) = ExpressionToJsonPath.TranslatePredicate(predicate);

        // Assert
        Assert.Equal("json_extract(data, '$.Name') = @p0", whereClause);
    }

    #endregion
}

/// <summary>
/// Unit tests for VirtualColumnInfo record.
/// </summary>
public class VirtualColumnInfoTests
{
    [Fact]
    public void VirtualColumnInfo_RecordEquality_Works()
    {
        // Arrange
        var info1 = new VirtualColumnInfo("$.Name", "name_vc", "TEXT");
        var info2 = new VirtualColumnInfo("$.Name", "name_vc", "TEXT");
        var info3 = new VirtualColumnInfo("$.Email", "email", "TEXT");

        // Assert
        Assert.Equal(info1, info2);
        Assert.NotEqual(info1, info3);
    }

    [Fact]
    public void VirtualColumnInfo_Properties_AreSet()
    {
        // Arrange & Act
        var info = new VirtualColumnInfo("$.Address.City", "city", "TEXT");

        // Assert
        Assert.Equal("$.Address.City", info.JsonPath);
        Assert.Equal("city", info.ColumnName);
        Assert.Equal("TEXT", info.ColumnType);
    }
}
