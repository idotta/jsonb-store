namespace LiteDocumentStore.Exceptions;

/// <summary>
/// Exception thrown when a table operation is attempted on a table that does not exist.
/// </summary>
public class TableNotFoundException : LiteDocumentStoreException
{
    /// <summary>
    /// Gets the name of the table that was not found.
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableNotFoundException"/> class.
    /// </summary>
    /// <param name="tableName">The name of the table that was not found.</param>
    public TableNotFoundException(string tableName)
        : base($"Table '{tableName}' does not exist in the database.")
    {
        TableName = tableName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableNotFoundException"/> class with an inner exception.
    /// </summary>
    /// <param name="tableName">The name of the table that was not found.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public TableNotFoundException(string tableName, Exception innerException)
        : base($"Table '{tableName}' does not exist in the database.", innerException)
    {
        TableName = tableName;
    }
}
