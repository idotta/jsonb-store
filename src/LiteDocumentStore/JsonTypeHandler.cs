using Dapper;
using System.Data;
using System.Text.Json;

namespace LiteDocumentStore;

/// <summary>
/// A Dapper TypeHandler that automatically serializes and deserializes JSON objects
/// to/from SQLite TEXT or BLOB columns.
/// </summary>
/// <typeparam name="T">The type of object to serialize/deserialize</typeparam>
public class JsonTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the JsonTypeHandler with optional JSON serializer options.
    /// </summary>
    /// <param name="options">Optional JSON serializer options</param>
    public JsonTypeHandler(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Parses JSON from the database into a typed object.
    /// </summary>
    public override T Parse(object value)
    {
        if (value == null || value is DBNull)
        {
            return default!;
        }

        string json;
        if (value is string stringValue)
        {
            json = stringValue;
        }
        else if (value is byte[] bytes)
        {
            json = System.Text.Encoding.UTF8.GetString(bytes);
        }
        else
        {
            throw new InvalidOperationException($"Cannot parse JSON from type {value.GetType()}");
        }

        return JsonSerializer.Deserialize<T>(json, _options)!;
    }

    /// <summary>
    /// Serializes a typed object to JSON for storage in the database.
    /// </summary>
    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        if (value == null)
        {
            parameter.Value = DBNull.Value;
        }
        else
        {
            var json = JsonSerializer.Serialize(value, _options);
            parameter.Value = json;
        }
    }
}
