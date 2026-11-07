using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.AIAssistance.Infrastructure;

/// <summary>Ngắt gọi trong 30 giây sau ba lỗi liên tiếp; dùng chung giữa các scope.</summary>
internal sealed class ClassificationCircuit(IClock clock)
{
    private readonly object _sync = new();
    private int _failures;
    private DateTimeOffset _retryAt;

    public bool IsOpen
    {
        get { lock (_sync) return clock.UtcNow < _retryAt; }
    }

    public void Record(bool success)
    {
        lock (_sync)
        {
            if (success) { _failures = 0; _retryAt = default; }
            else if (++_failures >= 3) _retryAt = clock.UtcNow.AddSeconds(30);
        }
    }
}
