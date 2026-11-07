namespace ServiceDesk.Modules.Identity.Domain;

/// <summary>
/// Đại diện cho một phòng ban / bộ phận trong tổ chức (IT Helpdesk routing).
/// </summary>
public sealed class Department
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
