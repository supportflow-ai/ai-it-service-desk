using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.SharedKernel.Authorization;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class AssignmentEndpointTests(TicketIdempotencyPostgresFixture fixture)
    : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Fact]
    public async Task Assign_ResolvedTicket_ReturnsBadRequest()
    {
        using var owner = fixture.First!.CreateClient();
        SetToken(owner, (await RegisterAsync(owner)).Token);
        var ticket = await CreateTicketAsync(owner);

        using var admin = fixture.Second!.CreateClient();
        await SignInAdminAsync(admin);

        // Claim and resolve ticket
        using var claim = await admin.PostAsync($"/api/tickets/{ticket.Id}/claim?expectedRevision={ticket.Revision}", null);
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        var claimedTicket = (await claim.Content.ReadFromJsonAsync<TicketDto>())!;

        using var resolve = await admin.PostAsync($"/api/tickets/{ticket.Id}/resolve?expectedRevision={claimedTicket.Revision}", null);
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        var resolvedTicket = (await resolve.Content.ReadFromJsonAsync<TicketDto>())!;

        // Attempt assignment on resolved ticket
        using var targetAgent = fixture.First.CreateClient();
        var targetAgentId = await SignInAgentAsync(targetAgent);

        using var assign = await admin.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign",
            new { agentId = targetAgentId, expectedRevision = resolvedTicket.Revision });

        Assert.Equal(HttpStatusCode.BadRequest, assign.StatusCode);
    }

    [Fact]
    public async Task Assign_RegularAgentInTeam_ReturnsForbidden()
    {
        using var owner = fixture.First!.CreateClient();
        SetToken(owner, (await RegisterAsync(owner)).Token);
        var ticket = await CreateTicketAsync(owner);

        using var agentClient = fixture.Second!.CreateClient();
        var agentId = await SignInAgentAsync(agentClient);
        var teamId = await AddTeamAsync(agentId, isLead: false, active: true);

        // Move ticket to teamId
        await using (var context = fixture.CreateContext())
        {
            await context.Tickets.Where(t => t.Id == ticket.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.SupportTeamId, teamId));
        }

        using var targetAgent = fixture.First.CreateClient();
        var targetAgentId = await SignInAgentAsync(targetAgent);

        using var assign = await agentClient.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign",
            new { agentId = targetAgentId, expectedRevision = ticket.Revision });

        Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);
    }

    [Fact]
    public async Task Assign_LeadOfOwningTeam_Succeeds()
    {
        using var owner = fixture.First!.CreateClient();
        SetToken(owner, (await RegisterAsync(owner)).Token);
        var ticket = await CreateTicketAsync(owner);

        using var leadClient = fixture.Second!.CreateClient();
        var leadId = await SignInAgentAsync(leadClient);

        using var memberClient = fixture.First.CreateClient();
        var memberId = await SignInAgentAsync(memberClient);

        var teamId = await AddTeamAsync(leadId, isLead: true, active: true);
        await AddMemberToTeamAsync(teamId, memberId, isLead: false);

        await using (var context = fixture.CreateContext())
        {
            await context.Tickets.Where(t => t.Id == ticket.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.SupportTeamId, teamId));
        }

        var leadTicket = (await leadClient.GetFromJsonAsync<TicketDto>($"/api/tickets/{ticket.Id}"))!;

        using var assign = await leadClient.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign",
            new { agentId = memberId, expectedRevision = leadTicket.Revision });

        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var assigned = (await assign.Content.ReadFromJsonAsync<TicketDto>())!;
        Assert.Equal(memberId, assigned.AssignedToId);
        Assert.Equal((int)TicketStatus.Assigned, assigned.Status);
    }

    [Fact]
    public async Task Assign_Lead_TargetOutsideOwningTeam_ReturnsBadRequest()
    {
        using var owner = fixture.First!.CreateClient();
        SetToken(owner, (await RegisterAsync(owner)).Token);
        var ticket = await CreateTicketAsync(owner);

        using var leadClient = fixture.Second!.CreateClient();
        var leadId = await SignInAgentAsync(leadClient);

        using var outsideAgentClient = fixture.First.CreateClient();
        var outsideAgentId = await SignInAgentAsync(outsideAgentClient);

        var teamId = await AddTeamAsync(leadId, isLead: true, active: true);

        await using (var context = fixture.CreateContext())
        {
            await context.Tickets.Where(t => t.Id == ticket.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.SupportTeamId, teamId));
        }

        using var assign = await leadClient.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign",
            new { agentId = outsideAgentId, expectedRevision = ticket.Revision });

        Assert.Equal(HttpStatusCode.BadRequest, assign.StatusCode);
    }

    [Fact]
    public async Task Assign_Admin_TargetOutsideOwningTeam_Succeeds()
    {
        using var owner = fixture.First!.CreateClient();
        SetToken(owner, (await RegisterAsync(owner)).Token);
        var ticket = await CreateTicketAsync(owner);

        using var admin = fixture.Second!.CreateClient();
        await SignInAdminAsync(admin);

        using var outsideAgentClient = fixture.First.CreateClient();
        var outsideAgentId = await SignInAgentAsync(outsideAgentClient);

        using var assign = await admin.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign",
            new { agentId = outsideAgentId, expectedRevision = ticket.Revision });

        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var assigned = (await assign.Content.ReadFromJsonAsync<TicketDto>())!;
        Assert.Equal(outsideAgentId, assigned.AssignedToId);
    }

    [Fact]
    public async Task Unassign_LeadOfOwningTeam_Succeeds()
    {
        using var owner = fixture.First!.CreateClient();
        SetToken(owner, (await RegisterAsync(owner)).Token);
        var ticket = await CreateTicketAsync(owner);

        using var leadClient = fixture.Second!.CreateClient();
        var leadId = await SignInAgentAsync(leadClient);

        using var memberClient = fixture.First.CreateClient();
        var memberId = await SignInAgentAsync(memberClient);

        var teamId = await AddTeamAsync(leadId, isLead: true, active: true);
        await AddMemberToTeamAsync(teamId, memberId, isLead: false);

        await using (var context = fixture.CreateContext())
        {
            await context.Tickets.Where(t => t.Id == ticket.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.SupportTeamId, teamId));
        }

        var leadTicket = (await leadClient.GetFromJsonAsync<TicketDto>($"/api/tickets/{ticket.Id}"))!;

        using var assign = await leadClient.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign",
            new { agentId = memberId, expectedRevision = leadTicket.Revision });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var assigned = (await assign.Content.ReadFromJsonAsync<TicketDto>())!;

        using var unassign = await leadClient.PostAsync($"/api/tickets/{ticket.Id}/unassign?expectedRevision={assigned.Revision}", null);
        Assert.Equal(HttpStatusCode.OK, unassign.StatusCode);
        var unassigned = (await unassign.Content.ReadFromJsonAsync<TicketDto>())!;
        Assert.Null(unassigned.AssignedToId);
        Assert.Equal((int)TicketStatus.Submitted, unassigned.Status);
    }

    [Fact]
    public async Task Unassign_RegularAgent_ReturnsForbidden()
    {
        using var owner = fixture.First!.CreateClient();
        SetToken(owner, (await RegisterAsync(owner)).Token);
        var ticket = await CreateTicketAsync(owner);

        using var leadClient = fixture.Second!.CreateClient();
        var leadId = await SignInAgentAsync(leadClient);

        using var memberClient = fixture.First.CreateClient();
        var memberId = await SignInAgentAsync(memberClient);

        var teamId = await AddTeamAsync(leadId, isLead: true, active: true);
        await AddMemberToTeamAsync(teamId, memberId, isLead: false);

        await using (var context = fixture.CreateContext())
        {
            await context.Tickets.Where(t => t.Id == ticket.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.SupportTeamId, teamId));
        }

        var leadTicket = (await leadClient.GetFromJsonAsync<TicketDto>($"/api/tickets/{ticket.Id}"))!;

        using var assign = await leadClient.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign",
            new { agentId = memberId, expectedRevision = leadTicket.Revision });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var assigned = (await assign.Content.ReadFromJsonAsync<TicketDto>())!;

        // Member attempts unassign
        using var unassign = await memberClient.PostAsync($"/api/tickets/{ticket.Id}/unassign?expectedRevision={assigned.Revision}", null);
        Assert.Equal(HttpStatusCode.Forbidden, unassign.StatusCode);
    }

    [Fact]
    public async Task RevokedLead_ForbiddenOnNextRequest_RetainsMembershipAndInternalNotes()
    {
        using var owner = fixture.First!.CreateClient();
        SetToken(owner, (await RegisterAsync(owner)).Token);
        var ticket = await CreateTicketAsync(owner);

        using var leadClient = fixture.Second!.CreateClient();
        var leadId = await SignInAgentAsync(leadClient);

        using var memberClient = fixture.First.CreateClient();
        var memberId = await SignInAgentAsync(memberClient);

        var teamId = await AddTeamAsync(leadId, isLead: true, active: true);
        await AddMemberToTeamAsync(teamId, memberId, isLead: false);

        await using (var context = fixture.CreateContext())
        {
            await context.Tickets.Where(t => t.Id == ticket.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.SupportTeamId, teamId));

            context.TicketComments.Add(TicketComment.CreateInternalNote(ticket.Id, leadId,
                "Trưởng nhóm", RoleNames.Agent, "Ghi chú nội bộ bí mật", DateTime.UtcNow));
            await context.SaveChangesAsync();
        }

        var leadTicket = (await leadClient.GetFromJsonAsync<TicketDto>($"/api/tickets/{ticket.Id}"))!;

        // 1. Phân công hợp lệ bởi Lead
        using var assign = await leadClient.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign",
            new { agentId = memberId, expectedRevision = leadTicket.Revision });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        var assignedTicket = (await assign.Content.ReadFromJsonAsync<TicketDto>())!;

        // 2. Commit DB: gỡ IsLead của leadId nhưng giữ membership dòng trong support_team_members
        uint snapshotRevision;
        List<TimelineSnapshot> snapshotTimeline;

        await using (var context = fixture.CreateContext())
        {
            await context.SupportTeamMembers
                .Where(m => m.TeamId == teamId && m.UserId == leadId)
                .ExecuteUpdateAsync(u => u.SetProperty(m => m.IsLead, false));

            snapshotRevision = await context.Tickets
                .AsNoTracking()
                .Where(t => t.Id == ticket.Id)
                .Select(t => EF.Property<uint>(t, "xmin"))
                .SingleAsync();

            snapshotTimeline = await context.TicketComments
                .AsNoTracking()
                .Where(c => c.TicketId == ticket.Id)
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.Id)
                .Select(c => new TimelineSnapshot(
                    c.Id,
                    c.AuthorId,
                    c.AuthorName,
                    c.AuthorRole,
                    c.Content,
                    c.IsInternal,
                    c.CreatedAt))
                .ToListAsync();
        }

        Assert.NotEmpty(snapshotTimeline);

        // 3. Dùng CÙNG access token của leadClient gọi request tiếp theo sau commit
        using var unassignDenied = await leadClient.PostAsync(
            $"/api/tickets/{ticket.Id}/unassign?expectedRevision={assignedTicket.Revision}", null);
        Assert.Equal(HttpStatusCode.Forbidden, unassignDenied.StatusCode);

        using var assignDenied = await leadClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/assign",
            new { agentId = leadId, expectedRevision = assignedTicket.Revision });
        Assert.Equal(HttpStatusCode.Forbidden, assignDenied.StatusCode);

        // 4. Assert dữ liệu không bị đột biến (assignee, status, revision và timeline giữ nguyên)
        await using (var context = fixture.CreateContext())
        {
            var currentTicket = await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticket.Id);
            var currentRevision = await context.Tickets
                .AsNoTracking()
                .Where(t => t.Id == ticket.Id)
                .Select(t => EF.Property<uint>(t, "xmin"))
                .SingleAsync();

            Assert.Equal(snapshotRevision, currentRevision);
            Assert.Equal(memberId, currentTicket.AssignedToId);
            Assert.Equal(TicketStatus.Assigned, currentTicket.Status);

            var currentTimeline = await context.TicketComments
                .AsNoTracking()
                .Where(c => c.TicketId == ticket.Id)
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.Id)
                .Select(c => new TimelineSnapshot(
                    c.Id,
                    c.AuthorId,
                    c.AuthorName,
                    c.AuthorRole,
                    c.Content,
                    c.IsInternal,
                    c.CreatedAt))
                .ToListAsync();

            Assert.Equal(snapshotTimeline.Count, currentTimeline.Count);
            for (var i = 0; i < snapshotTimeline.Count; i++)
            {
                var expected = snapshotTimeline[i];
                var actual = currentTimeline[i];
                Assert.Equal(expected.Id, actual.Id);
                Assert.Equal(expected.AuthorId, actual.AuthorId);
                Assert.Equal(expected.AuthorName, actual.AuthorName);
                Assert.Equal(expected.AuthorRole, actual.AuthorRole);
                Assert.Equal(expected.Content, actual.Content);
                Assert.Equal(expected.IsInternal, actual.IsInternal);
                Assert.Equal(expected.CreatedAt, actual.CreatedAt);
            }
        }

        // 5. Kiểm tra người đó vẫn đọc được phiếu và internal notes nhờ còn tư cách thành viên nhóm
        using var detailResponse = await leadClient.GetAsync($"/api/tickets/{ticket.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        var timeline = await leadClient.GetFromJsonAsync<TimelineItemDto[]>($"/api/tickets/{ticket.Id}/timeline");
        Assert.NotNull(timeline);
        Assert.Contains(timeline, item => item.IsInternal && item.Content == "Ghi chú nội bộ bí mật");
    }

    [Fact]
    public async Task InactiveTeam_LeadForbidden_DirectAssigneeReadsOnly_UnrelatedOutOfScope()
    {
        using var owner = fixture.First!.CreateClient();
        SetToken(owner, (await RegisterAsync(owner)).Token);
        var ticket = await CreateTicketAsync(owner);

        using var agentLeadClient = fixture.Second!.CreateClient();
        var agentLeadId = await SignInAgentAsync(agentLeadClient);

        using var memberClient = fixture.First.CreateClient();
        var memberId = await SignInAgentAsync(memberClient);

        // Tạo nhóm với active = false
        var teamId = await AddTeamAsync(agentLeadId, isLead: true, active: false);
        await AddMemberToTeamAsync(teamId, memberId, isLead: false);

        await using (var context = fixture.CreateContext())
        {
            await context.Tickets.Where(t => t.Id == ticket.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.SupportTeamId, teamId));
        }

        // Case A: Người từng là Lead không có direct assignment trên ticket, nhóm inactive -> ngoài read scope -> trả 404
        using var outOfScopeDetail = await agentLeadClient.GetAsync($"/api/tickets/{ticket.Id}");
        Assert.Equal(HttpStatusCode.NotFound, outOfScopeDetail.StatusCode);

        using var outOfScopeAssign = await agentLeadClient.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign",
            new { agentId = memberId, expectedRevision = ticket.Revision });
        Assert.Equal(HttpStatusCode.NotFound, outOfScopeAssign.StatusCode);

        // Case B: Ticket được gán trực tiếp cho agentLeadId -> có direct-read scope
        await using (var context = fixture.CreateContext())
        {
            var t = await context.Tickets.SingleAsync(item => item.Id == ticket.Id);
            t.Assign(agentLeadId, DateTime.UtcNow);
            await context.SaveChangesAsync();
        }

        var directTicket = (await agentLeadClient.GetFromJsonAsync<TicketDto>($"/api/tickets/{ticket.Id}"))!;
        Assert.Equal(agentLeadId, directTicket.AssignedToId);

        // Người này có quyền đọc phiếu, nhưng vì nhóm inactive nên IsLeadAsync trả false -> điều phối bị chặn 403
        using var directAssignDenied = await agentLeadClient.PostAsJsonAsync($"/api/tickets/{ticket.Id}/assign",
            new { agentId = memberId, expectedRevision = directTicket.Revision });
        Assert.Equal(HttpStatusCode.Forbidden, directAssignDenied.StatusCode);

        using var directUnassignDenied = await agentLeadClient.PostAsync(
            $"/api/tickets/{ticket.Id}/unassign?expectedRevision={directTicket.Revision}", null);
        Assert.Equal(HttpStatusCode.Forbidden, directUnassignDenied.StatusCode);
    }

    [Fact]
    public async Task NullSupportTeamId_DirectAssigneeForbiddenToAssign_AdminAllowed()
    {
        using var owner = fixture.First!.CreateClient();
        SetToken(owner, (await RegisterAsync(owner)).Token);
        var ticket = await CreateTicketAsync(owner);

        using var directAgentClient = fixture.Second!.CreateClient();
        var directAgentId = await SignInAgentAsync(directAgentClient);

        using var otherAgentClient = fixture.First.CreateClient();
        var otherAgentId = await SignInAgentAsync(otherAgentClient);

        using var adminClient = fixture.Second.CreateClient();
        await SignInAdminAsync(adminClient);

        // Đặt SupportTeamId = null (phiếu lịch sử) và gán cho directAgentId
        await using (var context = fixture.CreateContext())
        {
            var t = await context.Tickets.SingleAsync(item => item.Id == ticket.Id);
            t.Assign(directAgentId, DateTime.UtcNow);
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE public.tickets SET support_team_id = NULL WHERE \"Id\" = {ticket.Id}");
        }

        // Direct agent đọc được phiếu nhờ direct assignment
        var directTicket = (await directAgentClient.GetFromJsonAsync<TicketDto>($"/api/tickets/{ticket.Id}"))!;
        Assert.Equal(directAgentId, directTicket.AssignedToId);

        await using (var context = fixture.CreateContext())
        {
            var dbTicket = await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticket.Id);
            Assert.Null(dbTicket.SupportTeamId);
        }

        // Agent có direct-read scope nhưng không phải Admin: bị từ chối điều phối 403
        using var directAgentAssignDenied = await directAgentClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/assign",
            new { agentId = otherAgentId, expectedRevision = directTicket.Revision });
        Assert.Equal(HttpStatusCode.Forbidden, directAgentAssignDenied.StatusCode);

        using var directAgentUnassignDenied = await directAgentClient.PostAsync(
            $"/api/tickets/{ticket.Id}/unassign?expectedRevision={directTicket.Revision}", null);
        Assert.Equal(HttpStatusCode.Forbidden, directAgentUnassignDenied.StatusCode);

        // Admin được phân công/hủy phân công kể cả khi SupportTeamId = null
        using var adminUnassign = await adminClient.PostAsync(
            $"/api/tickets/{ticket.Id}/unassign?expectedRevision={directTicket.Revision}", null);
        Assert.Equal(HttpStatusCode.OK, adminUnassign.StatusCode);
        var unassignedTicket = (await adminUnassign.Content.ReadFromJsonAsync<TicketDto>())!;
        Assert.Null(unassignedTicket.AssignedToId);
        Assert.Equal((int)TicketStatus.Submitted, unassignedTicket.Status);

        using var adminAssign = await adminClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/assign",
            new { agentId = otherAgentId, expectedRevision = unassignedTicket.Revision });
        Assert.Equal(HttpStatusCode.OK, adminAssign.StatusCode);
        var newlyAssigned = (await adminAssign.Content.ReadFromJsonAsync<TicketDto>())!;
        Assert.Equal(otherAgentId, newlyAssigned.AssignedToId);
        Assert.Equal((int)TicketStatus.Assigned, newlyAssigned.Status);
    }

    private async Task<Guid> SignInAgentAsync(HttpClient client)
    {
        var identity = await RegisterAsync(client);
        await using var scope = fixture.Second!.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = (await users.FindByIdAsync(identity.User.UserId))!;
        Assert.True((await users.AddToRoleAsync(user, RoleNames.Agent)).Succeeded);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email = identity.User.Email, password = "Test@1234" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        SetToken(client, auth.Token);
        return Guid.Parse(auth.User.UserId);
    }

    private async Task SignInAdminAsync(HttpClient client)
    {
        var identity = await RegisterAsync(client);
        await using var scope = fixture.Second!.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = (await users.FindByIdAsync(identity.User.UserId))!;
        Assert.True((await users.AddToRoleAsync(user, RoleNames.Admin)).Succeeded);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email = identity.User.Email, password = "Test@1234" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        SetToken(client, auth.Token);
    }

    private async Task<Guid> AddTeamAsync(Guid agentId, bool isLead, bool active)
    {
        var id = Guid.NewGuid();
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.support_teams ("Id", code, name, is_triage_queue, is_active)
            VALUES ({id}, {id.ToString("N")}, {"Nhóm kiểm thử"}, false, {active});
            INSERT INTO public.support_team_members (team_id, user_id, is_lead)
            VALUES ({id}, {agentId}, {isLead});
            """);
        return id;
    }

    private async Task AddMemberToTeamAsync(Guid teamId, Guid agentId, bool isLead)
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.support_team_members (team_id, user_id, is_lead)
            VALUES ({teamId}, {agentId}, {isLead});
            """);
    }

    private static async Task<TicketDto> CreateTicketAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/tickets", new
        { title = "Kiểm tra phân công", description = "Phiếu dùng để kiểm tra endpoint phân công", categoryId = "NET" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TicketDto>())!;
    }

    private static void SetToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<AuthResponse> RegisterAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = $"assign-{Guid.NewGuid()}@test.invalid", password = "Test@1234" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private sealed record AuthResponse(string Token, UserInfo User);

    private sealed record TimelineSnapshot(
        Guid Id,
        Guid AuthorId,
        string AuthorName,
        string AuthorRole,
        string Content,
        bool IsInternal,
        DateTime CreatedAt);
}
