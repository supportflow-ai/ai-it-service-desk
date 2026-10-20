namespace ServiceDesk.Application.Common.Interfaces;

/// <summary>
/// Trừu tượng hóa thời gian hệ thống để phục vụ kiểm thử đơn vị và tính tất định.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Trả về thời gian UTC hiện tại.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
