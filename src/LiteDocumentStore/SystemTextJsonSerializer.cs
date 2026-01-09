using System.Text.Json;

namespace LiteDocumentStore;

/// <summary>
/// Default implementation of <see cref="IJsonSerializer"/> using System.Text.Json.
/// </summary>
public class SystemTextJsonSerializer : IJsonSerializer
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
        return JsonSerializer.Serialize(value, _options);
    }

    /// <inheritdoc/>
    public T? Deserialize<T>(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, _options);
    }
}
