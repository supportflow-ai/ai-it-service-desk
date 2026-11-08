using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Modules.Ticketing.Domain;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceDesk.IntegrationTests;

/// <summary>
/// Nền dữ liệu nhóm hỗ trợ (ADR-016, B1-PR1): migration thuần cộng thêm giữ nguyên phiếu cũ (SupportTeamId = null),
/// tạo đúng một hàng đợi phân loại thủ công, và cơ sở dữ liệu tự bảo vệ các ràng buộc. Chưa có quyền đọc theo nhóm.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SupportTeamPersistenceTests
{
    private const string BeforeSupportTeams = "20261108010000_DefaultRefreshTokenFamilyForLegacyWriters";

    private static PostgreSqlContainer NewPostgres(string database) =>
        new PostgreSqlBuilder("postgres:17-alpine").WithDatabase(database).WithUsername("test").WithPassword("test").Build();

    private static ApplicationDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options);

    // Câu INSERT của bản API cũ: chỉ các cột của schema trước khi có support_team_id.
    private static Task<int> InsertLegacyTicketAsync(ApplicationDbContext context, Guid id, CancellationToken token) =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.tickets ("Id", requester_id, title, description, category_id, status, created_at, updated_at)
            VALUES ({id}, {Guid.NewGuid()}, {"Phiếu cũ"}, {"Phiếu có trước khi có nhóm hỗ trợ"}, {"VPN"}, {1}, {DateTime.UtcNow}, {DateTime.UtcNow})
            """, token);

    private static Task<int> InsertTicketAsync(ApplicationDbContext context, Guid id, Guid? teamId, CancellationToken token) =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.tickets ("Id", requester_id, title, description, category_id, status, created_at, updated_at, support_team_id)
            VALUES ({id}, {Guid.NewGuid()}, {"Phiếu mới"}, {"Phiếu kiểm tra khóa ngoại nhóm"}, {"VPN"}, {1}, {DateTime.UtcNow}, {DateTime.UtcNow}, {teamId})
            """, token);

    private static Task<int> InsertTeamAsync(ApplicationDbContext context, Guid id, string code, bool triage, CancellationToken token) =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.support_teams ("Id", code, name, is_triage_queue, is_active)
            VALUES ({id}, {code}, {"Nhóm " + code}, {triage}, {true})
            """, token);

    private static Task<int> InsertMemberAsync(ApplicationDbContext context, Guid teamId, Guid userId, bool lead, CancellationToken token) =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.support_team_members (team_id, user_id, is_lead) VALUES ({teamId}, {userId}, {lead})
            """, token);

    private static Task<int> CountAsync(ApplicationDbContext context, FormattableString sql, CancellationToken token) =>
        context.Database.SqlQuery<int>(sql).SingleAsync(token);

    private static async Task<string?> RejectedWithAsync(Func<Task> action)
    {
        var exception = await Record.ExceptionAsync(action);
        Assert.NotNull(exception);
        var databaseError = exception as PostgresException ?? exception.InnerException as PostgresException;
        Assert.NotNull(databaseError);
        return databaseError.SqlState;
    }

    [Fact]
    public async Task UpgradeKeepsLegacyTicketsUnassignedSeedsOneTriageQueueAndRoundTrips()
    {
        await using var postgres = NewPostgres("support_team_upgrade_tests");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await postgres.StartAsync(timeout.Token);
        await using var context = NewContext(postgres.GetConnectionString());
        var token = timeout.Token;
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeSupportTeams, token);

        var legacy = Guid.NewGuid();
        await InsertLegacyTicketAsync(context, legacy, token);

        await context.Database.MigrateAsync(token);
        await context.Database.MigrateAsync(token); // Chạy lại không đổi dữ liệu.

        // Phiếu cũ còn nguyên và chưa thuộc nhóm nào; bản API cũ vẫn ghi được phiếu sau khi nâng cấp.
        var afterLegacyWrite = Guid.NewGuid();
        await InsertLegacyTicketAsync(context, afterLegacyWrite, token);
        var teams = await context.Tickets.AsNoTracking().Where(t => t.Id == legacy || t.Id == afterLegacyWrite)
            .Select(t => t.SupportTeamId).ToListAsync(token);
        Assert.Equal(2, teams.Count);
        Assert.All(teams, team => Assert.Null(team));
        Assert.Equal(1, await CountAsync(context, $"SELECT count(*)::int AS \"Value\" FROM public.tickets WHERE \"Id\" = {legacy}", token));

        await AssertSingleSeededTriageQueueAsync(context, token);
        Assert.False(context.Database.HasPendingModelChanges());

        // Quay lui về trước nhóm hỗ trợ rồi nâng cấp lại: bảng và cột được gỡ, rồi tạo lại sạch với đúng một hàng đợi.
        await migrator.MigrateAsync(BeforeSupportTeams, token);
        Assert.Equal(0, await CountAsync(context, $"""
            SELECT count(*)::int AS "Value" FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name IN ('support_teams', 'support_team_members')
            """, token));
        Assert.Equal(0, await CountAsync(context, $"""
            SELECT count(*)::int AS "Value" FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'tickets' AND column_name = 'support_team_id'
            """, token));
        Assert.Equal(1, await CountAsync(context, $"SELECT count(*)::int AS \"Value\" FROM public.tickets WHERE \"Id\" = {legacy}", token));

        await context.Database.MigrateAsync(token);
        await AssertSingleSeededTriageQueueAsync(context, token);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static async Task AssertSingleSeededTriageQueueAsync(ApplicationDbContext context, CancellationToken token)
    {
        Assert.Equal(1, await CountAsync(context, $"SELECT count(*)::int AS \"Value\" FROM public.support_teams", token));
        var queue = await context.SupportTeams.AsNoTracking().SingleAsync(t => t.IsTriageQueue, token);
        Assert.Equal(SupportTeam.ManualTriageId, queue.Id);
        Assert.Equal(SupportTeam.ManualTriageCode, queue.Code);
        Assert.Equal(SupportTeam.ManualTriageName, queue.Name);
        Assert.True(queue.IsActive);
    }

    [Fact]
    public async Task DatabaseEnforcesTeamTriageMembershipAndTicketReferenceRules()
    {
        await using var postgres = NewPostgres("support_team_constraint_tests");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await postgres.StartAsync(timeout.Token);
        await using var context = NewContext(postgres.GetConnectionString());
        var token = timeout.Token;
        await context.Database.MigrateAsync(token);

        // Thêm hàng đợi phân loại thứ hai bị từ chối; thêm nhiều nhóm thường vẫn được.
        Assert.Equal(PostgresErrorCodes.UniqueViolation,
            await RejectedWithAsync(() => InsertTeamAsync(context, Guid.NewGuid(), "SECOND_TRIAGE", true, token)));
        var network = Guid.NewGuid();
        var software = Guid.NewGuid();
        await InsertTeamAsync(context, network, "NETWORK", false, token);
        await InsertTeamAsync(context, software, "SOFTWARE", false, token);
        Assert.Equal(3, await CountAsync(context, $"SELECT count(*)::int AS \"Value\" FROM public.support_teams", token));
        Assert.Equal(1, await CountAsync(context, $"SELECT count(*)::int AS \"Value\" FROM public.support_teams WHERE is_triage_queue", token));

        // Mã nhóm trùng bị từ chối.
        Assert.Equal(PostgresErrorCodes.UniqueViolation,
            await RejectedWithAsync(() => InsertTeamAsync(context, Guid.NewGuid(), "NETWORK", false, token)));

        // Thành viên: khóa (team, user) duy nhất, một người thuộc nhiều nhóm được, nhóm không tồn tại bị từ chối.
        var agent = Guid.NewGuid();
        await InsertMemberAsync(context, network, agent, true, token);
        await InsertMemberAsync(context, software, agent, false, token);
        Assert.Equal(PostgresErrorCodes.UniqueViolation,
            await RejectedWithAsync(() => InsertMemberAsync(context, network, agent, false, token)));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation,
            await RejectedWithAsync(() => InsertMemberAsync(context, Guid.NewGuid(), agent, false, token)));
        var members = await context.SupportTeamMembers.AsNoTracking().Where(m => m.UserId == agent)
            .OrderBy(m => m.IsLead).ToListAsync(token);
        Assert.Equal(2, members.Count);
        Assert.True(members[1].IsLead);

        // Phiếu: gán nhóm hợp lệ được, nhóm không tồn tại bị từ chối, xóa nhóm đang giữ phiếu bị chặn.
        var ticket = Guid.NewGuid();
        await InsertTicketAsync(context, ticket, network, token);
        Assert.Equal(network, await context.Tickets.AsNoTracking().Where(t => t.Id == ticket)
            .Select(t => t.SupportTeamId).SingleAsync(token));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation,
            await RejectedWithAsync(() => InsertTicketAsync(context, Guid.NewGuid(), Guid.NewGuid(), token)));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation,
            await RejectedWithAsync(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM public.support_teams WHERE \"Id\" = {network}", token)));
        Assert.Equal(1, await CountAsync(context, $"SELECT count(*)::int AS \"Value\" FROM public.support_teams WHERE \"Id\" = {network}", token));

        // Nhóm không giữ phiếu xóa được và kéo theo thành viên của nó.
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM public.support_teams WHERE \"Id\" = {software}", token);
        Assert.Equal(0, await CountAsync(context, $"SELECT count(*)::int AS \"Value\" FROM public.support_team_members WHERE team_id = {software}", token));
        Assert.Equal(1, await CountAsync(context, $"SELECT count(*)::int AS \"Value\" FROM public.support_team_members WHERE team_id = {network}", token));
    }
}
