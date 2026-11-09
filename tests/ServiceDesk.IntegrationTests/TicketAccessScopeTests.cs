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
public sealed class TicketAccessScopeTests(TicketIdempotencyPostgresFixture fixture)
    : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Theory]
    [InlineData("Requester", HttpStatusCode.NotFound)]
    [InlineData("Agent", HttpStatusCode.NotFound)]
    [InlineData("Admin", HttpStatusCode.OK)]
    public async Task DetailDeniesUnrelatedRequesterAndAgentButAllowsAdmin(string role, HttpStatusCode expected)
    {
        using var owner = fixture.First!.CreateClient();
        var ownerIdentity = await RegisterAsync(owner);
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerIdentity.Token);
        using var created = await owner.PostAsJsonAsync("/api/tickets", new
        { title = "Scope verification", description = "Another requester's private ticket", categoryId = "NET" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ticket = (await created.Content.ReadFromJsonAsync<TicketDto>())!;

        using var actor = fixture.Second!.CreateClient();
        var identity = await RegisterAsync(actor);
        if (role != "Requester")
        {
            await using var scope = fixture.Second.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(identity.User.UserId))!, role)).Succeeded);
            using var login = await actor.PostAsJsonAsync("/api/auth/login", new { email = identity.User.Email, password = "Test@1234" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            identity = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        }
        actor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", identity.Token);
        using var detail = await actor.GetAsync($"/api/tickets/{ticket.Id}");
        Assert.Equal(expected, detail.StatusCode);
        var list = await actor.GetFromJsonAsync<PagedTicketResult>("/api/tickets/me?page=1&pageSize=10");
        Assert.Equal(0, list!.TotalCount);
        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task AgentScopeUnionsActiveTeamsAndDirectAssignmentAndRevokesOnNextRequest()
    {
        using var owner = fixture.First!.CreateClient();
        SetToken(owner, (await RegisterAsync(owner)).Token);
        using var agent = fixture.Second!.CreateClient();
        var agentId = await SignInAgentAsync(agent);
        var teamA = await AddTeamAsync(agentId, active: true);
        var teamB = await AddTeamAsync(agentId, active: true);
        var inactiveTeam = await AddTeamAsync(agentId, active: false);
        var first = await CreateTicketAsync(owner);
        var second = await CreateTicketAsync(owner);
        var inactive = await CreateTicketAsync(owner);
        var assigned = await CreateTicketAsync(owner);

        await using var context = fixture.CreateContext();
        foreach (var (id, team) in new[] { (first.Id, teamA), (second.Id, teamB), (inactive.Id, inactiveTeam), (assigned.Id, teamA) })
        {
            await context.Tickets.Where(ticket => ticket.Id == id)
                .ExecuteUpdateAsync(updates => updates.SetProperty(ticket => ticket.SupportTeamId, team));
        }
        var assignedTicket = await context.Tickets.SingleAsync(ticket => ticket.Id == assigned.Id);
        assignedTicket.Assign(agentId, DateTime.UtcNow);
        await context.SaveChangesAsync();

        await AssertTicketStatusAsync(agent, first.Id, HttpStatusCode.OK);
        await AssertTicketStatusAsync(agent, second.Id, HttpStatusCode.OK);
        await AssertTicketStatusAsync(agent, inactive.Id, HttpStatusCode.NotFound);
        var page = await agent.GetFromJsonAsync<PagedTicketResult>("/api/tickets/queue?pageSize=1");
        Assert.Equal(3, page!.TotalCount);
        Assert.Single(page.Items);

        await context.SupportTeamMembers.Where(member => member.UserId == agentId && member.TeamId == teamA)
            .ExecuteDeleteAsync();
        // Dùng lại cùng JWT, qua máy chủ thứ hai, ngay ở request tiếp theo.
        await AssertTicketStatusAsync(agent, first.Id, HttpStatusCode.NotFound);
        await AssertTicketStatusAsync(agent, assigned.Id, HttpStatusCode.OK);
        await AssertTicketStatusAsync(agent, second.Id, HttpStatusCode.OK);

        var queue = await agent.GetFromJsonAsync<PagedTicketResult>("/api/tickets/queue?pageSize=10");
        Assert.Equal(2, queue!.TotalCount);
        Assert.Equal(new[] { assigned.Id, second.Id }.Order(), queue.Items.Select(item => item.Id).Order());
        using var claim = await agent.PostAsync($"/api/tickets/{first.Id}/claim?expectedRevision={first.Revision}", null);
        Assert.Equal(HttpStatusCode.NotFound, claim.StatusCode);
        using var note = await agent.PostAsJsonAsync($"/api/tickets/{first.Id}/internal-notes", new { content = "Không thuộc phạm vi" });
        Assert.Equal(HttpStatusCode.NotFound, note.StatusCode);
        Assert.False(await context.TicketComments.AnyAsync(comment => comment.TicketId == first.Id));
    }

    [Fact]
    public async Task TransferKeepsInternalNotesWithTicketAndMovesAccessToNewTeam()
    {
        using var owner = fixture.First!.CreateClient();
        SetToken(owner, (await RegisterAsync(owner)).Token);
        using var oldTeamAgent = fixture.Second!.CreateClient();
        using var newTeamAgent = fixture.First.CreateClient();
        using var directAgent = fixture.Second.CreateClient();
        using var admin = fixture.First.CreateClient();
        var oldAgentId = await SignInAgentAsync(oldTeamAgent);
        var newAgentId = await SignInAgentAsync(newTeamAgent);
        var directAgentId = await SignInAgentAsync(directAgent);
        await SignInAdminAsync(admin);
        var oldTeam = await AddTeamAsync(oldAgentId, active: true);
        var newTeam = await AddTeamAsync(newAgentId, active: true);
        var ticket = await CreateTicketAsync(owner);

        await using var context = fixture.CreateContext();
        await context.Tickets.Where(item => item.Id == ticket.Id)
            .ExecuteUpdateAsync(updates => updates.SetProperty(item => item.SupportTeamId, oldTeam));
        context.TicketComments.Add(TicketComment.CreateInternalNote(ticket.Id, oldAgentId,
            "Nhân viên nhóm cũ", RoleNames.Agent, "Ghi chú của nhóm cũ", DateTime.UtcNow));
        await context.SaveChangesAsync();
        var directTicket = await context.Tickets.SingleAsync(item => item.Id == ticket.Id);
        directTicket.Assign(directAgentId, DateTime.UtcNow);
        await context.SaveChangesAsync();

        await AssertInternalNoteVisibleAsync(oldTeamAgent, ticket.Id, expected: true);
        await AssertTicketStatusAsync(newTeamAgent, ticket.Id, HttpStatusCode.NotFound);

        // Chuyển nhóm: phiếu và ghi chú đi cùng nhau, không có cơ chế chia sẻ riêng.
        await context.Tickets.Where(item => item.Id == ticket.Id)
            .ExecuteUpdateAsync(updates => updates.SetProperty(item => item.SupportTeamId, newTeam));

        await AssertTicketStatusAsync(oldTeamAgent, ticket.Id, HttpStatusCode.NotFound);
        using var oldNote = await oldTeamAgent.PostAsJsonAsync($"/api/tickets/{ticket.Id}/internal-notes", new { content = "Không còn quyền" });
        Assert.Equal(HttpStatusCode.NotFound, oldNote.StatusCode);
        await AssertInternalNoteVisibleAsync(newTeamAgent, ticket.Id, expected: true);
        await AssertInternalNoteVisibleAsync(directAgent, ticket.Id, expected: true);
        await AssertInternalNoteVisibleAsync(admin, ticket.Id, expected: true);
        await AssertInternalNoteVisibleAsync(owner, ticket.Id, expected: false);
    }

    [Fact]
    public async Task DeactivatingTeamRevokesTeamAccessOnNextRequestButKeepsDirectAssignment()
    {
        using var owner = fixture.First!.CreateClient();
        SetToken(owner, (await RegisterAsync(owner)).Token);
        using var agent = fixture.Second!.CreateClient();
        var agentId = await SignInAgentAsync(agent);
        var team = await AddTeamAsync(agentId, active: true);
        var byTeam = await CreateTicketAsync(owner);
        var assigned = await CreateTicketAsync(owner);

        await using var context = fixture.CreateContext();
        await context.Tickets.Where(item => item.Id == byTeam.Id || item.Id == assigned.Id)
            .ExecuteUpdateAsync(updates => updates.SetProperty(item => item.SupportTeamId, team));
        var assignedTicket = await context.Tickets.SingleAsync(item => item.Id == assigned.Id);
        assignedTicket.Assign(agentId, DateTime.UtcNow);
        await context.SaveChangesAsync();
        await AssertTicketStatusAsync(agent, byTeam.Id, HttpStatusCode.OK);

        await context.SupportTeams.Where(item => item.Id == team)
            .ExecuteUpdateAsync(updates => updates.SetProperty(item => item.IsActive, false));

        // Cùng JWT, máy chủ khác, request kế tiếp.
        await AssertTicketStatusAsync(agent, byTeam.Id, HttpStatusCode.NotFound);
        await AssertTicketStatusAsync(agent, assigned.Id, HttpStatusCode.OK);
        var queue = await agent.GetFromJsonAsync<PagedTicketResult>("/api/tickets/queue?pageSize=10");
        Assert.Equal(new[] { assigned.Id }, queue!.Items.Select(item => item.Id));
    }

    private async Task AssertInternalNoteVisibleAsync(HttpClient client, Guid ticketId, bool expected)
    {
        var timeline = await client.GetFromJsonAsync<TimelineItemDto[]>($"/api/tickets/{ticketId}/timeline");
        Assert.Equal(expected, timeline!.Any(item => item.IsInternal));
    }

    private async Task SignInAdminAsync(HttpClient client)
    {
        var identity = await RegisterAsync(client);
        await using var scope = fixture.Second!.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(identity.User.UserId))!, RoleNames.Admin)).Succeeded);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email = identity.User.Email, password = "Test@1234" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        SetToken(client, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnershipDoesNotGrantInternalTimelineToAgentOutsideTeam(bool hasRequesterRole)
    {
        using var actor = fixture.Second!.CreateClient();
        await SignInAgentAsync(actor, hasRequesterRole);
        var ownTicket = await CreateTicketAsync(actor);
        await using var context = fixture.CreateContext();
        context.TicketComments.Add(TicketComment.CreateInternalNote(ownTicket.Id, Guid.NewGuid(),
            "Nhân viên kiểm thử", RoleNames.Admin, "Thông tin nội bộ", DateTime.UtcNow));
        await context.SaveChangesAsync();

        using var comment = await actor.PostAsJsonAsync($"/api/tickets/{ownTicket.Id}/comments", new { content = "Thông tin công khai" });
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
        await AssertTicketStatusAsync(actor, ownTicket.Id, HttpStatusCode.OK);
        var timeline = await actor.GetFromJsonAsync<TimelineItemDto[]>($"/api/tickets/{ownTicket.Id}/timeline");
        Assert.False(Assert.Single(timeline!).IsInternal);
        var queue = await actor.GetFromJsonAsync<PagedTicketResult>("/api/tickets/queue");
        Assert.DoesNotContain(queue!.Items, ticket => ticket.Id == ownTicket.Id);
    }

    private async Task<Guid> SignInAgentAsync(HttpClient client, bool hasRequesterRole = true)
    {
        var identity = await RegisterAsync(client);
        await using var scope = fixture.Second!.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = (await users.FindByIdAsync(identity.User.UserId))!;
        Assert.True((await users.AddToRoleAsync(user, RoleNames.Agent)).Succeeded);
        if (!hasRequesterRole)
            Assert.True((await users.RemoveFromRoleAsync(user, RoleNames.Requester)).Succeeded);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email = identity.User.Email, password = "Test@1234" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.Equal(hasRequesterRole, auth.User.Roles.Contains(RoleNames.Requester));
        SetToken(client, auth.Token);
        return Guid.Parse(auth.User.UserId);
    }

    private async Task<Guid> AddTeamAsync(Guid agentId, bool active)
    {
        var id = Guid.NewGuid();
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.support_teams ("Id", code, name, is_triage_queue, is_active)
            VALUES ({id}, {id.ToString("N")}, {"Nhóm kiểm thử"}, false, {active});
            INSERT INTO public.support_team_members (team_id, user_id, is_lead)
            VALUES ({id}, {agentId}, false);
            """);
        return id;
    }

    private static async Task AssertTicketStatusAsync(HttpClient client, Guid id, HttpStatusCode expected)
    {
        foreach (var suffix in new[] { "", "/timeline", "/attachments" })
        {
            using var response = await client.GetAsync($"/api/tickets/{id}{suffix}");
            Assert.Equal(expected, response.StatusCode);
        }
    }

    private static async Task<TicketDto> CreateTicketAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/tickets", new
        { title = "Kiểm tra phạm vi", description = "Phiếu dùng để kiểm tra phạm vi đọc", categoryId = "NET" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TicketDto>())!;
    }

    private static void SetToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<AuthResponse> RegisterAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = $"scope-{Guid.NewGuid()}@test.invalid", password = "Test@1234" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private sealed record AuthResponse(string Token, UserInfo User);
}
