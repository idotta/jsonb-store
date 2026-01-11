using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace LiteDocumentStore;

/// <summary>
/// Translates LINQ expression trees to SQLite JSON path syntax for use with json_extract().
/// Supports simple property access, nested properties, and array indexing.
/// </summary>
internal static class ExpressionToJsonPath
{
    /// <summary>
    /// Converts a LINQ expression to a JSON path string.
    /// Supports patterns like: $.property, $.nested.property, $.array[0]
    /// </summary>
    /// <typeparam name="T">The type being queried</typeparam>
    /// <param name="expression">The expression to translate</param>
    /// <returns>A JSON path string starting with $.</returns>
    /// <exception cref="NotSupportedException">Thrown when the expression cannot be translated</exception>
    public static string Translate<T>(Expression<Func<T, object>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var body = expression.Body;

        // Handle boxing conversions (e.g., value types to object)
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            body = unary.Operand;
        }

        var path = new StringBuilder("$");
        BuildPath(body, path);
        return path.ToString();
    }

    /// <summary>
    /// Converts a predicate expression to SQL WHERE conditions and extracts parameter values.
    /// Supports equality comparisons on JSON properties.
    /// When virtual columns are provided, generates optimized SQL using column references instead of json_extract.
    /// </summary>
    /// <typeparam name="T">The type being queried</typeparam>
    /// <param name="predicate">The predicate expression to translate</param>
    /// <param name="virtualColumns">Optional dictionary of virtual columns keyed by JSON path</param>
    /// <returns>A tuple containing the WHERE clause and a dictionary of parameter values</returns>
    /// <exception cref="NotSupportedException">Thrown when the predicate cannot be translated</exception>
    public static (string whereClause, Dictionary<string, object> parameters) TranslatePredicate<T>(
        Expression<Func<T, bool>> predicate,
        IReadOnlyDictionary<string, VirtualColumnInfo>? virtualColumns = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var parameters = new Dictionary<string, object>();
        var whereClause = BuildWhereClause(predicate.Body, parameters, virtualColumns);

        return (whereClause, parameters);
    }

    private static void BuildPath(Expression expression, StringBuilder path)
    {
        switch (expression)
        {
            case MemberExpression memberExpr:
                // Recursively build the path for nested properties
                if (memberExpr.Expression != null && memberExpr.Expression.NodeType != ExpressionType.Parameter)
                {
                    BuildPath(memberExpr.Expression, path);
                }
                // Use the property name as-is (PascalCase) to match System.Text.Json default behavior
                path.Append('.').Append(memberExpr.Member.Name);
                break;

            case MethodCallExpression methodCall when IsIndexerAccess(methodCall):
                // Handle array/list indexing: list[0]
                BuildPath(methodCall.Object!, path);
                var index = GetIndexValue(methodCall.Arguments[0]);
                path.Append('[').Append(index).Append(']');
                break;

            case BinaryExpression binaryExpr when binaryExpr.NodeType == ExpressionType.ArrayIndex:
                // Handle array indexing: array[0]
                BuildPath(binaryExpr.Left, path);
                var arrayIndex = GetIndexValue(binaryExpr.Right);
                path.Append('[').Append(arrayIndex).Append(']');
                break;

            case ParameterExpression:
                // Base case - do nothing, we already have "$"
                break;

            default:
                throw new NotSupportedException(
                    $"Expression type '{expression.NodeType}' is not supported for JSON path translation. " +
                    $"Supported patterns: $.property, $.nested.property, $.array[0]");
        }
    }

    private static string BuildWhereClause(
        Expression expression,
        Dictionary<string, object> parameters,
        IReadOnlyDictionary<string, VirtualColumnInfo>? virtualColumns)
    {
        switch (expression)
        {
            case BinaryExpression binary when binary.NodeType == ExpressionType.Equal:
                return BuildEqualityComparison(binary, parameters, virtualColumns);

            case BinaryExpression binary when binary.NodeType == ExpressionType.NotEqual:
                return BuildInequalityComparison(binary, parameters, virtualColumns);

            case BinaryExpression binary when binary.NodeType == ExpressionType.AndAlso:
                var left = BuildWhereClause(binary.Left, parameters, virtualColumns);
                var right = BuildWhereClause(binary.Right, parameters, virtualColumns);
                return $"({left} AND {right})";

            case BinaryExpression binary when binary.NodeType == ExpressionType.OrElse:
                var leftOr = BuildWhereClause(binary.Left, parameters, virtualColumns);
                var rightOr = BuildWhereClause(binary.Right, parameters, virtualColumns);
                return $"({leftOr} OR {rightOr})";

            case BinaryExpression binary when IsComparisonOperator(binary.NodeType):
                return BuildComparisonOperator(binary, parameters, virtualColumns);

            case MethodCallExpression methodCall:
                return BuildMethodCallCondition(methodCall, parameters, virtualColumns);

            default:
                throw new NotSupportedException(
                    $"Expression type '{expression.NodeType}' is not supported for WHERE clause translation. " +
                    $"Supported: ==, !=, &&, ||, >, <, >=, <=, string methods");
        }
    }

    private static string BuildEqualityComparison(
        BinaryExpression binary,
        Dictionary<string, object> parameters,
        IReadOnlyDictionary<string, VirtualColumnInfo>? virtualColumns)
    {
        var (jsonPath, value) = ExtractComparisonParts(binary);
        var paramName = $"p{parameters.Count}";
        parameters[paramName] = value;

        var columnRef = GetColumnReference(jsonPath, virtualColumns);
        return $"{columnRef} = @{paramName}";
    }

    private static string BuildInequalityComparison(
        BinaryExpression binary,
        Dictionary<string, object> parameters,
        IReadOnlyDictionary<string, VirtualColumnInfo>? virtualColumns)
    {
        var (jsonPath, value) = ExtractComparisonParts(binary);
        var paramName = $"p{parameters.Count}";
        parameters[paramName] = value;

        var columnRef = GetColumnReference(jsonPath, virtualColumns);
        return $"{columnRef} != @{paramName}";
    }

    private static string BuildComparisonOperator(
        BinaryExpression binary,
        Dictionary<string, object> parameters,
        IReadOnlyDictionary<string, VirtualColumnInfo>? virtualColumns)
    {
        var (jsonPath, value) = ExtractComparisonParts(binary);
        var paramName = $"p{parameters.Count}";
        parameters[paramName] = value;

        var op = binary.NodeType switch
        {
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            _ => throw new NotSupportedException($"Comparison operator {binary.NodeType} not supported")
        };

        var columnRef = GetColumnReference(jsonPath, virtualColumns);
        return $"{columnRef} {op} @{paramName}";
    }

    private static string BuildMethodCallCondition(
        MethodCallExpression methodCall,
        Dictionary<string, object> parameters,
        IReadOnlyDictionary<string, VirtualColumnInfo>? virtualColumns)
    {
        // Support common string methods like Contains, StartsWith, EndsWith
        if (methodCall.Method.DeclaringType == typeof(string))
        {
            var jsonPath = TranslateToJsonPath(methodCall.Object!);
            var value = GetConstantValue(methodCall.Arguments[0]);
            var paramName = $"p{parameters.Count}";
            var columnRef = GetColumnReference(jsonPath, virtualColumns);

            switch (methodCall.Method.Name)
            {
                case "Contains":
                    parameters[paramName] = $"%{value}%";
                    return $"{columnRef} LIKE @{paramName}";

                case "StartsWith":
                    parameters[paramName] = $"{value}%";
                    return $"{columnRef} LIKE @{paramName}";

                case "EndsWith":
                    parameters[paramName] = $"%{value}";
                    return $"{columnRef} LIKE @{paramName}";

                default:
                    throw new NotSupportedException($"String method '{methodCall.Method.Name}' is not supported");
            }
        }

        throw new NotSupportedException($"Method '{methodCall.Method.Name}' is not supported");
    }

    /// <summary>
    /// Gets the SQL column reference for a JSON path.
    /// Returns the virtual column name if one exists, otherwise returns json_extract().
    /// </summary>
    private static string GetColumnReference(
        string jsonPath,
        IReadOnlyDictionary<string, VirtualColumnInfo>? virtualColumns)
    {
        if (virtualColumns != null && virtualColumns.TryGetValue(jsonPath, out var columnInfo))
        {
            return $"[{columnInfo.ColumnName}]";
        }

        return $"json_extract(data, '{jsonPath}')";
    }

    private static (string jsonPath, object value) ExtractComparisonParts(BinaryExpression binary)
    {
        // Determine which side is the member access and which is the constant
        Expression memberExpr;
        Expression valueExpr;

        if (IsMemberOrPropertyAccess(binary.Left))
        {
            memberExpr = binary.Left;
            valueExpr = binary.Right;
        }
        else if (IsMemberOrPropertyAccess(binary.Right))
        {
            memberExpr = binary.Right;
            valueExpr = binary.Left;
        }
        else
        {
            throw new NotSupportedException("At least one side of the comparison must be a property access");
        }

        var jsonPath = TranslateToJsonPath(memberExpr);
        var value = GetConstantValue(valueExpr);

        return (jsonPath, value);
    }

    private static string TranslateToJsonPath(Expression expression)
    {
        // Handle boxing conversions
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            expression = unary.Operand;
        }

        var path = new StringBuilder("$");
        BuildPath(expression, path);
        return path.ToString();
    }

    private static bool IsMemberOrPropertyAccess(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            expression = unary.Operand;
        }

        return expression is MemberExpression;
    }

    private static object GetConstantValue(Expression expression)
    {
        // Handle constant expressions
        if (expression is ConstantExpression constant)
        {
            return constant.Value ?? throw new InvalidOperationException("Constant value cannot be null");
        }

        // Handle member access on constants (e.g., variables in closure)
        if (expression is MemberExpression memberExpr && memberExpr.Expression is ConstantExpression constantExpr)
        {
            var member = memberExpr.Member;
            if (member is FieldInfo field)
            {
                return field.GetValue(constantExpr.Value)
                    ?? throw new InvalidOperationException($"Field '{field.Name}' value cannot be null");
            }
            if (member is PropertyInfo property)
            {
                return property.GetValue(constantExpr.Value)
                    ?? throw new InvalidOperationException($"Property '{property.Name}' value cannot be null");
            }
        }

        // Compile and execute the expression to get the value
        var lambda = Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object)));
        var compiled = lambda.Compile();
        return compiled() ?? throw new InvalidOperationException("Expression evaluated to null");
    }

    private static bool IsIndexerAccess(MethodCallExpression methodCall)
    {
        return methodCall.Method.Name == "get_Item" &&
               methodCall.Object != null &&
               methodCall.Arguments.Count == 1;
    }

    private static int GetIndexValue(Expression expression)
    {
        var value = GetConstantValue(expression);
        return value is int index ? index : throw new NotSupportedException("Array index must be an integer");
    }

    private static bool IsComparisonOperator(ExpressionType nodeType)
    {
        return nodeType is ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
            or ExpressionType.LessThan or ExpressionType.LessThanOrEqual;
    }

    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return propertyName;

        // Convert PascalCase to camelCase (FirstName -> firstName)
        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }

    /// <summary>
    /// Extracts field selections from a projection expression.
    /// Supports object initializer syntax: x => new DTO { Name = x.Name, Email = x.Email }
    /// </summary>
    /// <typeparam name="TSource">The source type</typeparam>
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="selector">The projection expression</param>
    /// <returns>Dictionary mapping result property names to JSON paths</returns>
    public static Dictionary<string, string> ExtractFieldSelections<TSource, TResult>(
        Expression<Func<TSource, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var body = selector.Body;

        // Handle boxing conversions
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            body = unary.Operand;
        }

        // Support MemberInitExpression: new DTO { Name = x.Name, Email = x.Email }
        if (body is MemberInitExpression memberInit)
        {
            return ExtractFromMemberInit(memberInit);
        }

        // Support NewExpression: new { x.Name, x.Email } (anonymous types)
        if (body is NewExpression newExpr)
        {
            return ExtractFromNewExpression(newExpr);
        }

        throw new NotSupportedException(
            "Only member initialization (new DTO { Name = x.Name }) and anonymous type creation (new { x.Name }) are supported for projection");
    }

    private static Dictionary<string, string> ExtractFromMemberInit(MemberInitExpression memberInit)
    {
        var selections = new Dictionary<string, string>();

        foreach (var binding in memberInit.Bindings)
        {
            if (binding is not MemberAssignment assignment)
            {
                throw new NotSupportedException($"Only member assignments are supported in projections");
            }

            var propertyName = assignment.Member.Name;
            var jsonPath = TranslateToJsonPath(assignment.Expression);
            selections[propertyName] = jsonPath;
        }

        return selections;
    }

    private static Dictionary<string, string> ExtractFromNewExpression(NewExpression newExpr)
    {
        var selections = new Dictionary<string, string>();

        // Anonymous types have both Arguments and Members
        if (newExpr.Members == null || newExpr.Members.Count != newExpr.Arguments.Count)
        {
            throw new NotSupportedException("Constructor expression must have matching members and arguments");
        }

        for (int i = 0; i < newExpr.Members.Count; i++)
        {
            var propertyName = newExpr.Members[i].Name;
            var jsonPath = TranslateToJsonPath(newExpr.Arguments[i]);
            selections[propertyName] = jsonPath;
        }

        return selections;
    }
}
