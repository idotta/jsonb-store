using System.Data;

namespace LiteDocumentStore.Exceptions;

/// <summary>
/// Base exception class for all LiteDocumentStore-specific exceptions.
/// </summary>
public class LiteDocumentStoreException : DataException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiteDocumentStoreException"/> class.
    /// </summary>
    public LiteDocumentStoreException()
        : base("An error occurred in LiteDocumentStore.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LiteDocumentStoreException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public LiteDocumentStoreException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LiteDocumentStoreException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or null if no inner exception is specified.</param>
    public LiteDocumentStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
