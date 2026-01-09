namespace LiteDocumentStore;

/// <summary>
/// Represents a record of an applied migration in the migration history table.
/// </summary>
public sealed class MigrationHistoryRecord
{
    /// <summary>
    /// Gets or sets the migration version identifier.
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// Gets or sets the descriptive name of the migration.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the migration was applied.
    /// </summary>
    public DateTimeOffset AppliedAt { get; set; }
}
