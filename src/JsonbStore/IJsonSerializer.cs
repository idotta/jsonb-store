namespace JsonbStore;

/// <summary>
/// Defines the contract for pluggable JSON serialization.
/// Allows users to choose between System.Text.Json, Newtonsoft.Json, or custom serializers.
/// </summary>
public interface IJsonSerializer
{
    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize</typeparam>
    /// <param name="value">The object to serialize</param>
    /// <returns>A JSON string representation of the object</returns>
    string Serialize<T>(T value);

    /// <summary>
    /// Deserializes a JSON string to an object.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize to</typeparam>
    /// <param name="json">The JSON string to deserialize</param>
    /// <returns>The deserialized object, or default if the JSON is null or empty</returns>
    T? Deserialize<T>(string json);
}
