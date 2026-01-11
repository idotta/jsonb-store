using Dapper;
using System.Data;

namespace LiteDocumentStore;

/// <summary>
/// A Dapper TypeHandler that automatically serializes and deserializes JSON objects
/// to/from SQLite JSONB columns using the optimized JsonHelper.
/// </summary>
/// <typeparam name="T">The type of object to serialize/deserialize</typeparam>
public sealed class SqliteJsonbTypeHandler<T> : SqlMapper.TypeHandler<T>
{
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
            parameter.Value = JsonHelper.SerializeToUtf8Bytes(value);
        }
        parameter.DbType = DbType.Binary;
    }

    /// <summary>
    /// Parses JSONB from the database into a typed object.
    /// </summary>
    /// <param name="value">The JSONB value from the database</param>
    /// <returns>The deserialized object</returns>
    public override T? Parse(object value)
    {
        return value switch
        {
            null or DBNull => default,
            byte[] bytes => JsonHelper.Deserialize<T>(bytes),
            string json => JsonHelper.Deserialize<T>(json),
            _ => throw new InvalidOperationException($"Unsupported JSON value type: {value.GetType().Name}")
        };
    }
}