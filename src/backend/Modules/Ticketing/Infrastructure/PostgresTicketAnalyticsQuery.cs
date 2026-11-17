using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceDesk.Modules.Ticketing.Application.Ports;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

/// <summary>
/// Cài đặt truy vấn tổng hợp báo cáo phân tích SLA và khối lượng ticket trực tiếp trên PostgreSQL
/// bằng một snapshot transaction nhất quán, tính toán hoàn toàn trên database engine.
/// </summary>
internal sealed class PostgresTicketAnalyticsQuery(DbContext context) : ITicketAnalyticsQuery
{
    private const string SqlQuery = """
        SELECT
            COUNT(*)::int AS "Volume",
            COUNT(*) FILTER (WHERE status = 0)::int AS "StatusDraft",
            COUNT(*) FILTER (WHERE status = 1)::int AS "StatusSubmitted",
            COUNT(*) FILTER (WHERE status = 2)::int AS "StatusInProgress",
            COUNT(*) FILTER (WHERE status = 3)::int AS "StatusPendingUser",
            COUNT(*) FILTER (WHERE status = 4)::int AS "StatusResolved",
            COUNT(*) FILTER (WHERE status = 5)::int AS "StatusClosed",
            COUNT(*) FILTER (WHERE status = 6)::int AS "StatusCancelled",
            COUNT(*) FILTER (WHERE status = 7)::int AS "StatusTriaged",
            COUNT(*) FILTER (WHERE status = 8)::int AS "StatusAssigned",
            COUNT(*) FILTER (WHERE status = 9)::int AS "StatusPendingExternal",
            COUNT(*) FILTER (WHERE status NOT IN (0, 1, 2, 3, 4, 5, 6, 7, 8, 9))::int AS "StatusOther",
            COUNT(*) FILTER (WHERE priority = 0)::int AS "PriorityP1",
            COUNT(*) FILTER (WHERE priority = 1)::int AS "PriorityP2",
            COUNT(*) FILTER (WHERE priority = 2)::int AS "PriorityP3",
            COUNT(*) FILTER (WHERE priority = 3)::int AS "PriorityP4",
            COUNT(*) FILTER (WHERE priority IS NULL OR priority NOT IN (0, 1, 2, 3))::int AS "PriorityUnassigned",
            -- First Response Compliance
            COUNT(*) FILTER (WHERE is_sla_applicable
                AND first_responded_at IS NOT NULL
                AND NOT first_response_breached
                AND (first_responded_at - created_at) <= (first_response_target_minutes * INTERVAL '1 minute')
            )::int AS "FirstResponseAchieved",
            COUNT(*) FILTER (WHERE is_sla_applicable AND (
                first_response_breached
                OR (first_responded_at IS NOT NULL AND (first_responded_at - created_at) > (first_response_target_minutes * INTERVAL '1 minute'))
                OR (status = 6 AND first_responded_at IS NULL AND (updated_at - created_at) > (first_response_target_minutes * INTERVAL '1 minute'))
                OR (status NOT IN (5, 6) AND first_responded_at IS NULL AND (@evaluatedAt - created_at) > (first_response_target_minutes * INTERVAL '1 minute'))
            ))::int AS "FirstResponseBreached",
            -- Resolution Compliance
            COUNT(*) FILTER (WHERE is_sla_applicable
                AND status IN (4, 5)
                AND NOT resolution_breached
                AND resolution_accumulated_duration <= (resolution_target_minutes * INTERVAL '1 minute')
            )::int AS "ResolutionAchieved",
            COUNT(*) FILTER (WHERE is_sla_applicable AND (
                resolution_breached
                OR (status IN (4, 5) AND resolution_accumulated_duration > (resolution_target_minutes * INTERVAL '1 minute'))
                OR (status IN (3, 9) AND resolution_accumulated_duration > (resolution_target_minutes * INTERVAL '1 minute'))
                OR (status = 6 AND resolution_accumulated_duration > (resolution_target_minutes * INTERVAL '1 minute'))
                OR (resolution_segment_started_at IS NOT NULL AND status NOT IN (5, 6) AND (resolution_accumulated_duration + (@evaluatedAt - resolution_segment_started_at)) > (resolution_target_minutes * INTERVAL '1 minute'))
            ))::int AS "ResolutionBreached",
            -- Average Resolution Duration (chỉ tính ticket áp dụng SLA, hiện Resolved hoặc Closed)
            COUNT(*) FILTER (WHERE is_sla_applicable AND status IN (4, 5))::int AS "ResolutionEligibleCount",
            COALESCE(SUM(EXTRACT(EPOCH FROM resolution_accumulated_duration) / 60.0) FILTER (WHERE is_sla_applicable AND status IN (4, 5)), 0.0)::double precision AS "ResolutionTotalMinutes"
        FROM tickets t
        WHERE t.created_at >= @fromUtc AND t.created_at < @toUtc
          AND (
              @isAdmin
              OR t.assigned_to_id = @actorId
              OR (t.support_team_id IS NOT NULL AND t.support_team_id IN (
                  SELECT stm.team_id
                  FROM support_team_members stm
                  JOIN support_teams st ON st."Id" = stm.team_id
                  WHERE stm.user_id = @actorId AND st.is_active = TRUE
              ))
          );
        """;

