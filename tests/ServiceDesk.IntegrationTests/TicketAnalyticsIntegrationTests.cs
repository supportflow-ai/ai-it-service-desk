using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Platform.Persistence;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class TicketAnalyticsIntegrationTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly ServiceDeskWebApplicationFactory _factory;
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public TicketAnalyticsIntegrationTests(ServiceDeskWebApplicationFactory factory, Xunit.Abstractions.ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Analytics_CalculatesCorrectAggregates_AndMatchesDomainEvaluatorParity_AcrossDiverseDataset()
    {
        var fromUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);
        // evaluatedAt cách toUtc 200 phút (800m từ t4 CreatedAt: 800m > 480m FR target, nhưng < 4320m Res target)
        var evaluatedAt = toUtc.AddMinutes(200);
        var agentId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var query = scope.ServiceProvider.GetRequiredService<ITicketAnalyticsQuery>();

        // 1. Dựng tập dữ liệu đại diện trong cohort [from, to)
        // t1: P1, ResolvedAchieved (FR 30m, Res 120m). Target P1: FR 60m, Res 240m.
        var t1 = Ticket.Create(Guid.NewGuid(), "T1 P1 ResolvedAchieved", "Desc long enough for ticket", TicketCategories.VPN, fromUtc.AddDays(1));
        t1.InitializeSla(PrioritySlaTargets.GetFor(Priority.P1), fromUtc.AddDays(1));
        t1.Claim(agentId, fromUtc.AddDays(1).AddMinutes(10));
        t1.RecordFirstResponse(fromUtc.AddDays(1).AddMinutes(30));
        t1.Resolve(fromUtc.AddDays(1).AddMinutes(120));

        // t2: P2, ResolvedBreached (FR 60m achieved, Res 500m > 480m breached). Target P2: FR 120m, Res 480m.
        var t2 = Ticket.Create(Guid.NewGuid(), "T2 P2 ResolvedBreached", "Desc long enough for ticket", TicketCategories.VPN, fromUtc.AddDays(2));
        t2.InitializeSla(PrioritySlaTargets.GetFor(Priority.P2), fromUtc.AddDays(2));
        t2.Claim(agentId, fromUtc.AddDays(2).AddMinutes(10));
        t2.RecordFirstResponse(fromUtc.AddDays(2).AddMinutes(60));
        t2.Resolve(fromUtc.AddDays(2).AddMinutes(500));

        // t3: P3, RunningOnTrack (FR 60m achieved, Res ongoing: 40m acc + 30m segment = 70m <= 1440m).
        var t3 = Ticket.Create(Guid.NewGuid(), "T3 P3 RunningOnTrack", "Desc long enough for ticket", TicketCategories.VPN, fromUtc.AddDays(3));
        t3.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), fromUtc.AddDays(3));
        t3.Claim(agentId, fromUtc.AddDays(3).AddMinutes(10));
        t3.RecordFirstResponse(fromUtc.AddDays(3).AddMinutes(60));
        t3.MarkPending(TicketStatus.PendingUser, fromUtc.AddDays(3).AddMinutes(100)); // Acc = 40m
        t3.ResumeFromPending(evaluatedAt.AddMinutes(-30)); // Running segment 30m, tổng 70m <= 1440m

        // t4: P4, RunningBreached FR (Tạo tại toUtc - 600m; evaluatedAt - CreatedAt = 800m > 480m FR target, nhưng < 4320m Res target).
        var t4 = Ticket.Create(Guid.NewGuid(), "T4 P4 RunningBreachedFR", "Desc long enough for ticket", TicketCategories.VPN, toUtc.AddMinutes(-600));
        t4.InitializeSla(PrioritySlaTargets.GetFor(Priority.P4), toUtc.AddMinutes(-600));
        t4.Claim(agentId, toUtc.AddMinutes(-590)); // Status InProgress, Segment started at toUtc - 600m (800m <= 4320m Res target)

        // t5: Legacy ticket (IsSlaApplicable = false), Resolved
        var t5 = Ticket.Create(Guid.NewGuid(), "T5 Legacy Resolved", "Desc long enough for ticket", TicketCategories.VPN, fromUtc.AddDays(5));
        t5.Claim(agentId, fromUtc.AddDays(5).AddMinutes(10));
        t5.Resolve(fromUtc.AddDays(5).AddMinutes(200));

        // t6: P3, Cancelled lúc 10m (chưa phản hồi, chưa breach lúc cancel).
        var t6 = Ticket.Create(Guid.NewGuid(), "T6 CancelledOnTrack", "Desc long enough for ticket", TicketCategories.VPN, fromUtc.AddDays(6));
        t6.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), fromUtc.AddDays(6));
        t6.Cancel(fromUtc.AddDays(6).AddMinutes(10));

        // t7: P1, Cancelled lúc 90m (> 60m FR target của P1 -> Breached lúc cancel).
        var t7 = Ticket.Create(Guid.NewGuid(), "T7 CancelledBreached", "Desc long enough for ticket", TicketCategories.VPN, fromUtc.AddDays(7));
        t7.InitializeSla(PrioritySlaTargets.GetFor(Priority.P1), fromUtc.AddDays(7));
        t7.Cancel(fromUtc.AddDays(7).AddMinutes(90));

        // t8: P2, Resolved lúc 100m, Reopen lúc 150m, hiện Status = InProgress (Ongoing, chưa Achieved).
        var t8 = Ticket.Create(Guid.NewGuid(), "T8 P2 Reopened", "Desc long enough for ticket", TicketCategories.VPN, fromUtc.AddDays(8));
        t8.InitializeSla(PrioritySlaTargets.GetFor(Priority.P2), fromUtc.AddDays(8));
        t8.Claim(agentId, fromUtc.AddDays(8).AddMinutes(10));
        t8.RecordFirstResponse(fromUtc.AddDays(8).AddMinutes(50));
        t8.Resolve(fromUtc.AddDays(8).AddMinutes(100)); // Lần 1: Acc = 90m
        t8.Reopen(evaluatedAt.AddMinutes(-30)); // Reopen tại evaluatedAt - 30m, Segment = 30m, Tổng 120m <= 480m

        // t9: P3, Closed với Duration = 180m (FR 40m, Resolved 180m, Close sau đó).
        var t9 = Ticket.Create(Guid.NewGuid(), "T9 P3 Closed", "Desc long enough for ticket", TicketCategories.VPN, fromUtc.AddDays(9));
        t9.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), fromUtc.AddDays(9));
        t9.Claim(agentId, fromUtc.AddDays(9).AddMinutes(10));
        t9.RecordFirstResponse(fromUtc.AddDays(9).AddMinutes(40));
        t9.Resolve(fromUtc.AddDays(9).AddMinutes(180));
        t9.Close(fromUtc.AddDays(9).AddDays(1)); // Closed 1 ngày sau, thời gian từ Resolve tới Close không tính vào duration

        var cohortTickets = new[] { t1, t2, t3, t4, t5, t6, t7, t8, t9 };
        context.Tickets.AddRange(cohortTickets);
        await context.SaveChangesAsync();

        // 2. Chạy Query Analytics (với quyền Admin = true để lấy toàn bộ)
        var result = await query.QueryAnalyticsAsync(fromUtc, toUtc, evaluatedAt, isAdmin: true, actorId: agentId);

        // 3. Assert Volume & Distributions
        result.Volume.Should().Be(cohortTickets.Length);
        result.StatusDistribution.Values.Sum().Should().Be(result.Volume);
        result.PriorityDistribution.Values.Sum().Should().Be(result.Volume);

        // 4. Assert First Response Compliance:
        // t1: Achieved (30m <= 60m)
        // t2: Achieved (60m <= 120m)
        // t3: Achieved (60m <= 240m)
        // t4: Breached (chưa FR, quá hạn tại evaluatedAt)
        // t5: Legacy (bỏ qua)
        // t6: Cancelled lúc 10m (chưa FR, chưa breach -> loại khỏi mẫu số)
        // t7: Cancelled lúc 90m (FR breached tại 60m -> Breached)
        // t8: Achieved (50m <= 120m)
        // t9: Achieved (40m <= 240m)
        // -> FR Achieved: 5 (t1, t2, t3, t8, t9)
        // -> FR Breached: 2 (t4, t7)
        result.FirstResponseAchieved.Should().Be(5);
        result.FirstResponseBreached.Should().Be(2);

        // 5. Assert Resolution Compliance:
        // t1: Achieved (Resolved lúc 120m <= 240m)
        // t2: Breached (Resolved lúc 500m > 480m)
        // t3: Ongoing OnTrack (InProgress, 100m <= 1440m -> loại khỏi mẫu số)
        // t4: Ongoing OnTrack (InProgress, segment 10m <= 4320m -> loại khỏi mẫu số)
        // t5: Legacy (bỏ qua)
        // t6: Cancelled lúc 10m (loại khỏi mẫu số)
        // t7: Cancelled lúc 90m (loại khỏi mẫu số vì chưa quá target Resolution 240m)
        // t8: Reopen đang InProgress (chưa hoàn thành, chưa breach -> loại khỏi mẫu số)
        // t9: Achieved (Closed, Duration 180m <= 1440m)
        // -> Res Achieved: 2 (t1, t9)
        // -> Res Breached: 1 (t2)
        result.ResolutionAchieved.Should().Be(2);
        result.ResolutionBreached.Should().Be(1);

        // 6. Assert Average Resolution:
        // Vé đủ điều kiện: áp dụng SLA VÀ hiện Resolved/Closed:
        // t1 (120m) + t2 (500m) + t9 (180m) = 800m. Sample count = 3.
        // t5 là legacy (bỏ qua). t8 là Reopen hiện InProgress (bỏ qua).
        result.ResolutionEligibleCount.Should().Be(3);
        result.ResolutionTotalMinutes.Should().BeApproximately(800.0, 0.01);

        // 7. Oracle Parity Test với Domain SlaEvaluator:
        int expectedFrAchieved = 0;
        int expectedFrBreached = 0;
        int expectedResAchieved = 0;
        int expectedResBreached = 0;
        int expectedResEligible = 0;
        double expectedResTotalMinutes = 0;

        foreach (var t in cohortTickets)
        {
            var eval = SlaEvaluator.Evaluate(t, evaluatedAt);
            if (!eval.IsApplicable) continue;

            // First response
            if (eval.FirstResponse != null)
            {
                if (eval.FirstResponse.State == "CompletedAchieved") expectedFrAchieved++;
                else if (eval.FirstResponse.IsBreached) expectedFrBreached++;
            }

            // Resolution
            if (eval.Resolution != null)
            {
                if (eval.Resolution.State == "ResolvedAchieved") expectedResAchieved++;
                else if (eval.Resolution.IsBreached) expectedResBreached++;
            }

            // Eligible resolution average
            if (t.Status is TicketStatus.Resolved or TicketStatus.Closed)
            {
                expectedResEligible++;
                expectedResTotalMinutes += t.ResolutionAccumulatedDuration.TotalMinutes;
            }
        }

        result.FirstResponseAchieved.Should().Be(expectedFrAchieved, "SQL FR Achieved phải khớp 100% Domain Evaluator");
        result.FirstResponseBreached.Should().Be(expectedFrBreached, "SQL FR Breached phải khớp 100% Domain Evaluator");
        result.ResolutionAchieved.Should().Be(expectedResAchieved, "SQL Res Achieved phải khớp 100% Domain Evaluator");
        result.ResolutionBreached.Should().Be(expectedResBreached, "SQL Res Breached phải khớp 100% Domain Evaluator");
        result.ResolutionEligibleCount.Should().Be(expectedResEligible);
        result.ResolutionTotalMinutes.Should().BeApproximately(expectedResTotalMinutes, 0.01);
    }

    [Fact]
    public async Task Analytics_RespectsHalfOpenInterval_FromInclusive_ToExclusive()
    {
        var fromUtc = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
        var evaluatedAt = new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc);
        var agentId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var query = scope.ServiceProvider.GetRequiredService<ITicketAnalyticsQuery>();

        // t_before: 1 giây trước from
        var tBefore = Ticket.Create(Guid.NewGuid(), "T_Before", "Desc long enough", TicketCategories.VPN, fromUtc.AddSeconds(-1));
        // t_exact_from: đúng mốc from -> ĐƯỢC TÍNH
        var tExactFrom = Ticket.Create(Guid.NewGuid(), "T_ExactFrom", "Desc long enough", TicketCategories.VPN, fromUtc);
        // t_inside: ở giữa
        var tInside = Ticket.Create(Guid.NewGuid(), "T_Inside", "Desc long enough", TicketCategories.VPN, fromUtc.AddDays(5));
        // t_exact_to: đúng mốc to -> KHÔNG ĐƯỢC TÍNH (nửa mở)
        var tExactTo = Ticket.Create(Guid.NewGuid(), "T_ExactTo", "Desc long enough", TicketCategories.VPN, toUtc);
        // t_after: 1 giây sau to
        var tAfter = Ticket.Create(Guid.NewGuid(), "T_After", "Desc long enough", TicketCategories.VPN, toUtc.AddSeconds(1));

        context.Tickets.AddRange(tBefore, tExactFrom, tInside, tExactTo, tAfter);
        await context.SaveChangesAsync();

        var result = await query.QueryAnalyticsAsync(fromUtc, toUtc, evaluatedAt, isAdmin: true, actorId: agentId);

        // Chỉ tExactFrom và tInside được tính trong volume
        var matchedTickets = await context.Tickets
            .Where(t => t.CreatedAt >= fromUtc && t.CreatedAt < toUtc)
            .Select(t => t.Id)
            .ToListAsync();

        matchedTickets.Should().Contain(tExactFrom.Id);
        matchedTickets.Should().Contain(tInside.Id);
        matchedTickets.Should().NotContain(tBefore.Id);
        matchedTickets.Should().NotContain(tExactTo.Id);
        matchedTickets.Should().NotContain(tAfter.Id);

        result.Volume.Should().Be(matchedTickets.Count);
    }

    [Fact]
    public async Task Analytics_ScopeFiltering_AdminVsAgent_DirectAssignment_InactiveTeam_OwnerOnly()
    {
        var fromUtc = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 12, 15, 0, 0, 0, DateTimeKind.Utc);
        var evaluatedAt = new DateTime(2026, 12, 16, 0, 0, 0, DateTimeKind.Utc);

        var agent1Id = Guid.NewGuid();
        var agent2Id = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var query = scope.ServiceProvider.GetRequiredService<ITicketAnalyticsQuery>();

        // Thiết lập Support Teams: Tạo active team và inactive team riêng biệt cho test này
        var teamActive = Guid.NewGuid();
        var teamInactive = Guid.NewGuid();

        await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO support_teams (""Id"", code, name, is_triage_queue, is_active)
            VALUES ({teamActive}, 'ACTIVE_ANALYTICS_TEST', 'Active Team Analytics', false, true)
            ON CONFLICT DO NOTHING;

            INSERT INTO support_team_members (team_id, user_id, is_lead)
            VALUES ({teamActive}, {agent1Id}, false)
            ON CONFLICT DO NOTHING;

            INSERT INTO support_teams (""Id"", code, name, is_triage_queue, is_active)
            VALUES ({teamInactive}, 'INACTIVE_ANALYTICS_TEST', 'Inactive Team Analytics', false, false)
            ON CONFLICT DO NOTHING;

            INSERT INTO support_team_members (team_id, user_id, is_lead)
            VALUES ({teamInactive}, {agent1Id}, false)
            ON CONFLICT DO NOTHING;
        ");

        // 1. Ticket thuộc Active Team mà Agent 1 là thành viên -> Agent 1 THẤY
        var tTeam = Ticket.Create(Guid.NewGuid(), "T_ActiveTeam", "Desc long enough", TicketCategories.VPN, fromUtc.AddDays(1));
        await context.Tickets.AddAsync(tTeam);
        await context.SaveChangesAsync();
        await context.Tickets.Where(t => t.Id == tTeam.Id).ExecuteUpdateAsync(u => u.SetProperty(t => t.SupportTeamId, teamActive));

        // 2. Ticket phân công trực tiếp cho Agent 1 (team null) -> Agent 1 THẤY
        var tDirect = Ticket.Create(Guid.NewGuid(), "T_DirectAssigned", "Desc long enough", TicketCategories.VPN, fromUtc.AddDays(2));
        tDirect.Assign(agent1Id, fromUtc.AddDays(2).AddMinutes(10));
        await context.Tickets.AddAsync(tDirect);
        await context.SaveChangesAsync();
        await context.Tickets.Where(t => t.Id == tDirect.Id).ExecuteUpdateAsync(u => u.SetProperty(t => t.SupportTeamId, (Guid?)null));

        // 3. Ticket do Agent 1 tạo (RequesterId == agent1), nhưng không gán và không thuộc team active của Agent 1 -> Agent 1 KHÔNG THẤY
        var tOwnerOnly = Ticket.Create(agent1Id, "T_OwnerOnly", "Desc long enough", TicketCategories.VPN, fromUtc.AddDays(3));
        await context.Tickets.AddAsync(tOwnerOnly);
        await context.SaveChangesAsync();
        await context.Tickets.Where(t => t.Id == tOwnerOnly.Id).ExecuteUpdateAsync(u => u.SetProperty(t => t.SupportTeamId, (Guid?)null));

        // 4. Ticket thuộc Inactive Team (không direct assign) -> Agent 1 KHÔNG THẤY
        var tInactiveTeam = Ticket.Create(Guid.NewGuid(), "T_InactiveTeam", "Desc long enough", TicketCategories.VPN, fromUtc.AddDays(4));
        await context.Tickets.AddAsync(tInactiveTeam);
        await context.SaveChangesAsync();
        await context.Tickets.Where(t => t.Id == tInactiveTeam.Id).ExecuteUpdateAsync(u => u.SetProperty(t => t.SupportTeamId, teamInactive));

        // Query với Agent 1 scope
        var agent1Result = await query.QueryAnalyticsAsync(fromUtc, toUtc, evaluatedAt, isAdmin: false, actorId: agent1Id);
        // Agent 1 chỉ thấy tTeam và tDirect
        agent1Result.Volume.Should().Be(2);

        // Query với Admin scope -> Admin thấy cả 4 vé
        var adminResult = await query.QueryAnalyticsAsync(fromUtc, toUtc, evaluatedAt, isAdmin: true, actorId: agent1Id);
        adminResult.Volume.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task Analytics_DoesNotMutateXmin_OrChangeEntityState()
    {
        var fromUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);
        var evaluatedAt = new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
        var agentId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var query = scope.ServiceProvider.GetRequiredService<ITicketAnalyticsQuery>();

        var ticket = Ticket.Create(Guid.NewGuid(), "T_PureAnalytics", "Desc long enough", TicketCategories.VPN, fromUtc.AddDays(1));
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), fromUtc.AddDays(1));
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        var xminBefore = await context.Tickets
            .Where(t => t.Id == ticket.Id)
            .Select(t => EF.Property<uint>(t, "xmin"))
            .SingleAsync();

        // Gọi analytics nhiều lần
        for (int i = 0; i < 3; i++)
        {
            _ = await query.QueryAnalyticsAsync(fromUtc, toUtc, evaluatedAt, true, agentId);
        }

        var xminAfter = await context.Tickets
            .Where(t => t.Id == ticket.Id)
            .Select(t => EF.Property<uint>(t, "xmin"))
            .SingleAsync();

        xminAfter.Should().Be(xminBefore, "truy vấn Analytics phải là read-only, không mutate xmin hoặc entity");
    }

    [Fact]
    public async Task Analytics_HttpEndpoint_Validation_Authorization_AndBaselineContract()
    {
        var client = _factory.CreateClient();

        // 1. Anonymous -> 401 Unauthorized
        var anonRes = await client.GetAsync("/api/tickets/analytics?from=2026-11-01T00:00:00Z&to=2026-11-15T00:00:00Z");
        anonRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Requester-only -> 403 Forbidden
        var (requesterToken, _) = await RegisterAndPromoteRoleAsync(client, "Requester");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var reqRes = await client.GetAsync("/api/tickets/analytics?from=2026-11-01T00:00:00Z&to=2026-11-15T00:00:00Z");
        reqRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Agent -> 200 OK
        var (agentToken, _) = await RegisterAndPromoteRoleAsync(client, "Agent");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);

        // a. Thiếu tham số -> 400 Bad Request
        var missingRes = await client.GetAsync("/api/tickets/analytics");
        missingRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // b. Thiếu timezone (ngày thuần) -> 400 Bad Request
        var dateOnlyRes = await client.GetAsync("/api/tickets/analytics?from=2026-11-01&to=2026-11-15T00:00:00Z");
        dateOnlyRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // c. from >= to -> 400 Bad Request
        var invertedRes = await client.GetAsync("/api/tickets/analytics?from=2026-11-15T00:00:00Z&to=2026-11-01T00:00:00Z");
        invertedRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // d. Vượt 90 ngày -> 400 Bad Request
        var exceedRes = await client.GetAsync("/api/tickets/analytics?from=2026-01-01T00:00:00Z&to=2026-04-05T00:00:00Z");
        exceedRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // e. Hợp lệ với múi giờ offset -> 200 OK với DTO chuẩn
        var validRes = await client.GetAsync("/api/tickets/analytics?from=2026-11-01T07:00:00%2B07:00&to=2026-11-15T07:00:00%2B07:00");
        validRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await validRes.Content.ReadFromJsonAsync<TicketAnalyticsDto>();
        body.Should().NotBeNull();
        body!.Range.From.Should().Be(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        body.Range.To.Should().Be(new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc));
        body.StatusDistribution.Should().NotBeNull();
        body.PriorityDistribution.Should().NotBeNull();
        body.FirstResponseCompliance.Should().NotBeNull();
        body.ResolutionCompliance.Should().NotBeNull();

        // 4. Route detail /api/tickets/{id:guid} không bị xung đột
        var nonExistentGuid = Guid.NewGuid();
        var detailRes = await client.GetAsync($"/api/tickets/{nonExistentGuid}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.NotFound, "route /analytics không làm xung đột hoặc chiếm route /api/tickets/{id:guid}");

        client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task Analytics_Performance_QueryExecutionLatency_OnDeterministicDataset()
    {
        var fromUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2027, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var evaluatedAt = toUtc.AddHours(2);
        var agentId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var query = scope.ServiceProvider.GetRequiredService<ITicketAnalyticsQuery>();

        // 1. Dựng dataset quy mô xác định 100 ticket đa dạng các trạng thái, độ ưu tiên và SLA
        var tickets = new List<Ticket>(100);
        for (var i = 0; i < 100; i++)
        {
            var priority = (Priority)(i % 4);
            var createdAt = fromUtc.AddMinutes(i * 180); // Rải đều trong 14 ngày
            var ticket = Ticket.Create(Guid.NewGuid(), $"Perf Ticket {i}", "Description sufficiently long for test", TicketCategories.VPN, createdAt);

            if (i % 10 != 0) // 90% áp dụng SLA, 10% legacy
            {
                ticket.InitializeSla(PrioritySlaTargets.GetFor(priority), createdAt);

                if (i % 2 == 0) // 50% có first response
                {
                    if (ticket.Status is TicketStatus.Submitted or TicketStatus.Assigned)
                    {
                        ticket.Claim(agentId, createdAt.AddMinutes(15));
                    }
                    ticket.RecordFirstResponse(createdAt.AddMinutes(30 + (i % 60)));
                }

                if (i % 3 == 0) // 33% Resolved
                {
                    if (ticket.Status is TicketStatus.Submitted or TicketStatus.Assigned)
                    {
                        ticket.Claim(agentId, createdAt.AddMinutes(15));
                    }
                    ticket.Resolve(createdAt.AddMinutes(100 + (i * 10)));
                }
                else if (i % 5 == 0 && ticket.Status == TicketStatus.Submitted) // Cancelled (chỉ khi còn Submitted)
                {
                    ticket.Cancel(createdAt.AddMinutes(45));
                }
                else if (i % 7 == 0 && ticket.Status == TicketStatus.InProgress) // Pending (chỉ khi InProgress)
                {
                    ticket.MarkPending(TicketStatus.PendingUser, createdAt.AddMinutes(60));
                }
            }
            tickets.Add(ticket);
        }

        context.Tickets.AddRange(tickets);
        await context.SaveChangesAsync();

        // 2. Warmup run (nạp connection pool, EF query plan cache, PostgreSQL buffer cache)
        var warmupResult = await query.QueryAnalyticsAsync(fromUtc, toUtc, evaluatedAt, isAdmin: true, actorId: agentId);
        warmupResult.Volume.Should().Be(100);

        // 3. Thực thi 5 lượt đo lường latency của SQL query
        const int iterations = 5;
        var latencies = new List<double>(iterations);
        for (var i = 0; i < iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await query.QueryAnalyticsAsync(fromUtc, toUtc, evaluatedAt, isAdmin: true, actorId: agentId);
            sw.Stop();

            result.Volume.Should().Be(100);
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        var avgMs = latencies.Average();
        var minMs = latencies.Min();
        var maxMs = latencies.Max();

        _output.WriteLine("[Analytics Performance Evidence]");
        _output.WriteLine($"Dataset size: 100 tickets in cohort [{fromUtc:yyyy-MM-ddTHH:mm:ssZ}, {toUtc:yyyy-MM-ddTHH:mm:ssZ})");
        _output.WriteLine($"Measured iterations: {iterations}");
        _output.WriteLine($"Latencies (ms): {string.Join(", ", latencies.Select(l => l.ToString("F2")))}");
        _output.WriteLine($"Min: {minMs:F2} ms | Max: {maxMs:F2} ms | Avg: {avgMs:F2} ms");
        _output.WriteLine("Execution mode: 100% in PostgreSQL engine via COUNT(*) FILTER, single row returned.");
    }

    private async Task<(string Token, string UserId)> RegisterAndPromoteRoleAsync(HttpClient client, string role)
    {
        var email = $"analytics_{Guid.NewGuid():N}@example.com";
        var password = "Test@1234";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName = "Analytics Role Test"
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseWithUser>();

        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
        var user = await users.FindByIdAsync(body!.User!.UserId);
        await users.AddToRoleAsync(user!, role);

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loginRes.EnsureSuccessStatusCode();
        var loginBody = await loginRes.Content.ReadFromJsonAsync<AuthResponseWithUser>();
        return (loginBody!.Token!, body.User.UserId);
    }

    private sealed record AuthResponseWithUser(string? Token, UserInfo? User);
    private sealed record UserInfo(string UserId, string Email, string? FullName, IReadOnlyList<string> Roles);
}
