namespace ServiceDesk.Infrastructure.Options;

/// <summary>
/// Các tùy chọn cấu hình kết nối cơ sở dữ liệu PostgreSQL.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Chuỗi kết nối tới cơ sở dữ liệu PostgreSQL.</summary>
    public string ConnectionString { get; set; } = string.Empty;
}
