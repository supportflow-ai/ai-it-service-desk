using ServiceDesk.Application.Common.Interfaces;

namespace ServiceDesk.Infrastructure.Clock;

/// <summary>
/// Cài đặt đồng hồ hệ thống thực tế trả về thời gian UTC (DateTimeOffset.UtcNow).
/// </summary>
internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
