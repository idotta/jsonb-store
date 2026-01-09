namespace JsonbStore;

/// <summary>
/// Defines the contract for customizable table naming conventions.
/// Supports pluralization, snake_case, PascalCase, custom prefixes/suffixes, and more.
/// </summary>
public interface ITableNamingConvention
{
    /// <summary>
    /// Gets the table name for a given type.
    /// </summary>
    /// <typeparam name="T">The type to get the table name for</typeparam>
    /// <returns>The table name to use in SQL statements</returns>
    string GetTableName<T>();

    /// <summary>
    /// Gets the table name for a given type.
    /// </summary>
    /// <param name="type">The type to get the table name for</param>
    /// <returns>The table name to use in SQL statements</returns>
    string GetTableName(Type type);
}
