using System.Data;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace LiteDocumentStore.Data;

/// <summary>
/// Holds the current ambient transaction for the async context.
/// Used to automatically associate commands with active transactions.
/// </summary>
internal static class AmbientTransaction
{
    private static readonly AsyncLocal<SqliteTransaction?> _current = new();

    /// <summary>
    /// Gets or sets the current ambient transaction.
    /// </summary>
    public static SqliteTransaction? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    /// <summary>
    /// Executes an action within a transaction scope.
    /// </summary>
    public static async Task<T> ExecuteInScopeAsync<T>(SqliteTransaction transaction, Func<Task<T>> action)
    {
        var previousTransaction = Current;
        try
        {
            Current = transaction;
            return await action().ConfigureAwait(false);
        }
        finally
        {
            Current = previousTransaction;
        }
    }

    /// <summary>
    /// Executes an action within a transaction scope.
    /// </summary>
    public static async Task ExecuteInScopeAsync(SqliteTransaction transaction, Func<Task> action)
    {
        var previousTransaction = Current;
        try
        {
            Current = transaction;
            await action().ConfigureAwait(false);
        }
        finally
        {
            Current = previousTransaction;
        }
    }
}

/// <summary>
/// Provides ADO.NET extension methods for SqliteConnection that replace Dapper functionality.
/// Offers a lightweight, zero-dependency alternative for database operations.
/// </summary>
internal static class AdoNetExtensions
{
    /// <summary>
    /// Executes a command that returns no results (INSERT, UPDATE, DELETE, DDL).
    /// </summary>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL command to execute</param>
    /// <param name="parameters">Optional anonymous object with parameters</param>
    /// <param name="transaction">Optional transaction</param>
    /// <returns>The number of rows affected</returns>
    public static async Task<int> ExecuteAsync(
        this SqliteConnection connection,
        string sql,
        object? parameters = null,
        IDbTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        // Auto-detect transaction if not provided
        command.Transaction = (transaction as SqliteTransaction) ?? GetActiveTransaction(connection);

        if (parameters != null)
        {
            AddParameters(command, parameters);
        }

        return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a command synchronously that returns no results (for backwards compatibility).
    /// </summary>
    public static int Execute(
        this SqliteConnection connection,
        string sql,
        object? parameters = null,
        IDbTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        // Auto-detect transaction if not provided
        command.Transaction = (transaction as SqliteTransaction) ?? GetActiveTransaction(connection);

        if (parameters != null)
        {
            AddParameters(command, parameters);
        }

        return command.ExecuteNonQuery();
    }

    /// <summary>
    /// Executes a query and returns all results as a list of typed objects.
    /// </summary>
    /// <typeparam name="T">The type to map results to</typeparam>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL query to execute</param>
    /// <param name="parameters">Optional anonymous object with parameters</param>
    /// <returns>An enumerable of results</returns>
    public static async Task<IEnumerable<T>> QueryAsync<T>(
        this SqliteConnection connection,
        string sql,
        object? parameters = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = GetActiveTransaction(connection);

        if (parameters != null)
        {
            AddParameters(command, parameters);
        }

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var results = new List<T>();

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            results.Add(MapRow<T>(reader));
        }

        return results;
    }

    /// <summary>
    /// Executes a query synchronously and returns all results (for backwards compatibility).
    /// </summary>
    public static IEnumerable<T> Query<T>(
        this SqliteConnection connection,
        string sql,
        object? parameters = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = GetActiveTransaction(connection);

        if (parameters != null)
        {
            AddParameters(command, parameters);
        }

        using var reader = command.ExecuteReader();
        var results = new List<T>();

        while (reader.Read())
        {
            results.Add(MapRow<T>(reader));
        }

        return results;
    }

    /// <summary>
    /// Executes a query and returns the first result or default.
    /// </summary>
    /// <typeparam name="T">The type to map the result to</typeparam>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL query to execute</param>
    /// <param name="parameters">Optional anonymous object with parameters</param>
    /// <returns>The first result or default(T)</returns>
    public static async Task<T?> QueryFirstOrDefaultAsync<T>(
        this SqliteConnection connection,
        string sql,
        object? parameters = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = GetActiveTransaction(connection);

        if (parameters != null)
        {
            AddParameters(command, parameters);
        }

        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return MapRow<T>(reader);
        }

