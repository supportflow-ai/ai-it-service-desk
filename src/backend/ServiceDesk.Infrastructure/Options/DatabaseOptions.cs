namespace ServiceDesk.Infrastructure.Options;

/// <summary>
/// Strongly typed database connection options.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;
}
