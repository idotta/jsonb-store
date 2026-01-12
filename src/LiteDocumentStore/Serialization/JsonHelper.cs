using System.Text.Json;
using System.Text.Json.Serialization;
using LiteDocumentStore.Exceptions;

namespace LiteDocumentStore;

/// <summary>
/// Internal helper for high-performance JSON serialization optimized for SQLite JSONB storage.
/// Uses fixed, optimized JsonSerializerOptions for maximum performance.
/// </summary>
internal static class JsonHelper
{
    /// <summary>
    /// Optimized JSON serializer options for performance.
    /// - PropertyNameCaseInsensitive = false (exact match for speed)
    /// - DefaultIgnoreCondition = WhenWritingNull (smaller JSON)
    /// - WriteIndented = false (compact output)
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Serializes an object to UTF-8 encoded JSON bytes for JSONB storage.
    /// This is the most efficient path for SQLite JSONB operations.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize</typeparam>
    /// <param name="value">The object to serialize</param>
    /// <returns>UTF-8 encoded JSON bytes</returns>
    /// <exception cref="SerializationException">Thrown when serialization fails</exception>
    public static byte[] SerializeToUtf8Bytes<T>(T value)
    {
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(value, Options);
        }
        catch (JsonException ex)
        {
            throw new SerializationException(
                $"Failed to serialize object of type {typeof(T).Name}.",
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

    /// <summary>
    /// Deserializes UTF-8 encoded JSON bytes to a typed object.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize to</typeparam>
    /// <param name="utf8Json">The UTF-8 encoded JSON bytes</param>
    /// <returns>The deserialized object, or default if input is empty</returns>
    /// <exception cref="SerializationException">Thrown when deserialization fails</exception>
    public static T? Deserialize<T>(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(utf8Json, Options);
        }
        catch (JsonException ex)
        {
            throw new SerializationException(
                $"Failed to deserialize JSON to type {typeof(T).Name}.",
                typeof(T),
                ex);
        }
    }

    /// <summary>
    /// Deserializes a JSON string to a typed object.
    /// This overload handles string data from the database.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize to</typeparam>
    /// <param name="json">The JSON string</param>
    /// <returns>The deserialized object, or default if input is null or empty</returns>
    /// <exception cref="SerializationException">Thrown when deserialization fails</exception>
    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new SerializationException(
                $"Failed to deserialize JSON to type {typeof(T).Name}.",
                typeof(T),
                ex);
        }
    }
}
