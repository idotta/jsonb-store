namespace LiteDocumentStore.Exceptions;

/// <summary>
/// Exception thrown when a concurrency conflict occurs during a database operation.
/// This typically happens when optimistic concurrency control detects that a record was modified
/// by another process between when it was read and when an update was attempted.
/// </summary>
public class ConcurrencyException : LiteDocumentStoreException
{
    /// <summary>
    /// Gets the ID of the document that encountered the concurrency conflict.
    /// </summary>
    public string? DocumentId { get; }

    /// <summary>
    /// Gets the table name where the concurrency conflict occurred.
    /// </summary>
    public string? TableName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ConcurrencyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyException"/> class with document and table information.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="documentId">The ID of the document that encountered the concurrency conflict.</param>
    /// <param name="tableName">The table name where the concurrency conflict occurred.</param>
    public ConcurrencyException(string message, string? documentId, string? tableName)
        : base(message)
    {
        DocumentId = documentId;
        TableName = tableName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyException"/> class with document, table information, and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="documentId">The ID of the document that encountered the concurrency conflict.</param>
    /// <param name="tableName">The table name where the concurrency conflict occurred.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ConcurrencyException(string message, string? documentId, string? tableName, Exception innerException)
        : base(message, innerException)
    {
        DocumentId = documentId;
        TableName = tableName;
    }
}
