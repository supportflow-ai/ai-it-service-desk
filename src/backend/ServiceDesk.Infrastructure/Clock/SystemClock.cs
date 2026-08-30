using ServiceDesk.Application.Common.Interfaces;

namespace ServiceDesk.Infrastructure.Clock;

/// <summary>
/// Production clock implementation returning real UTC time.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
