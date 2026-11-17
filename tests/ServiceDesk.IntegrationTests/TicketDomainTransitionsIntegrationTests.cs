using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.CreateTicket;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Platform.Persistence;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class TicketDomainTransitionsIntegrationTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly ServiceDeskWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TicketDomainTransitionsIntegrationTests(ServiceDeskWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateTicket_InitializesSlaAtomically_InPostgres()
    {
        var requesterToken = await RegisterAndGetTokenAsync("create_sla_user");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);

        var ticket = await CreateTicketAsync("VPN Problem", "Cannot connect to company VPN from home");

        // Đọc trực tiếp từ PostgreSQL thật để đối chiếu
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var row = await context.Database.SqlQueryRaw<TicketSlaRow>(
            """
            SELECT
                "Id",
                status AS "Status",
                is_sla_applicable AS "IsSlaApplicable",
                first_response_target_minutes AS "FirstResponseTargetMinutes",
                first_responded_at AS "FirstRespondedAt",
                first_response_breached AS "FirstResponseBreached",
                resolution_target_minutes AS "ResolutionTargetMinutes",
                resolution_accumulated_duration AS "ResolutionAccumulatedDuration",
                resolution_segment_started_at AS "ResolutionSegmentStartedAt",
                resolved_at AS "ResolvedAt",
                resolution_breached AS "ResolutionBreached"
            FROM public.tickets
            WHERE "Id" = {0}
            """, ticket.Id).SingleOrDefaultAsync();

        row.Should().NotBeNull();
        row!.IsSlaApplicable.Should().BeTrue();
        row.FirstResponseTargetMinutes.Should().Be(240); // P3 target 4h
        row.ResolutionTargetMinutes.Should().Be(1440); // P3 target 24h
        row.FirstRespondedAt.Should().BeNull();
        row.FirstResponseBreached.Should().BeFalse();
        row.ResolutionAccumulatedDuration.Should().Be(TimeSpan.Zero);
        row.ResolutionSegmentStartedAt.Should().NotBeNull();
        row.ResolvedAt.Should().BeNull();
        row.ResolutionBreached.Should().BeFalse();

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task StaffComment_TriggersFirstResponse_WhileOwnerCommentDoesNot()
    {
        // 1. Requester creates ticket
        var requesterToken = await RegisterAndGetTokenAsync("sla_fr_owner");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var ticket = await CreateTicketAsync("Wifi not working", "Office wifi connects but no internet access");

        // 2. Requester comments -> First response KHÔNG được kích hoạt
        var ownerCommentRes = await _client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new
        {
            content = "Owner follow-up: still no internet."
        });
        ownerCommentRes.StatusCode.Should().Be(HttpStatusCode.Created);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var fr1 = await context.Database.SqlQueryRaw<DateTime?>(
                """SELECT first_responded_at AS "Value" FROM public.tickets WHERE "Id" = {0}""", ticket.Id).SingleOrDefaultAsync();
            fr1.Should().BeNull();
        }

        // 3. Agent comments -> First response ĐƯỢC kích hoạt
        var (agentToken, agentId) = await RegisterAndPromoteRoleAsync("Agent", "sla_fr_agent");
        await AssignToManualTriageTeamAsync(Guid.Parse(agentId), isLead: false);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var agentCommentRes = await _client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new
        {
            content = "IT Support: We are checking the access point now."
        });
        agentCommentRes.StatusCode.Should().Be(HttpStatusCode.Created);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var fr2 = await context.Database.SqlQueryRaw<DateTime?>(
                """SELECT first_responded_at AS "Value" FROM public.tickets WHERE "Id" = {0}""", ticket.Id).SingleOrDefaultAsync();
            fr2.Should().NotBeNull();
        }

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task PendingUser_AutoResumesOnOwnerComment_WithAudit_DEC03()
    {
        // 1. Requester creates ticket
        var requesterToken = await RegisterAndGetTokenAsync("dec03_owner");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var ticket = await CreateTicketAsync("App Crash", "Application crashes upon launch with code 500");

        // 2. Agent claims ticket -> InProgress
        var (agentToken, agentId) = await RegisterAndPromoteRoleAsync("Agent", "dec03_agent");
        await AssignToManualTriageTeamAsync(Guid.Parse(agentId), isLead: false);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var claimRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/claim?expectedRevision={ticket.Revision}", null);
        claimRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var claimedTicket = await claimRes.Content.ReadFromJsonAsync<TicketDto>();

        // 3. Agent moves to PendingUser
        var pendingRes = await _client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/pending", new
        {
            status = (int)TicketStatus.PendingUser,
            expectedRevision = claimedTicket!.Revision
        });
        pendingRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var pendingTicket = await pendingRes.Content.ReadFromJsonAsync<TicketDto>();
        pendingTicket!.Status.Should().Be((int)TicketStatus.PendingUser);

        // Kiểm tra resolution segment paused
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var segStart = await context.Database.SqlQueryRaw<DateTime?>(
                """SELECT resolution_segment_started_at AS "Value" FROM public.tickets WHERE "Id" = {0}""", ticket.Id).SingleOrDefaultAsync();
            segStart.Should().BeNull();
        }

        // 4. Requester replies -> DEC-03 Auto-Resumes to InProgress with System Audit
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var replyRes = await _client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new
        {
            content = "Here is the error log file output attached."
        });
        replyRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Kiểm tra database: status trở lại InProgress và segment started at được gán lại
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await context.Database.SqlQueryRaw<TicketSlaRow>(
                """
                SELECT
                    "Id",
                    status AS "Status",
                    is_sla_applicable AS "IsSlaApplicable",
                    first_response_target_minutes AS "FirstResponseTargetMinutes",
                    first_responded_at AS "FirstRespondedAt",
                    first_response_breached AS "FirstResponseBreached",
                    resolution_target_minutes AS "ResolutionTargetMinutes",
                    resolution_accumulated_duration AS "ResolutionAccumulatedDuration",
                    resolution_segment_started_at AS "ResolutionSegmentStartedAt",
                    resolved_at AS "ResolvedAt",
                    resolution_breached AS "ResolutionBreached"
                FROM public.tickets
                WHERE "Id" = {0}
                """, ticket.Id).SingleOrDefaultAsync();

            row!.Status.Should().Be((int)TicketStatus.InProgress);
            row.ResolutionSegmentStartedAt.Should().NotBeNull();

            // Kiểm tra timeline có system audit auto-resume
            var auditCount = await context.Database.SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM public.ticket_comments
                WHERE ticket_id = {0} AND author_role = 'System' AND content LIKE '%Tự động tiếp tục%'
                """, ticket.Id).SingleAsync();

            auditCount.Should().Be(1);
        }

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task PendingExternal_CanBeResolvedDirectly_DEC04()
    {
        // 1. Setup ticket in InProgress
        var requesterToken = await RegisterAndGetTokenAsync("dec04_owner");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var ticket = await CreateTicketAsync("Server Hardware", "Need disk replacement from vendor");

        var (agentToken, agentId) = await RegisterAndPromoteRoleAsync("Agent", "dec04_agent");
        await AssignToManualTriageTeamAsync(Guid.Parse(agentId), isLead: false);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var claimRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/claim?expectedRevision={ticket.Revision}", null);
        var inProgressTicket = await claimRes.Content.ReadFromJsonAsync<TicketDto>();

        // 2. Chuyển sang PendingExternal
        var pendingRes = await _client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/pending", new
        {
            status = (int)TicketStatus.PendingExternal,
            expectedRevision = inProgressTicket!.Revision
        });
        pendingRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var pendingTicket = await pendingRes.Content.ReadFromJsonAsync<TicketDto>();
        pendingTicket!.Status.Should().Be((int)TicketStatus.PendingExternal);

        // 3. DEC-04: Resolve trực tiếp từ PendingExternal
        var resolveRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/resolve?expectedRevision={pendingTicket.Revision}", null);
        resolveRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolvedTicket = await resolveRes.Content.ReadFromJsonAsync<TicketDto>();
        resolvedTicket!.Status.Should().Be((int)TicketStatus.Resolved);

        // Kiểm tra trong PostgreSQL: resolved_at có giá trị, resolution segment = null
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await context.Database.SqlQueryRaw<TicketSlaRow>(
                """
                SELECT
                    "Id",
                    status AS "Status",
                    is_sla_applicable AS "IsSlaApplicable",
                    first_response_target_minutes AS "FirstResponseTargetMinutes",
                    first_responded_at AS "FirstRespondedAt",
                    first_response_breached AS "FirstResponseBreached",
                    resolution_target_minutes AS "ResolutionTargetMinutes",
                    resolution_accumulated_duration AS "ResolutionAccumulatedDuration",
                    resolution_segment_started_at AS "ResolutionSegmentStartedAt",
                    resolved_at AS "ResolvedAt",
                    resolution_breached AS "ResolutionBreached"
                FROM public.tickets
                WHERE "Id" = {0}
                """, ticket.Id).SingleOrDefaultAsync();

            row!.Status.Should().Be((int)TicketStatus.Resolved);
            row.ResolvedAt.Should().NotBeNull();
            row.ResolutionSegmentStartedAt.Should().BeNull();
        }

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task Reopen_CarriesOverAccumulatedResolutionDuration()
    {
        // 1. Setup ticket and resolve
        var requesterToken = await RegisterAndGetTokenAsync("reopen_owner");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var ticket = await CreateTicketAsync("Email issue", "Cannot send email to external recipients");

        var (agentToken, agentId) = await RegisterAndPromoteRoleAsync("Agent", "reopen_agent");
        await AssignToManualTriageTeamAsync(Guid.Parse(agentId), isLead: false);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var claimRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/claim?expectedRevision={ticket.Revision}", null);
        var inProgressTicket = await claimRes.Content.ReadFromJsonAsync<TicketDto>();

        var resolveRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/resolve?expectedRevision={inProgressTicket!.Revision}", null);
        var resolvedTicket = await resolveRes.Content.ReadFromJsonAsync<TicketDto>();

        // 2. Requester reopens ticket
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var reopenRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/reopen?expectedRevision={resolvedTicket!.Revision}", null);
        reopenRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var reopenedTicket = await reopenRes.Content.ReadFromJsonAsync<TicketDto>();
        reopenedTicket!.Status.Should().Be((int)TicketStatus.InProgress);

        // Kiểm tra database: segment started at được gán lại mốc reopen
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await context.Database.SqlQueryRaw<TicketSlaRow>(
                """
                SELECT
                    "Id",
                    status AS "Status",
                    is_sla_applicable AS "IsSlaApplicable",
                    first_response_target_minutes AS "FirstResponseTargetMinutes",
                    first_responded_at AS "FirstRespondedAt",
                    first_response_breached AS "FirstResponseBreached",
                    resolution_target_minutes AS "ResolutionTargetMinutes",
                    resolution_accumulated_duration AS "ResolutionAccumulatedDuration",
                    resolution_segment_started_at AS "ResolutionSegmentStartedAt",
                    resolved_at AS "ResolvedAt",
                    resolution_breached AS "ResolutionBreached"
                FROM public.tickets
                WHERE "Id" = {0}
                """, ticket.Id).SingleOrDefaultAsync();

            row!.Status.Should().Be((int)TicketStatus.InProgress);
            row.ResolutionSegmentStartedAt.Should().NotBeNull();
        }

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task PendingAndResume_PermissionsMatrix_EnforcedStrictly()
    {
        // 1. Tạo ticket và claim bởi agent 1
        var requesterToken = await RegisterAndGetTokenAsync("perm_owner");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var ticket = await CreateTicketAsync("Database slow", "Database query execution is taking too long");

        var (agent1Token, agent1Id) = await RegisterAndPromoteRoleAsync("Agent", "perm_agent1");
        await AssignToManualTriageTeamAsync(Guid.Parse(agent1Id), isLead: false);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agent1Token);
        var claimRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/claim?expectedRevision={ticket.Revision}", null);
        var inProgressTicket = await claimRes.Content.ReadFromJsonAsync<TicketDto>();

        // 2. Requester gọi pending -> 403 Forbidden
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var reqPendingRes = await _client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/pending", new
        {
            status = (int)TicketStatus.PendingUser,
            expectedRevision = inProgressTicket!.Revision
        });
        reqPendingRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. Agent 2 (thành viên nhóm nhưng không phải assignee, không phải lead) gọi pending -> 403 Forbidden
        var (agent2Token, agent2Id) = await RegisterAndPromoteRoleAsync("Agent", "perm_agent2");
        await AssignToManualTriageTeamAsync(Guid.Parse(agent2Id), isLead: false);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agent2Token);
        var agent2PendingRes = await _client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/pending", new
        {
            status = (int)TicketStatus.PendingUser,
            expectedRevision = inProgressTicket.Revision
        });
        agent2PendingRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Stale revision bởi Agent 1 -> 409 Conflict
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agent1Token);
        var staleRes = await _client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/pending", new
        {
            status = (int)TicketStatus.PendingUser,
            expectedRevision = inProgressTicket.Revision + 999
        });
        staleRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 5. Agent 1 (Assignee) gọi pending -> 200 OK
        var agent1PendingRes = await _client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/pending", new
        {
            status = (int)TicketStatus.PendingUser,
            expectedRevision = inProgressTicket.Revision
        });
        agent1PendingRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var pendingTicket = await agent1PendingRes.Content.ReadFromJsonAsync<TicketDto>();

        // 6. Agent 1 gọi resume -> 200 OK
        var resumeRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/resume?expectedRevision={pendingTicket!.Revision}", null);
        resumeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task ConcurrentComments_OnPendingUserTicket_HandledSafelyWithoutCorruptingState()
    {
        // 1. Setup ticket in PendingUser
        var requesterToken = await RegisterAndGetTokenAsync("concurrent_owner");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var ticket = await CreateTicketAsync("Concurrent Test", "Testing concurrent comments on pending ticket");

        var (agentToken, agentId) = await RegisterAndPromoteRoleAsync("Agent", "concurrent_agent");
        await AssignToManualTriageTeamAsync(Guid.Parse(agentId), isLead: false);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var claimRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/claim?expectedRevision={ticket.Revision}", null);
        var inProgressTicket = await claimRes.Content.ReadFromJsonAsync<TicketDto>();

        var pendingRes = await _client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/pending", new
        {
            status = (int)TicketStatus.PendingUser,
            expectedRevision = inProgressTicket!.Revision
        });
        var pendingTicket = await pendingRes.Content.ReadFromJsonAsync<TicketDto>();

        // 2. Client 1 (Requester) gửi comment (kích hoạt auto-resume) và Client 2 (Agent) gửi comment đồng thời
        using var requesterClient = _factory.CreateClient();
        requesterClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);

        using var agentClient = _factory.CreateClient();
        agentClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);

        var task1 = requesterClient.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new
        {
            content = "Requester reply info 1"
        });
        var task2 = agentClient.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new
        {
            content = "Agent reply info 2"
        });

        var responses = await Task.WhenAll(task1, task2);

        // Ít nhất một request ghi thành công (201 Created); nếu có xung đột concurrency xmin thì trả về 409 Conflict an toàn
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created || r.StatusCode == HttpStatusCode.Conflict);
        responses.Any(r => r.StatusCode == HttpStatusCode.Created).Should().BeTrue();

        // Kiểm tra database: trạng thái phản ánh chính xác request ghi thành công
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var status = await context.Database.SqlQueryRaw<int>(
            """SELECT status AS "Value" FROM public.tickets WHERE "Id" = {0}""", ticket.Id).SingleAsync();

        if (responses[0].StatusCode == HttpStatusCode.Created)
        {
            // Requester comment đã commit thành công -> kích hoạt auto-resume sang InProgress
            status.Should().Be((int)TicketStatus.InProgress);
        }
        else
        {
            // Requester comment gặp xung đột xmin (409) và được rollback an toàn, chỉ Agent comment được commit -> ticket vẫn PendingUser
            status.Should().Be((int)TicketStatus.PendingUser);

            // Khi Requester retry gửi lại comment thành công -> ticket kích hoạt auto-resume sang InProgress
            var retryRes = await requesterClient.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new
            {
                content = "Requester reply info 1 retry"
            });
            retryRes.StatusCode.Should().Be(HttpStatusCode.Created);

            await using var retryScope = _factory.Services.CreateAsyncScope();
            var retryContext = retryScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var retriedStatus = await retryContext.Database.SqlQueryRaw<int>(
                """SELECT status AS "Value" FROM public.tickets WHERE "Id" = {0}""", ticket.Id).SingleAsync();
            retriedStatus.Should().Be((int)TicketStatus.InProgress);
        }

        var commentsCount = await context.Database.SqlQueryRaw<int>(
            """SELECT COUNT(*)::int AS "Value" FROM public.ticket_comments WHERE ticket_id = {0} AND is_internal = false""", ticket.Id).SingleAsync();
        commentsCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task AddToOpenTicketAsync_WhenPersistenceFails_RollsBackBothTicketAndComments()
    {
        // 1. Setup ticket in PendingUser
        var requesterToken = await RegisterAndGetTokenAsync("rollback_owner");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        var ticket = await CreateTicketAsync("Rollback Test", "Testing atomic rollback when persistence fails");

        var (agentToken, agentId) = await RegisterAndPromoteRoleAsync("Agent", "rollback_agent");
        await AssignToManualTriageTeamAsync(Guid.Parse(agentId), isLead: false);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agentToken);
        var claimRes = await _client.PostAsync($"/api/tickets/{ticket.Id}/claim?expectedRevision={ticket.Revision}", null);
        var inProgressTicket = await claimRes.Content.ReadFromJsonAsync<TicketDto>();

        var pendingRes = await _client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/pending", new
        {
            status = (int)TicketStatus.PendingUser,
            expectedRevision = inProgressTicket!.Revision
        });
        var pendingTicket = await pendingRes.Content.ReadFromJsonAsync<TicketDto>();

        // 2. Tải ticket và thực hiện ResumeFromPending trong bộ nhớ
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var timelineStore = scope.ServiceProvider.GetRequiredService<ServiceDesk.Modules.Ticketing.Application.Ports.ITicketTimelineStore>();

        var domainTicket = await context.Set<Ticket>().SingleAsync(t => t.Id == ticket.Id);
        domainTicket.ResumeFromPending(DateTime.UtcNow);

        var validComment = TicketComment.CreatePublic(
            ticket.Id,
            Guid.Parse(agentId),
            "Agent Name",
            RoleNames.Agent,
            "Valid comment content",
            DateTime.UtcNow);

        // Tạo systemAudit bị lỗi vi phạm ràng buộc NOT NULL của PostgreSQL (AuthorRole = null!)
        var corruptedAudit = TicketComment.CreatePublic(
            ticket.Id,
            Guid.Parse(agentId),
            "System",
            "System",
            "Audit text",
            DateTime.UtcNow);
        typeof(TicketComment).GetProperty(nameof(TicketComment.AuthorRole))!.SetValue(corruptedAudit, null!);

        // 3. Thực thi AddToOpenTicketAsync -> Phải ném DbUpdateException
        var act = () => timelineStore.AddToOpenTicketAsync(validComment, domainTicket, corruptedAudit, CancellationToken.None);
        await act.Should().ThrowAsync<DbUpdateException>();

        // 4. Mở scope mới đọc lại từ PostgreSQL thật:
        // Cả ticket (vẫn PendingUser) và comment/audit đều KHÔNG được lưu (Rollback sạch)
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var persistedStatus = await verifyContext.Database.SqlQueryRaw<int>(
            """SELECT status AS "Value" FROM public.tickets WHERE "Id" = {0}""", ticket.Id).SingleAsync();
        persistedStatus.Should().Be((int)TicketStatus.PendingUser); // Không bị đổi thành InProgress

        var persistedSegment = await verifyContext.Database.SqlQueryRaw<DateTime?>(
            """SELECT resolution_segment_started_at AS "Value" FROM public.tickets WHERE "Id" = {0}""", ticket.Id).SingleOrDefaultAsync();
        persistedSegment.Should().BeNull(); // Vẫn là null

        var commentsTotal = await verifyContext.Database.SqlQueryRaw<int>(
            """SELECT COUNT(*)::int AS "Value" FROM public.ticket_comments WHERE ticket_id = {0}""", ticket.Id).SingleAsync();
        commentsTotal.Should().Be(2); // Chỉ có 2 audit từ claim và pending trước đó, không có comment hay audit mới
    }

    private async Task AssignToManualTriageTeamAsync(Guid agentId, bool isLead)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.support_team_members (team_id, user_id, is_lead)
            VALUES ({SupportTeam.ManualTriageId}, {agentId}, {isLead})
            ON CONFLICT (team_id, user_id) DO UPDATE SET is_lead = {isLead}
            """);
    }

    private async Task<TicketDto> CreateTicketAsync(string title, string description)
    {
        var command = new CreateTicketCommand(
            Title: title,
            Description: description,
            CategoryId: TicketCategories.SW
        );

        var response = await _client.PostAsJsonAsync("/api/tickets", command);
        response.EnsureSuccessStatusCode();
        var ticket = await response.Content.ReadFromJsonAsync<TicketDto>();
        return ticket!;
    }

    private async Task<string> RegisterAndGetTokenAsync(string prefix)
    {
        var email = $"{prefix}_{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Test@1234",
            fullName = "Integration User"
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token!;
    }

    private async Task<(string Token, string UserId)> RegisterAndPromoteRoleAsync(string role, string prefix)
    {
        var email = $"{prefix}_{Guid.NewGuid():N}@example.com";
        var password = "Test@1234";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName = "Staff User"
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseWithUser>();

        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
        var user = await users.FindByIdAsync(body!.User!.UserId);
        await users.AddToRoleAsync(user!, role);

        var loginRes = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loginRes.EnsureSuccessStatusCode();
        var loginBody = await loginRes.Content.ReadFromJsonAsync<AuthResponseWithUser>();
        return (loginBody!.Token!, body.User.UserId);
    }

    private sealed record AuthResponse(string? Token, object? User);
    private sealed record AuthResponseWithUser(string? Token, UserInfo? User);
    private sealed record UserInfo(string UserId, string Email, string? FullName, IReadOnlyList<string> Roles);

    private sealed class TicketSlaRow
    {
        public Guid Id { get; set; }
        public int Status { get; set; }
        public bool IsSlaApplicable { get; set; }
        public int? FirstResponseTargetMinutes { get; set; }
        public DateTime? FirstRespondedAt { get; set; }
        public bool FirstResponseBreached { get; set; }
        public int? ResolutionTargetMinutes { get; set; }
        public TimeSpan ResolutionAccumulatedDuration { get; set; }
        public DateTime? ResolutionSegmentStartedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public bool ResolutionBreached { get; set; }
    }
}
