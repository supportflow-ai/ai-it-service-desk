namespace ServiceDesk.Modules.Ticketing.Domain;

/// <summary>
/// Thời lượng mục tiêu SLA cho First Response và Resolution theo phút.
/// </summary>
public readonly record struct SlaTargetDuration(int FirstResponseMinutes, int ResolutionMinutes);

/// <summary>
/// Bảng quy định thời lượng SLA theo Priority (P1 đến P4) cho dự án:
/// - P1: Phản hồi 60 phút, Giải quyết 240 phút (4 giờ).
/// - P2: Phản hồi 120 phút (2 giờ), Giải quyết 480 phút (8 giờ).
/// - P3: Phản hồi 240 phút (4 giờ), Giải quyết 1.440 phút (24 giờ).
/// - P4: Phản hồi 480 phút (8 giờ), Giải quyết 4.320 phút (72 giờ).
/// </summary>
public static class PrioritySlaTargets
{
    public static SlaTargetDuration GetFor(Priority priority) => priority switch
    {
        Priority.P1 => new SlaTargetDuration(60, 240),
        Priority.P2 => new SlaTargetDuration(120, 480),
        Priority.P3 => new SlaTargetDuration(240, 1440),
        Priority.P4 => new SlaTargetDuration(480, 4320),
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Priority không hợp lệ.")
    };
}
