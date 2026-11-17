using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.CreateTicket;
using ServiceDesk.Modules.Ticketing.Application.EvaluateSla;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;
using ServiceDesk.Platform.Persistence;
using ServiceDesk.SharedKernel.Application.Abstractions;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class SlaEvaluationIntegrationTests : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private readonly ServiceDeskWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SlaEvaluationIntegrationTests(ServiceDeskWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Sweep_EvaluatesAndUpdatesBreachedTicketsInPostgres_AndSkipsLegacyAndOnTrack()
    {
        var now = DateTime.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // 1. Ticket 1: Quá hạn First Response (tạo 5h trước > 4h), chưa phản hồi
            var t1 = Ticket.Create(Guid.NewGuid(), "T1 FR Breach", "Description for ticket 1 long enough", TicketCategories.VPN, now.AddHours(-5));
            t1.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), now.AddHours(-5));

            // 2. Ticket 2: Quá hạn Resolution (tạo 30h trước > 24h), đã phản hồi lúc 1h
            var t2 = Ticket.Create(Guid.NewGuid(), "T2 Res Breach", "Description for ticket 2 long enough", TicketCategories.VPN, now.AddHours(-30));
            t2.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), now.AddHours(-30));
            t2.RecordFirstResponse(now.AddHours(-29));

            // 3. Ticket 3: Vừa tạo 10 phút trước (On-track)
            var t3 = Ticket.Create(Guid.NewGuid(), "T3 On Track", "Description for ticket 3 long enough", TicketCategories.VPN, now.AddMinutes(-10));
            t3.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), now.AddMinutes(-10));

            // 4. Ticket 4: Legacy ticket (không áp dụng SLA) tạo 10 ngày trước
            var t4 = Ticket.Create(Guid.NewGuid(), "T4 Legacy", "Description for legacy ticket long enough", TicketCategories.VPN, now.AddDays(-10));

            context.Set<Ticket>().AddRange(t1, t2, t3, t4);
            await context.SaveChangesAsync();

            // 5. Chạy EvaluateSlaUseCase
            var clockMock = new Moq.Mock<IClock>();
            clockMock.SetupGet(c => c.UtcNow).Returns(now);

            var gateway = scope.ServiceProvider.GetRequiredService<ISlaEvaluationGateway>();
            var useCase = new EvaluateSlaUseCase(gateway, clockMock.Object);

            var result = await useCase.ExecuteAsync();

            result.ProcessedCount.Should().BeGreaterThanOrEqualTo(3);
            result.UpdatedCount.Should().BeGreaterThanOrEqualTo(2);
            result.ConflictCount.Should().Be(0);

            // Kiểm tra trong PostgreSQL thật
            var row1 = await context.Set<Ticket>().AsNoTracking().SingleAsync(t => t.Id == t1.Id);
            row1.FirstResponseBreached.Should().BeTrue();
            row1.ResolutionBreached.Should().BeFalse();

            var row2 = await context.Set<Ticket>().AsNoTracking().SingleAsync(t => t.Id == t2.Id);
            row2.FirstResponseBreached.Should().BeFalse();
            row2.ResolutionBreached.Should().BeTrue();

            var row3 = await context.Set<Ticket>().AsNoTracking().SingleAsync(t => t.Id == t3.Id);
            row3.FirstResponseBreached.Should().BeFalse();
            row3.ResolutionBreached.Should().BeFalse();

            var row4 = await context.Set<Ticket>().AsNoTracking().SingleAsync(t => t.Id == t4.Id);
            row4.IsSlaApplicable.Should().BeFalse();
            row4.FirstResponseBreached.Should().BeFalse();
            row4.ResolutionBreached.Should().BeFalse();

            // 6. Idempotency test: Quét lại lần 2 không sinh thêm UPDATE nào
            var xmin1Before = context.Entry(row1).Property<uint>("xmin").CurrentValue;
            var result2 = await useCase.ExecuteAsync();

            // Không có ticket nào bị cập nhật thêm
            var row1After = await context.Set<Ticket>().AsNoTracking().SingleAsync(t => t.Id == t1.Id);
            var xmin1After = context.Entry(row1After).Property<uint>("xmin").CurrentValue;
            xmin1After.Should().Be(xmin1Before);
        }
    }

    [Fact]
    public async Task TwoConcurrentWorkers_SweepSameTickets_ExecuteSafelyWithoutDuplicateOrCorruptedData()
    {
        var now = DateTime.UtcNow;
        var ticketIds = new List<Guid>();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            for (int i = 0; i < 5; i++)
            {
                var t = Ticket.Create(Guid.NewGuid(), $"Concurrent Ticket {i}", "Description for concurrent test ticket", TicketCategories.VPN, now.AddHours(-10));
                t.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), now.AddHours(-10));
                context.Set<Ticket>().Add(t);
                ticketIds.Add(t.Id);
            }
            await context.SaveChangesAsync();
        }

        var clockMock = new Moq.Mock<IClock>();
        clockMock.SetupGet(c => c.UtcNow).Returns(now);

        // Chạy 2 worker task đồng thời trên 2 scope riêng
        await using var scope1 = _factory.Services.CreateAsyncScope();
        var gateway1 = scope1.ServiceProvider.GetRequiredService<ISlaEvaluationGateway>();
        var useCase1 = new EvaluateSlaUseCase(gateway1, clockMock.Object, batchSize: 2);

        await using var scope2 = _factory.Services.CreateAsyncScope();
        var gateway2 = scope2.ServiceProvider.GetRequiredService<ISlaEvaluationGateway>();
        var useCase2 = new EvaluateSlaUseCase(gateway2, clockMock.Object, batchSize: 2);

        var task1 = useCase1.ExecuteAsync();
        var task2 = useCase2.ExecuteAsync();

        var results = await Task.WhenAll(task1, task2);

        // Kiểm tra database: toàn bộ 5 ticket đều được đánh dấu breach hợp lệ
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var breachedCount = await verifyContext.Set<Ticket>().AsNoTracking()
            .CountAsync(t => ticketIds.Contains(t.Id) && t.FirstResponseBreached);

        breachedCount.Should().Be(5);
    }

    [Fact]
    public async Task WorkerConflict_DetachesEntity_AndContinuesSavingSubsequentTickets()
    {
        var now = DateTime.UtcNow;
        Guid t1Id, t2Id;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var t1 = Ticket.Create(Guid.NewGuid(), "Conflict Ticket 1", "Description for conflict ticket 1", TicketCategories.VPN, now.AddHours(-5));
            t1.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), now.AddHours(-5));

            var t2 = Ticket.Create(Guid.NewGuid(), "Subsequent Ticket 2", "Description for subsequent ticket 2", TicketCategories.VPN, now.AddHours(-5));
            t2.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), now.AddHours(-5));

            // Đảm bảo t1Id < t2Id để thứ tự keyset pagination luôn là t1 trước, t2 sau
            if (t1.Id.CompareTo(t2.Id) > 0)
            {
                (t1, t2) = (t2, t1);
            }

            t1Id = t1.Id;
            t2Id = t2.Id;

            context.Set<Ticket>().AddRange(t1, t2);
            await context.SaveChangesAsync();
        }

        await using (var workerScope = _factory.Services.CreateAsyncScope())
        {
            var workerContext = workerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var gateway = workerScope.ServiceProvider.GetRequiredService<ISlaEvaluationGateway>();

            // Worker nạp batch chứa cả 2 ticket
            var batch = await gateway.GetActiveSlaTicketsBatchAsync(null, 10, CancellationToken.None);
            var loadedT1 = batch.Single(t => t.Id == t1Id);
            var loadedT2 = batch.Single(t => t.Id == t2Id);

            // Tác vụ khác bên ngoài cập nhật t1 trước (làm thay đổi xmin của t1)
            await using (var raceScope = _factory.Services.CreateAsyncScope())
            {
                var raceContext = raceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var raceT1 = await raceContext.Set<Ticket>().SingleAsync(t => t.Id == t1Id);
                raceT1.Assign(Guid.NewGuid(), DateTime.UtcNow);
                await raceContext.SaveChangesAsync();
            }

            // Worker đánh giá và cập nhật t1 -> Phải bị xung đột xmin và detach sạch sẽ
            loadedT1.EvaluateSlaBreach(now);
            var t1Updated = await gateway.UpdateBreachedMarkersAsync(loadedT1, CancellationToken.None);
            t1Updated.Should().BeFalse(); // Xung đột

            // Worker tiếp tục xử lý t2 -> Phải thành công bình thường (không bị lỗi ChangeTracker của t1 kéo theo)
            loadedT2.EvaluateSlaBreach(now);
            var t2Updated = await gateway.UpdateBreachedMarkersAsync(loadedT2, CancellationToken.None);
            t2Updated.Should().BeTrue(); // Thành công!
        }

        // Kiểm tra database: t2 đã được cập nhật thành công
        await using var finalScope = _factory.Services.CreateAsyncScope();
        var finalContext = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var finalT2 = await finalContext.Set<Ticket>().AsNoTracking().SingleAsync(t => t.Id == t2Id);
        finalT2.FirstResponseBreached.Should().BeTrue();
    }
}
