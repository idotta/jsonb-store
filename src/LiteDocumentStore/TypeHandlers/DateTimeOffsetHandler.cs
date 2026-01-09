using System.Data;
using Dapper;

namespace LiteDocumentStore;

/// <summary>
/// A Dapper TypeHandler that serializes and deserializes DateTimeOffset values
/// to/from ISO 8601 string format for reliable storage in SQLite.
/// </summary>
public sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    /// <summary>
    /// Parses a DateTimeOffset from its string representation in the database.
    /// </summary>
    /// <param name="value">The value from the database to parse.</param>
    /// <returns>The parsed DateTimeOffset.</returns>
    /// <exception cref="DataException">In case the value cannot be parsed as a DateTimeOffset.</exception>
    public override DateTimeOffset Parse(object value)
    {
        if (value is string strValue)
        {
            if (DateTimeOffset.TryParse(strValue, out var result))
            {
                return result;
            }
            throw new DataException($"Invalid DateTimeOffset value: {strValue}");
        }

        throw new DataException($"Unsupported DateTimeOffset value: {value.GetType()}");
    }

    /// <summary>
    /// Sets a DateTimeOffset value into the database parameter as an ISO 8601 string.
    /// </summary>
    /// <param name="parameter">The database parameter to set the value on.</param>
    /// <param name="value">The DateTimeOffset value to set.</param>
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        // Store as a TEXT string in ISO8601 format for reliable storage across time zones.
        parameter.Value = value.UtcDateTime.ToString("o"); // "o" is the round-trip format specifier (ISO 8601)
        parameter.DbType = DbType.String;
    }
}