using System.Text.Json;
using LiteDocumentStore.Exceptions;

namespace LiteDocumentStore;

/// <summary>
/// Default implementation of <see cref="IJsonSerializer"/> using System.Text.Json.
/// </summary>
internal sealed class SystemTextJsonSerializer : IJsonSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of SystemTextJsonSerializer with default options.
    /// </summary>
    public SystemTextJsonSerializer()
        : this(new JsonSerializerOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of SystemTextJsonSerializer with custom options.
    /// </summary>
    /// <param name="options">The JSON serializer options to use</param>
    public SystemTextJsonSerializer(JsonSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Serialize<T>(T value)
    {
        try
        {
            return JsonSerializer.Serialize(value, _options);
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

    /// <inheritdoc/>
    public T? Deserialize<T>(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, _options);
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