        return default;
    }

    /// <summary>
    /// Executes a query and returns the first result (throws if no results).
    /// </summary>
    /// <typeparam name="T">The type to map the result to</typeparam>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL query to execute</param>
    /// <param name="parameters">Optional anonymous object with parameters</param>
    /// <returns>The first result</returns>
    /// <exception cref="InvalidOperationException">Thrown when no results are found</exception>
    public static async Task<T> QueryFirstAsync<T>(
        this SqliteConnection connection,
        string sql,
        object? parameters = null)
    {
        var result = await QueryFirstOrDefaultAsync<T>(connection, sql, parameters).ConfigureAwait(false);

        // Handle both reference types (null check) and value types (default comparison)
        if (EqualityComparer<T>.Default.Equals(result, default(T)))
        {
            throw new InvalidOperationException("Sequence contains no elements");
        }
        return result;
    }

    /// <summary>
    /// Executes a query synchronously and returns the first result or default.
    /// </summary>
    public static T? QueryFirstOrDefault<T>(
        this SqliteConnection connection,
        string sql,
        object? parameters = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = GetActiveTransaction(connection);

        if (parameters != null)
        {
            AddParameters(command, parameters);
        }

        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            return MapRow<T>(reader);
        }

        return default;
    }

    /// <summary>
    /// Executes a query and returns a single scalar value.
    /// </summary>
    /// <typeparam name="T">The type of the scalar value</typeparam>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL query to execute</param>
    /// <param name="parameters">Optional anonymous object with parameters</param>
    /// <returns>The scalar value</returns>
    public static async Task<T> ExecuteScalarAsync<T>(
        this SqliteConnection connection,
        string sql,
        object? parameters = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = GetActiveTransaction(connection);

        if (parameters != null)
        {
            AddParameters(command, parameters);
        }

        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return ConvertValue<T>(result);
    }

    /// <summary>
    /// Executes a query synchronously and returns a single scalar value.
    /// </summary>
    public static T ExecuteScalar<T>(
        this SqliteConnection connection,
        string sql,
        object? parameters = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = GetActiveTransaction(connection);

        if (parameters != null)
        {
            AddParameters(command, parameters);
        }

        var result = command.ExecuteScalar();
        return ConvertValue<T>(result);
    }

    /// <summary>
    /// Adds parameters from an anonymous object or dictionary to a command.
    /// Supports anonymous objects, DynamicParameters-like objects, and dictionaries.
    /// </summary>
    private static void AddParameters(SqliteCommand command, object parameters)
    {
        if (parameters is DynamicParameters dynamicParams)
        {
            // Handle our custom DynamicParameters type
            foreach (var (name, value) in dynamicParams.GetParameters())
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                SetParameterValue(parameter, value);
                command.Parameters.Add(parameter);
            }
        }
        else if (parameters is IDictionary<string, object> dict)
        {
            foreach (var kvp in dict)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = kvp.Key;
                SetParameterValue(parameter, kvp.Value);
                command.Parameters.Add(parameter);
            }
        }
        else
        {
            // Handle anonymous objects via reflection
            var properties = parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = prop.Name;
                var value = prop.GetValue(parameters);
                SetParameterValue(parameter, value);
                command.Parameters.Add(parameter);
            }
        }
    }

    /// <summary>
    /// Sets the parameter value with proper type handling for SQLite.
    /// Handles DateTimeOffset serialization and byte arrays for JSONB.
    /// </summary>
    private static void SetParameterValue(SqliteParameter parameter, object? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
        }
        else if (value is DateTimeOffset dto)
        {
            // Store DateTimeOffset as ISO 8601 string for reliable storage
            parameter.Value = dto.UtcDateTime.ToString("o");
            parameter.DbType = DbType.String;
        }
        else if (value is byte[] bytes)
        {
            // Handle JSONB binary data
            parameter.Value = bytes;
            parameter.DbType = DbType.Binary;
        }
        else
        {
            parameter.Value = value;
        }
    }

    /// <summary>
    /// Maps a data reader row to a typed object.
    /// Supports simple types (string, int, long, bool) and complex object mapping.
    /// </summary>
    private static T MapRow<T>(SqliteDataReader reader)
    {
        var type = typeof(T);

        // Handle simple types that map directly to a single column
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid))
        {
            return ConvertValue<T>(reader.GetValue(0));
        }

        // For complex types, map all columns to properties
        var obj = Activator.CreateInstance<T>();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var fieldName = reader.GetName(i);
            var property = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property != null && property.CanWrite)
            {
                var value = reader.GetValue(i);
                if (value != DBNull.Value)
                {
                    // Handle DateTimeOffset conversion
                    if (property.PropertyType == typeof(DateTimeOffset) && value is string strValue)
                    {
                        if (DateTimeOffset.TryParse(strValue, out var dto))
                        {
                            property.SetValue(obj, dto);
                        }
                    }
                    else
                    {
                        var convertedValue = Convert.ChangeType(value, Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
                        property.SetValue(obj, convertedValue);
                    }
                }
            }
        }

        return obj;
    }

    /// <summary>
    /// Converts a database value to the target type.
    /// </summary>
    private static T ConvertValue<T>(object? value)
    {
        if (value == null || value == DBNull.Value)
        {
            return default!;
        }

        var targetType = typeof(T);

        // Handle nullable types
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            targetType = Nullable.GetUnderlyingType(targetType)!;
        }

        // Handle DateTimeOffset
        if (targetType == typeof(DateTimeOffset) && value is string strValue)
        {
            if (DateTimeOffset.TryParse(strValue, out var dto))
            {
                return (T)(object)dto;
            }
        }

        // Handle boolean (SQLite stores as integer)
        if (targetType == typeof(bool) && value is long longValue)
        {
            return (T)(object)(longValue != 0);
        }

        // Direct cast for matching types
        if (value is T typedValue)
        {
            return typedValue;
        }

        // Convert for compatible types
        return (T)Convert.ChangeType(value, targetType);
    }

    /// <summary>
    /// Gets the active transaction on a connection, if any.
    /// First checks the ambient transaction, then falls back to null.
    /// </summary>
    private static SqliteTransaction? GetActiveTransaction(SqliteConnection connection)
    {
        // Check the ambient transaction first
        var ambient = AmbientTransaction.Current;
        if (ambient != null && ambient.Connection == connection)
        {
            return ambient;
        }

        return null;
    }
}

/// <summary>
/// A lightweight replacement for Dapper's DynamicParameters.
/// Allows building parameter collections dynamically for bulk operations.
/// </summary>
internal sealed class DynamicParameters
{
    private readonly Dictionary<string, object?> _parameters = new();

    /// <summary>
    /// Adds a parameter to the collection.
    /// </summary>
    /// <param name="name">Parameter name (without @ prefix)</param>
    /// <param name="value">Parameter value</param>
    public void Add(string name, object? value)
    {
        _parameters[name] = value;
    }

    /// <summary>
    /// Gets all parameters as key-value pairs.
    /// </summary>
    internal IEnumerable<(string name, object? value)> GetParameters()
    {
        foreach (var kvp in _parameters)
        {
            yield return (kvp.Key, kvp.Value);
        }
    }
}
