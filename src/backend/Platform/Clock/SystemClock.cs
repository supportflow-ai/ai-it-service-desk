using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Platform.Clock;

/// <summary>
/// Cài đặt đồng hồ hệ thống thực tế trả về thời gian UTC (DateTimeOffset.UtcNow).
/// </summary>
internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
