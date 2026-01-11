using Dapper;
using LiteDocumentStore.Exceptions;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiteDocumentStore;

/// <summary>
/// A Dapper TypeHandler that automatically serializes and deserializes JSON objects
/// to/from SQLite JSONB columns.
/// </summary>
/// <typeparam name="T">The type of object to serialize/deserialize</typeparam>
public sealed class SqliteJsonbTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializes a typed object to JSONB for storage in the database.
    /// </summary>
    /// <param name="parameter">The database parameter to set</param>
    /// <param name="value">The value to serialize</param>
    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        if (value is null)
        {
            parameter.Value = DBNull.Value;
        }
        else
        {
            try
            {
                // SerializeToUtf8Bytes is faster than Serialize(string)
                parameter.Value = JsonSerializer.SerializeToUtf8Bytes(value, Options);
            }
            catch (JsonException ex)
            {
                throw new SerializationException(
                    $"Failed to serialize object of type {typeof(T).Name} to JSONB.",
                    typeof(T),
                    ex);
            }
            catch (NotSupportedException ex)
            {
                throw new SerializationException(
                    $"Serialization not supported for type {typeof(T).Name}.",
                    typeof(T),
                    ex);
            }
        }
        parameter.DbType = DbType.Binary;
    }

    /// <summary>
    /// Parses JSONB from the database into a typed object.
    /// </summary>
    /// <param name="value">The JSONB value from the database</param>
    /// <returns>The deserialized object</returns>
    /// <exception cref="SerializationException">Thrown when the JSON value cannot be parsed</exception>
    public override T? Parse(object value)
    {
        try
        {
            return value switch
            {
                null or DBNull => default,
                byte[] bytes => JsonSerializer.Deserialize<T>(bytes)!,
                string json => JsonSerializer.Deserialize<T>(json)!,
                _ => throw new SerializationException($"Unsupported JSON value type: {value.GetType().Name}")
            };
        }
        catch (JsonException ex)
        {
            throw new SerializationException(
                $"Failed to deserialize JSONB to type {typeof(T).Name}.",
                typeof(T),
                ex);
        }
    }
}