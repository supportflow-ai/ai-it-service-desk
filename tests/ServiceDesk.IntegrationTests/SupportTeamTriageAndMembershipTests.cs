using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Modules.Ticketing.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// B1-PR2 (ADR-016): phiếu mới vào hàng đợi phân loại thủ công, phiếu cũ chưa kết thúc được chuyển vào đó bằng migration
/// dữ liệu, và cổng đọc thành viên nhóm trả đúng hợp các nhóm hợp lệ. Chưa có quyền đọc theo nhóm.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SupportTeamTriageAndMembershipTests(TicketIdempotencyPostgresFixture fixture)
    : IClassFixture<TicketIdempotencyPostgresFixture>
{
    private const string BeforeBackfill = "20261108120000_AddSupportTeams";
    private const int Closed = 5, Cancelled = 6;

    private static async Task InsertTicketAsync(ApplicationDbContext context, Guid id, int status, Guid? teamId,
        Guid? assignee, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        DateTime? assignedAt = assignee is null ? null : now;
        var department = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.tickets ("Id", requester_id, requester_department_id, title, description, category_id, status,
                                         assigned_to_id, assigned_at, impact, urgency, priority, created_at, updated_at, support_team_id)
            VALUES ({id}, {Guid.NewGuid()}, {department}, {"Phiếu " + status}, {"Phiếu kiểm tra backfill nhóm hỗ trợ"}, {"NET"}, {status},
                    {assignee}, {assignedAt}, {2}, {3}, {2}, {now}, {now}, {teamId})
            """, token);
    }

    private static Task<List<string>> FingerprintsAsync(ApplicationDbContext context, CancellationToken token) =>
        context.Database.SqlQueryRaw<string>("""
            SELECT concat_ws('|', "Id", requester_id, requester_department_id, title, description, category_id, status,
                             assigned_to_id, assigned_at, impact, urgency, priority, created_at, updated_at, ticket_number) AS "Value"
            FROM public.tickets ORDER BY "Id"
            """).ToListAsync(token);

    private static Task<Dictionary<Guid, Guid?>> TeamsAsync(ApplicationDbContext context, CancellationToken token) =>
        context.Tickets.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.SupportTeamId, token);

    [Fact]
    public async Task BackfillMovesOnlyUnroutedActiveLegacyTicketsToManualTriageWithoutTouchingOtherColumns()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("support_team_backfill_tests").WithUsername("test").WithPassword("test").Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await postgres.StartAsync(timeout.Token);
        var token = timeout.Token;
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeBackfill, token);

        var agent = Guid.NewGuid();
        var network = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.support_teams ("Id", code, name, is_triage_queue, is_active)
            VALUES ({network}, {"NETWORK"}, {"Mạng"}, {false}, {true})
            """, token);

        // Một phiếu cho mỗi trạng thái đã lưu (Draft 0 ... PendingExternal 9); phiếu Assigned/InProgress có người phụ trách.
        var byStatus = new Dictionary<int, Guid>();
        foreach (var status in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 })
        {
            byStatus[status] = Guid.NewGuid();
            await InsertTicketAsync(context, byStatus[status], status, null, status is 2 or 8 ? agent : null, token);
        }

        var alreadyRouted = Guid.NewGuid();
        await InsertTicketAsync(context, alreadyRouted, 2, network, agent, token);
        var before = await FingerprintsAsync(context, token);

        await context.Database.MigrateAsync(token);

        var teams = await TeamsAsync(context, token);
        foreach (var (status, id) in byStatus)
            Assert.Equal(status is Closed or Cancelled ? null : SupportTeam.ManualTriageId, teams[id]);
        Assert.Equal(network, teams[alreadyRouted]); // Không ghi đè phiếu đã có nhóm.
        Assert.Equal(before, await FingerprintsAsync(context, token)); // Chỉ support_team_id thay đổi.
        Assert.False(context.Database.HasPendingModelChanges());

        // Phiếu có người phụ trách vẫn thuộc hàng đợi phân loại: hai khái niệm độc lập.
        var assigned = await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == byStatus[8], token);
        Assert.Equal(agent, assigned.AssignedToId);
        Assert.Equal(SupportTeam.ManualTriageId, assigned.SupportTeamId);

        // Áp dụng lại (Down không làm gì): phiếu cũ do bản API cũ ghi trong lúc triển khai được bổ sung,
        // mọi phiếu khác không đổi.
        await migrator.MigrateAsync(BeforeBackfill, token);
        var lateLegacy = Guid.NewGuid();
        await InsertTicketAsync(context, lateLegacy, 1, null, null, token);
        var lateClosed = Guid.NewGuid();
        await InsertTicketAsync(context, lateClosed, Closed, null, null, token);
        var settled = await FingerprintsAsync(context, token);
        await context.Database.MigrateAsync(token);
        await context.Database.MigrateAsync(token);
        var again = await TeamsAsync(context, token);
        Assert.Equal(SupportTeam.ManualTriageId, again[lateLegacy]);
        Assert.Null(again[lateClosed]);
        Assert.Equal(network, again[alreadyRouted]);
        foreach (var (status, id) in byStatus)
            Assert.Equal(status is Closed or Cancelled ? null : SupportTeam.ManualTriageId, again[id]);
        Assert.Equal(settled, await FingerprintsAsync(context, token));
    }

    [Fact]
    public async Task NewTicketIsPersistedInManualTriageAndMayBeAssignedWithoutChangingTeam()
    {
        var agent = Guid.NewGuid();
        Guid created, assigned;
        await using (var seed = fixture.CreateContext())
        {
            var ticket = Ticket.Create(Guid.NewGuid(), "Tiêu đề", "Mô tả hợp lệ cho phiếu mới", TicketCategories.VPN, DateTime.UtcNow);
            var dto = await new PostgresTicketWriter(seed).CreateAsync(ticket, null, CancellationToken.None);
            created = dto.Id;

            var other = Ticket.Create(Guid.NewGuid(), "Tiêu đề 2", "Mô tả hợp lệ cho phiếu thứ hai", TicketCategories.VPN, DateTime.UtcNow);
            other.Assign(agent, DateTime.UtcNow);
            seed.Tickets.Add(other);
            await seed.SaveChangesAsync();
            assigned = other.Id;
        }

        await using var verify = fixture.CreateContext();
        var createdRow = await verify.Tickets.AsNoTracking().SingleAsync(t => t.Id == created);
        Assert.Equal(SupportTeam.ManualTriageId, createdRow.SupportTeamId);
        Assert.Null(createdRow.AssignedToId);
        var assignedRow = await verify.Tickets.AsNoTracking().SingleAsync(t => t.Id == assigned);
        Assert.Equal(agent, assignedRow.AssignedToId);
        Assert.Equal(SupportTeam.ManualTriageId, assignedRow.SupportTeamId);
    }

    private async Task<Guid> AddTeamAsync(bool active)
    {
        var id = Guid.NewGuid();
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.support_teams ("Id", code, name, is_triage_queue, is_active)
            VALUES ({id}, {"T_" + id.ToString("N")}, {"Nhóm kiểm thử"}, {false}, {active})
            """);
        return id;
    }

    private async Task AddMemberAsync(Guid teamId, Guid userId, bool lead)
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.support_team_members (team_id, user_id, is_lead) VALUES ({teamId}, {userId}, {lead})
            """);
    }

    [Fact]
    public async Task MembershipReaderReturnsUnionOfActiveTeamsAndEvaluatesLeadPerTeam()
    {
        var infrastructure = await AddTeamAsync(true);
        var security = await AddTeamAsync(true);
        var retired = await AddTeamAsync(false);
        var agentA = Guid.NewGuid();
        var agentB = Guid.NewGuid();
        await AddMemberAsync(infrastructure, agentA, true);   // Lead chỉ ở nhóm này.
        await AddMemberAsync(security, agentA, false);
        await AddMemberAsync(retired, agentA, true);          // Nhóm ngừng hoạt động: không tính.
        await AddMemberAsync(security, agentB, true);

        await using var context = fixture.CreateContext();
        var reader = new PostgresSupportTeamMembershipReader(context);

        Assert.Equal(new[] { infrastructure, security }.Order().ToArray(), (await reader.GetTeamIdsAsync(agentA)).Order().ToArray());
        Assert.Equal(new[] { security }, (await reader.GetTeamIdsAsync(agentB)).ToArray());

        Assert.True(await reader.IsMemberAsync(infrastructure, agentA));
        Assert.True(await reader.IsMemberAsync(security, agentA));
        Assert.False(await reader.IsMemberAsync(retired, agentA));
        Assert.False(await reader.IsMemberAsync(infrastructure, agentB));

        Assert.True(await reader.IsLeadAsync(infrastructure, agentA));
        Assert.False(await reader.IsLeadAsync(security, agentA));
        Assert.False(await reader.IsLeadAsync(retired, agentA));
        Assert.True(await reader.IsLeadAsync(security, agentB));
        Assert.False(await reader.IsLeadAsync(infrastructure, agentB));
    }

    [Fact]
    public async Task MembershipReaderGrantsNothingToUnknownUsersAndRevocationTakesEffectImmediately()
    {
        var team = await AddTeamAsync(true);
        var member = Guid.NewGuid();
        await AddMemberAsync(team, member, true);

        await using var context = fixture.CreateContext();
        var reader = new PostgresSupportTeamMembershipReader(context);

        // Người dùng không có dòng thành viên (kể cả mã không tồn tại trong Identity): không có quyền nào.
        var stranger = Guid.NewGuid();
        Assert.Empty(await reader.GetTeamIdsAsync(stranger));
        Assert.False(await reader.IsMemberAsync(team, stranger));
        Assert.False(await reader.IsLeadAsync(team, stranger));
        Assert.Empty(await reader.GetTeamIdsAsync(Guid.Empty));
        Assert.False(await reader.IsMemberAsync(team, Guid.Empty));
        Assert.False(await reader.IsLeadAsync(Guid.Empty, member));

        // Triage không tự có thành viên: không seed Agent vào hàng đợi phân loại.
        Assert.False(await reader.IsMemberAsync(SupportTeam.ManualTriageId, member));
        Assert.Equal(0, await context.SupportTeamMembers.AsNoTracking().CountAsync(m => m.TeamId == SupportTeam.ManualTriageId));

        Assert.True(await reader.IsLeadAsync(team, member));
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM public.support_team_members WHERE team_id = {team} AND user_id = {member}");
        Assert.False(await reader.IsMemberAsync(team, member));
        Assert.False(await reader.IsLeadAsync(team, member));
        Assert.Empty(await reader.GetTeamIdsAsync(member));
    }
}
