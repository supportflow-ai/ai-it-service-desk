using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Platform.Persistence;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class QueueSlaFilterIntegrationTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly ServiceDeskWebApplicationFactory _factory;

    public QueueSlaFilterIntegrationTests(ServiceDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task QueueSlaFilter_MatchesSlaEvaluatorParity_AcrossDiverseDataset()
    {
        var evaluatedAt = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var agentId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<ITicketQueries>();

        // 1. Dựng tập dữ liệu đại diện
        // T1: Legacy (không áp dụng SLA)
        var t1 = Ticket.Create(Guid.NewGuid(), "T1 Legacy", "Description for legacy ticket long enough", TicketCategories.VPN, evaluatedAt.AddDays(-10));

        // T2: OnTrack (P3: FR 240m, Res 1440m; tạo 30m trước)
        var t2 = Ticket.Create(Guid.NewGuid(), "T2 OnTrack", "Description for ontrack ticket long enough", TicketCategories.VPN, evaluatedAt.AddMinutes(-30));
        t2.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), evaluatedAt.AddMinutes(-30));

        // T3: AtRisk FR (P3; tạo 200m trước: 200m >= 180m && <= 240m; chưa FR)
        var t3 = Ticket.Create(Guid.NewGuid(), "T3 AtRisk FR", "Description for atrisk fr ticket long enough", TicketCategories.VPN, evaluatedAt.AddMinutes(-200));
        t3.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), evaluatedAt.AddMinutes(-200));

        // T4: Breached FR (P3; tạo 300m trước: 300m > 240m; chưa FR)
        var t4 = Ticket.Create(Guid.NewGuid(), "T4 Breached FR", "Description for breached fr ticket long enough", TicketCategories.VPN, evaluatedAt.AddMinutes(-300));
        t4.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), evaluatedAt.AddMinutes(-300));

        // T5: AtRisk Resolution (P3; tạo 1140m trước: 1140m >= 1080m && <= 1440m; đã FR lúc 30m)
        var t5 = Ticket.Create(Guid.NewGuid(), "T5 AtRisk Res", "Description for atrisk res ticket long enough", TicketCategories.VPN, evaluatedAt.AddMinutes(-1140));
        t5.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), evaluatedAt.AddMinutes(-1140));
        t5.RecordFirstResponse(evaluatedAt.AddMinutes(-1110));

        // T6: Breached Resolution (P3; tạo 1500m trước: 1500m > 1440m; đã FR lúc 30m)
        var t6 = Ticket.Create(Guid.NewGuid(), "T6 Breached Res", "Description for breached res ticket long enough", TicketCategories.VPN, evaluatedAt.AddMinutes(-1500));
        t6.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), evaluatedAt.AddMinutes(-1500));
        t6.RecordFirstResponse(evaluatedAt.AddMinutes(-1470));

        // T7: Completed Achieved (P3; tạo 5h trước, FR lúc 30m, Resolved lúc 120m)
        var t7 = Ticket.Create(Guid.NewGuid(), "T7 Completed Achieved", "Description for completed achieved long enough", TicketCategories.VPN, evaluatedAt.AddHours(-5));
        t7.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), evaluatedAt.AddHours(-5));
        t7.Claim(agentId, evaluatedAt.AddHours(-5).AddMinutes(10));
        t7.RecordFirstResponse(evaluatedAt.AddHours(-5).AddMinutes(30));
        t7.Resolve(evaluatedAt.AddHours(-5).AddMinutes(120));

        // T8: Completed Breached (P3; tạo 30h trước, FR lúc 30m, Resolved lúc 1500m > 1440m)
        var t8 = Ticket.Create(Guid.NewGuid(), "T8 Completed Breached", "Description for completed breached long enough", TicketCategories.VPN, evaluatedAt.AddHours(-30));
        t8.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), evaluatedAt.AddHours(-30));
        t8.Claim(agentId, evaluatedAt.AddHours(-30).AddMinutes(10));
        t8.RecordFirstResponse(evaluatedAt.AddHours(-30).AddMinutes(30));
        t8.Resolve(evaluatedAt.AddHours(-30).AddMinutes(1500));

        // T9: Cancelled (P3; tạo 2h trước, Cancelled lúc 10m)
        var t9 = Ticket.Create(Guid.NewGuid(), "T9 Cancelled", "Description for cancelled ticket long enough", TicketCategories.VPN, evaluatedAt.AddHours(-2));
        t9.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), evaluatedAt.AddHours(-2));
        t9.Cancel(evaluatedAt.AddHours(-2).AddMinutes(10));

        var testTickets = new[] { t1, t2, t3, t4, t5, t6, t7, t8, t9 };
        context.Set<Ticket>().AddRange(testTickets);
        await context.SaveChangesAsync();

        var testIds = testTickets.Select(t => t.Id).ToHashSet();

        // 2. Tính ground-truth từ Domain SlaEvaluator
        var groundTruth = testTickets.ToDictionary(
            t => t.Id,
            t => SlaEvaluator.Evaluate(t, evaluatedAt).OverallStatus
        );

        groundTruth[t1.Id].Should().Be("NotApplicable");
        groundTruth[t2.Id].Should().Be("OnTrack");
        groundTruth[t3.Id].Should().Be("AtRisk");
        groundTruth[t4.Id].Should().Be("Breached");
        groundTruth[t5.Id].Should().Be("AtRisk");
        groundTruth[t6.Id].Should().Be("Breached");
        groundTruth[t7.Id].Should().Be("Completed");
        groundTruth[t8.Id].Should().Be("Breached");
        groundTruth[t9.Id].Should().Be("Completed");

        var readScope = new TicketReadScope.AllTickets();

        // 3. Test Parity cho từng trạng thái:

        // A. Lọc "Breached":
        var breachedResult = await queries.GetAgentQueueAsync(
            agentId, readScope, null, null, null, null, false, 1, 100, "Breached", evaluatedAt);
        var breachedIdsInTest = breachedResult.Items.Where(i => testIds.Contains(i.Id)).Select(i => i.Id).ToList();
        var expectedBreached = groundTruth.Where(kv => kv.Value == "Breached").Select(kv => kv.Key).ToList();
        breachedIdsInTest.Should().BeEquivalentTo(expectedBreached, "SQL filter 'Breached' phải khớp 100% với SlaEvaluator");

        // B. Lọc "AtRisk":
        var atRiskResult = await queries.GetAgentQueueAsync(
            agentId, readScope, null, null, null, null, false, 1, 100, "AtRisk", evaluatedAt);
        var atRiskIdsInTest = atRiskResult.Items.Where(i => testIds.Contains(i.Id)).Select(i => i.Id).ToList();
        var expectedAtRisk = groundTruth.Where(kv => kv.Value == "AtRisk").Select(kv => kv.Key).ToList();
        atRiskIdsInTest.Should().BeEquivalentTo(expectedAtRisk, "SQL filter 'AtRisk' phải khớp 100% với SlaEvaluator");

        // C. Lọc "OnTrack":
        var onTrackResult = await queries.GetAgentQueueAsync(
            agentId, readScope, null, null, null, null, false, 1, 100, "OnTrack", evaluatedAt);
        var onTrackIdsInTest = onTrackResult.Items.Where(i => testIds.Contains(i.Id)).Select(i => i.Id).ToList();
        var expectedOnTrack = groundTruth.Where(kv => kv.Value == "OnTrack").Select(kv => kv.Key).ToList();
        onTrackIdsInTest.Should().BeEquivalentTo(expectedOnTrack, "SQL filter 'OnTrack' phải khớp 100% với SlaEvaluator");

        // D. Lọc "All":
        var allResult = await queries.GetAgentQueueAsync(
            agentId, readScope, null, null, null, null, false, 1, 100, "All", evaluatedAt);
        var allIdsInTest = allResult.Items.Where(i => testIds.Contains(i.Id)).Select(i => i.Id).ToList();
        allIdsInTest.Should().BeEquivalentTo(testIds, "SQL filter 'All' phải bao gồm toàn bộ ticket trong tập");

        // E. Khẳng định mỗi DTO trả về có SLA Summary khớp với Evaluator:
        foreach (var item in breachedResult.Items.Where(i => testIds.Contains(i.Id)))
        {
            item.Sla.Should().NotBeNull();
            item.Sla!.OverallStatus.Should().Be("Breached");
            item.Sla.EvaluatedAt.Should().Be(evaluatedAt);
        }
    }

    [Fact]
    public async Task QueueDetail_DoesNotMutateXmin_OrChangeEntity()
    {
        var evaluatedAt = DateTime.UtcNow;
        var agentId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<ITicketQueries>();

        var ticket = Ticket.Create(Guid.NewGuid(), "XminCheck", "Description for xmin check long enough", TicketCategories.VPN, evaluatedAt.AddMinutes(-30));
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), evaluatedAt.AddMinutes(-30));
        context.Set<Ticket>().Add(ticket);
        await context.SaveChangesAsync();

        // Lấy xmin ban đầu
        var xminBefore = await context.Set<Ticket>()
            .Where(t => t.Id == ticket.Id)
            .Select(t => EF.Property<uint>(t, "xmin"))
            .SingleAsync();

        // Đọc qua Queue
        var queueResult = await queries.GetAgentQueueAsync(
            agentId, new TicketReadScope.AllTickets(), null, null, null, null, false, 1, 10, "OnTrack", evaluatedAt);

        // Đọc qua GetByIdAsync
        var detailResult = await queries.GetByIdAsync(ticket.Id, new TicketReadScope.AllTickets(), CancellationToken.None);

        detailResult.Should().NotBeNull();
        detailResult!.Sla.Should().NotBeNull();
        detailResult.Sla!.OverallStatus.Should().Be("OnTrack");

        // Kiểm tra lại xmin trong DB không đổi
        var xminAfter = await context.Set<Ticket>()
            .Where(t => t.Id == ticket.Id)
            .Select(t => EF.Property<uint>(t, "xmin"))
            .SingleAsync();

        xminAfter.Should().Be(xminBefore, "Truy vấn đọc không được làm thay đổi xmin hoặc gây ghi vào database");
    }

    [Fact]
    public async Task QueueSlaFilter_PaginationAndTotalCount_CalculatedBeforeSkipTake()
    {
        var evaluatedAt = new DateTime(2026, 11, 16, 14, 0, 0, DateTimeKind.Utc);
        var agentId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<ITicketQueries>();

        // Tạo 5 ticket breached (P3, FR = 240m, tạo 300m trước)
        var breachedTickets = Enumerable.Range(1, 5).Select(i =>
        {
            var t = Ticket.Create(Guid.NewGuid(), $"Breached-{i}", $"Description for breached ticket {i} long", TicketCategories.VPN, evaluatedAt.AddMinutes(-300));
            t.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), evaluatedAt.AddMinutes(-300));
            return t;
        }).ToList();

        // Tạo 5 ticket ontrack (P3, tạo 30m trước)
        var onTrackTickets = Enumerable.Range(1, 5).Select(i =>
        {
            var t = Ticket.Create(Guid.NewGuid(), $"OnTrack-{i}", $"Description for ontrack ticket {i} long", TicketCategories.VPN, evaluatedAt.AddMinutes(-30));
            t.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), evaluatedAt.AddMinutes(-30));
            return t;
        }).ToList();

        context.Set<Ticket>().AddRange(breachedTickets);
        context.Set<Ticket>().AddRange(onTrackTickets);
        await context.SaveChangesAsync();

        var readScope = new TicketReadScope.AllTickets();

        // Truy vấn trang 1 với pageSize = 2
        var page1 = await queries.GetAgentQueueAsync(
            agentId, readScope, null, null, null, null, false, 1, 2, "Breached", evaluatedAt);

        // Truy vấn trang 2 với pageSize = 2
        var page2 = await queries.GetAgentQueueAsync(
            agentId, readScope, null, null, null, null, false, 2, 2, "Breached", evaluatedAt);

        // Total count phải phản ánh tổng số ticket Breached trong hệ thống (ít nhất 5)
        page1.TotalCount.Should().BeGreaterThanOrEqualTo(5);
        page1.Items.Count.Should().Be(2);
        page2.Items.Count.Should().Be(2);

        // Hai trang không được trùng lặp
        var page1Ids = page1.Items.Select(x => x.Id).ToHashSet();
        var page2Ids = page2.Items.Select(x => x.Id).ToHashSet();
        page1Ids.Intersect(page2Ids).Should().BeEmpty("Phân trang không được trả về trùng lặp bản ghi");

        // Toàn bộ item trên cả 2 trang đều phải có OverallStatus == Breached
        foreach (var item in page1.Items.Concat(page2.Items))
        {
            item.Sla.Should().NotBeNull();
            item.Sla!.OverallStatus.Should().Be("Breached");
        }
    }

    [Fact]
    public async Task QueueSlaFilter_HttpEndpoint_FilterAndValidation_AndCreateReplay()
    {
        var client = _factory.CreateClient();

        // 1. Tạo user Admin và lấy token (Admin có ReadScope là AllTickets)
        var (adminToken, adminUserId) = await RegisterAndPromoteRoleAsync(client, "Admin");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        // 2. Tạo ticket qua API với Idempotency-Key
        var idempotencyKey = $"sla-idemp-{Guid.NewGuid():N}";
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

        var createCmd = new
        {
            title = "Test SLA Ticket",
            description = "Description for SLA ticket long enough",
            categoryId = TicketCategories.VPN
        };
        var createRes = await client.PostAsJsonAsync("/api/tickets", createCmd);
        createRes.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var createdTicket = await createRes.Content.ReadFromJsonAsync<TicketDto>();
        createdTicket.Should().NotBeNull();
        createdTicket!.Sla.Should().NotBeNull();
        createdTicket.Sla!.IsApplicable.Should().BeTrue();
        createdTicket.Sla.OverallStatus.Should().Be("OnTrack");

        // 3. Replay create ticket với cùng Idempotency-Key: trả đúng snapshot cũ
        var replayRes = await client.PostAsJsonAsync("/api/tickets", createCmd);
        replayRes.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var replayedTicket = await replayRes.Content.ReadFromJsonAsync<TicketDto>();
        replayedTicket.Should().NotBeNull();
        replayedTicket!.Id.Should().Be(createdTicket.Id);
        replayedTicket.Sla.Should().NotBeNull();
        replayedTicket.Sla!.OverallStatus.Should().Be(createdTicket.Sla.OverallStatus);

        client.DefaultRequestHeaders.Remove("Idempotency-Key");

        // 4. GET detail ticket qua HTTP: trả về có SLA summary
        var detailRes = await client.GetAsync($"/api/tickets/{createdTicket.Id}");
        detailRes.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var detailTicket = await detailRes.Content.ReadFromJsonAsync<TicketDto>();
        detailTicket.Should().NotBeNull();
        detailTicket!.Sla.Should().NotBeNull();
        detailTicket.Sla!.IsApplicable.Should().BeTrue();
        detailTicket.Sla.OverallStatus.Should().Be("OnTrack");

        // 5. GET queue với slaStatus=OnTrack: phải chứa createdTicket
        var queueOnTrackRes = await client.GetAsync("/api/tickets/queue?slaStatus=OnTrack");
        queueOnTrackRes.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var queueOnTrack = await queueOnTrackRes.Content.ReadFromJsonAsync<PagedTicketResult>();
        queueOnTrack.Should().NotBeNull();
        queueOnTrack!.Items.Should().Contain(t => t.Id == createdTicket.Id);

        // 6. GET queue với slaStatus=invalid: trả 400 Bad Request
        var invalidRes = await client.GetAsync("/api/tickets/queue?slaStatus=invalid_status");
        invalidRes.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);

        client.DefaultRequestHeaders.Authorization = null;
    }

    private async Task<(string Token, string UserId)> RegisterAndPromoteRoleAsync(HttpClient client, string role)
    {
        var email = $"agent_sla_{Guid.NewGuid():N}@example.com";
        var password = "Test@1234";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName = "Agent SLA Test"
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
