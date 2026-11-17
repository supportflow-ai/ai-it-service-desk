using System.Globalization;
using System.Text.RegularExpressions;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.SharedKernel.Application.Abstractions;

namespace ServiceDesk.Modules.Ticketing.Application.GetTicketAnalytics;

/// <summary>
/// Xử lý truy vấn báo cáo thống kê phân tích SLA và hiệu suất ticket:
/// - Xác thực quyền nhân viên (Agent hoặc Admin).
/// - Xác thực tham số khoảng thời gian (from, to: ISO-8601 có timezone, from &lt; to, tối đa 90 ngày).
/// - Tính toán chỉ số tuân thủ First Response, Resolution, và thời gian xử lý trung bình.
/// </summary>
internal sealed class GetTicketAnalyticsUseCase(
    ITicketAnalyticsQuery queryGateway,
    ICurrentUser currentUser,
    IClock? clock = null) : IGetTicketAnalytics
{
    private static readonly TimeSpan MaxRangeDuration = TimeSpan.FromDays(90);

    // Regex kiểm tra timestamp ISO-8601 có thành phần thời gian (T) và múi giờ tường minh (Z hoặc offset +/-HH:mm)
    private static readonly Regex ExplicitTimezoneRegex = new(
        @"^.+T.+(Z|[+-]\d{2}(:?\d{2})?)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task<TicketAnalyticsDto> ExecuteAsync(GetTicketAnalyticsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!TicketPermissions.IsStaff(currentUser))
        {
            throw new UnauthorizedAccessException("Only agents or admins can access analytics.");
        }

        if (string.IsNullOrWhiteSpace(query.From) || string.IsNullOrWhiteSpace(query.To))
        {
            throw new ArgumentException("both from and to are required");
        }

        var fromUtc = ParseAndNormalizeIsoTimestamp(query.From);
        var toUtc = ParseAndNormalizeIsoTimestamp(query.To);

        if (fromUtc >= toUtc)
        {
            throw new ArgumentException("from must be earlier than to");
        }

        if (toUtc - fromUtc > MaxRangeDuration)
        {
            throw new ArgumentException("date range cannot exceed 90 days");
        }

        var isAdmin = TicketPermissions.IsAdmin(currentUser);
        var actorId = TicketActor.RequireId(currentUser);
        var evaluatedAt = clock?.UtcNow.UtcDateTime ?? DateTime.UtcNow;

        var data = await queryGateway.QueryAnalyticsAsync(
            fromUtc,
            toUtc,
            evaluatedAt,
            isAdmin,
            actorId,
            cancellationToken);

        // Tính toán tỷ lệ tuân thủ First Response
        var frDenominator = data.FirstResponseAchieved + data.FirstResponseBreached;
        double? frRate = frDenominator > 0
            ? Math.Round((double)data.FirstResponseAchieved / frDenominator * 100, 2, MidpointRounding.AwayFromZero)
            : null;

        // Tính toán tỷ lệ tuân thủ Resolution
        var resDenominator = data.ResolutionAchieved + data.ResolutionBreached;
        double? resRate = resDenominator > 0
            ? Math.Round((double)data.ResolutionAchieved / resDenominator * 100, 2, MidpointRounding.AwayFromZero)
            : null;

        // Tính toán thời gian giải quyết trung bình
        double? avgResolution = data.ResolutionEligibleCount > 0
            ? Math.Round(data.ResolutionTotalMinutes / data.ResolutionEligibleCount, 2, MidpointRounding.AwayFromZero)
            : null;

        return new TicketAnalyticsDto(
            Range: new DateRangeDto(fromUtc, toUtc),
            EvaluatedAt: evaluatedAt,
            Volume: data.Volume,
            StatusDistribution: data.StatusDistribution,
            PriorityDistribution: data.PriorityDistribution,
            FirstResponseCompliance: new SlaComplianceMetricDto(
                Achieved: data.FirstResponseAchieved,
                Breached: data.FirstResponseBreached,
                Rate: frRate),
            ResolutionCompliance: new SlaComplianceMetricDto(
                Achieved: data.ResolutionAchieved,
                Breached: data.ResolutionBreached,
                Rate: resRate),
            AverageResolutionMinutes: avgResolution,
            ResolutionEligibleCount: data.ResolutionEligibleCount
        );
    }

    private static DateTime ParseAndNormalizeIsoTimestamp(string input)
    {
        var trimmed = input.Trim();
        if (!ExplicitTimezoneRegex.IsMatch(trimmed))
        {
            throw new ArgumentException("from and to must be ISO-8601 timestamp with explicit timezone (e.g. Z or +07:00)");
        }

        if (!DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
        {
            throw new ArgumentException("from and to must be ISO-8601 timestamp with explicit timezone (e.g. Z or +07:00)");
        }

        return dto.UtcDateTime;
    }
}