    public async Task<TicketAnalyticsQueryResult> QueryAnalyticsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        DateTime evaluatedAt,
        bool isAdmin,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new object[]
        {
            new NpgsqlParameter("fromUtc", fromUtc),
            new NpgsqlParameter("toUtc", toUtc),
            new NpgsqlParameter("evaluatedAt", evaluatedAt),
            new NpgsqlParameter("isAdmin", isAdmin),
            new NpgsqlParameter("actorId", actorId)
        };

        var results = await context.Database.SqlQueryRaw<RawAnalyticsRow>(SqlQuery, parameters)
            .ToListAsync(cancellationToken);

        var row = results.FirstOrDefault() ?? new RawAnalyticsRow();

        var statusDist = new Dictionary<string, int>
        {
            ["Submitted"] = row.StatusSubmitted,
            ["Assigned"] = row.StatusAssigned,
            ["InProgress"] = row.StatusInProgress,
            ["PendingUser"] = row.StatusPendingUser,
            ["PendingExternal"] = row.StatusPendingExternal,
            ["Resolved"] = row.StatusResolved,
            ["Closed"] = row.StatusClosed,
            ["Cancelled"] = row.StatusCancelled,
            ["Triaged"] = row.StatusTriaged,
            ["Draft"] = row.StatusDraft
        };
        if (row.StatusOther > 0)
        {
            statusDist["Other"] = row.StatusOther;
        }

        var priorityDist = new Dictionary<string, int>
        {
            ["P1"] = row.PriorityP1,
            ["P2"] = row.PriorityP2,
            ["P3"] = row.PriorityP3,
            ["P4"] = row.PriorityP4
        };
        if (row.PriorityUnassigned > 0)
        {
            priorityDist["Unassigned"] = row.PriorityUnassigned;
        }

        return new TicketAnalyticsQueryResult(
            Volume: row.Volume,
            StatusDistribution: statusDist,
            PriorityDistribution: priorityDist,
            FirstResponseAchieved: row.FirstResponseAchieved,
            FirstResponseBreached: row.FirstResponseBreached,
            ResolutionAchieved: row.ResolutionAchieved,
            ResolutionBreached: row.ResolutionBreached,
            ResolutionEligibleCount: row.ResolutionEligibleCount,
            ResolutionTotalMinutes: row.ResolutionTotalMinutes
        );
    }

    public sealed class RawAnalyticsRow
    {
        public int Volume { get; set; }
        public int StatusDraft { get; set; }
        public int StatusSubmitted { get; set; }
        public int StatusInProgress { get; set; }
        public int StatusPendingUser { get; set; }
        public int StatusResolved { get; set; }
        public int StatusClosed { get; set; }
        public int StatusCancelled { get; set; }
        public int StatusTriaged { get; set; }
        public int StatusAssigned { get; set; }
        public int StatusPendingExternal { get; set; }
        public int StatusOther { get; set; }
        public int PriorityP1 { get; set; }
        public int PriorityP2 { get; set; }
        public int PriorityP3 { get; set; }
        public int PriorityP4 { get; set; }
        public int PriorityUnassigned { get; set; }
        public int FirstResponseAchieved { get; set; }
        public int FirstResponseBreached { get; set; }
        public int ResolutionAchieved { get; set; }
        public int ResolutionBreached { get; set; }
        public int ResolutionEligibleCount { get; set; }
        public double ResolutionTotalMinutes { get; set; }
    }
}
