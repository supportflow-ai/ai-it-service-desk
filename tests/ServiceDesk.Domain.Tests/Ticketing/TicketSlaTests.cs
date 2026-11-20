using System;
using FluentAssertions;
using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

public class TicketSlaTests
{
    private static Ticket CreateTestTicket(DateTime createdAt, Priority priority = Priority.P3)
    {
        var ticket = Ticket.Create(
            requesterId: Guid.NewGuid(),
            title: "Yêu cầu hỗ trợ phần mềm",
            description: "Cần cài đặt phần mềm chuyên ngành",
            categoryId: "SW",
            createdAt: createdAt);

        // Khởi tạo SLA với target tương ứng priority
        var targets = PrioritySlaTargets.GetFor(priority);
        ticket.InitializeSla(targets, createdAt);
        return ticket;
    }

    [Fact]
    public void InitializeSla_ShouldSetProperties_Correctly()
    {
        var now = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = Ticket.Create(Guid.NewGuid(), "Tiêu đề vé", "Mô tả chi tiết vé", "SW", now);

        ticket.IsSlaApplicable.Should().BeFalse();

        var targets = PrioritySlaTargets.GetFor(Priority.P2);
        ticket.InitializeSla(targets, now);

        ticket.IsSlaApplicable.Should().BeTrue();
        ticket.FirstResponseTargetMinutes.Should().Be(120);
        ticket.ResolutionTargetMinutes.Should().Be(480);
        ticket.ResolutionAccumulatedDuration.Should().Be(TimeSpan.Zero);
        ticket.ResolutionSegmentStartedAt.Should().Be(now);
        ticket.FirstResponseBreached.Should().BeFalse();
        ticket.ResolutionBreached.Should().BeFalse();
        ticket.FirstRespondedAt.Should().BeNull();
        ticket.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void InitializeSla_CalledTwice_ShouldThrow()
    {
        var now = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(now);

        var act = () => ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P1), now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RecordFirstResponse_WithinTarget_ShouldNotBeBreached()
    {
        var createdAt = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(createdAt, Priority.P1); // FR Target = 60m

        var responseAt = createdAt.AddMinutes(59);
        ticket.RecordFirstResponse(responseAt);

        ticket.FirstRespondedAt.Should().Be(responseAt);
        ticket.FirstResponseBreached.Should().BeFalse();
        ticket.GetFirstResponseElapsed(responseAt.AddHours(5)).Should().Be(TimeSpan.FromMinutes(59));
    }

    [Fact]
    public void RecordFirstResponse_ExactlyAtTarget_ShouldNotBeBreached()
    {
        var createdAt = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(createdAt, Priority.P1); // FR Target = 60m

        var responseAt = createdAt.AddMinutes(60);
        ticket.RecordFirstResponse(responseAt);

        ticket.FirstResponseBreached.Should().BeFalse(); // E = T chưa vi phạm
    }

    [Fact]
    public void RecordFirstResponse_AtTargetMinus1Tick_ShouldNotBeBreached()
    {
        var createdAt = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(createdAt, Priority.P1); // FR Target = 60m

        var responseAt = createdAt.AddMinutes(60).AddTicks(-1);
        ticket.RecordFirstResponse(responseAt);

        ticket.FirstResponseBreached.Should().BeFalse();
    }

    [Fact]
    public void RecordFirstResponse_AtTargetPlus1Tick_ShouldBeBreached()
    {
        var createdAt = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(createdAt, Priority.P1); // FR Target = 60m

        var responseAt = createdAt.AddMinutes(60).AddTicks(1); // E = T + 1 tick > T
        ticket.RecordFirstResponse(responseAt);

        ticket.FirstResponseBreached.Should().BeTrue();
    }

    [Fact]
    public void EvaluateSlaBreach_AtTarget_ShouldNotBreach()
    {
        var createdAt = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(createdAt, Priority.P1); // FR Target = 60m

        var changed = ticket.EvaluateSlaBreach(createdAt.AddMinutes(60)); // E == T

        changed.Should().BeFalse();
        ticket.FirstResponseBreached.Should().BeFalse();
    }

    [Fact]
    public void EvaluateSlaBreach_AtTargetPlus1Tick_ShouldBreach()
    {
        var createdAt = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(createdAt, Priority.P1); // FR Target = 60m

        var changed = ticket.EvaluateSlaBreach(createdAt.AddMinutes(60).AddTicks(1)); // E = T + 1 tick

        changed.Should().BeTrue();
        ticket.FirstResponseBreached.Should().BeTrue();
    }

    [Fact]
    public void EvaluateSlaBreach_Resolution_AtTarget_ShouldNotBreach()
    {
        var createdAt = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(createdAt, Priority.P3); // Res Target = 1440m
        ticket.RecordFirstResponse(createdAt.AddMinutes(10));

        var changed = ticket.EvaluateSlaBreach(createdAt.AddMinutes(1440)); // E == T

        changed.Should().BeFalse();
        ticket.ResolutionBreached.Should().BeFalse();
    }

    [Fact]
    public void EvaluateSlaBreach_Resolution_AtTargetPlus1Tick_ShouldBreach()
    {
        var createdAt = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(createdAt, Priority.P3); // Res Target = 1440m
        ticket.RecordFirstResponse(createdAt.AddMinutes(10));

        var changed = ticket.EvaluateSlaBreach(createdAt.AddMinutes(1440).AddTicks(1)); // E = T + 1 tick

        changed.Should().BeTrue();
        ticket.ResolutionBreached.Should().BeTrue();
    }

    [Fact]
    public void RecordFirstResponse_AfterTarget_ShouldBeBreached()
    {
        var createdAt = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(createdAt, Priority.P1); // FR Target = 60m

        var responseAt = createdAt.AddMinutes(60).AddSeconds(1); // E > T
        ticket.RecordFirstResponse(responseAt);

        ticket.FirstResponseBreached.Should().BeTrue();
    }

    [Fact]
    public void PauseAndResumeResolution_ShouldAccumulateElapsed_WithoutTimePassingDuringPause()
    {
        var t0 = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(t0, Priority.P3); // Res Target = 1440m
        ticket.Claim(Guid.NewGuid(), t0); // InProgress

        // Chạy 1 giờ: 8:00 -> 9:00
        var t1 = t0.AddHours(1);
        ticket.MarkPending(TicketStatus.PendingUser, t1);

        ticket.Status.Should().Be(TicketStatus.PendingUser);
        ticket.ResolutionSegmentStartedAt.Should().BeNull();
        ticket.ResolutionAccumulatedDuration.Should().Be(TimeSpan.FromHours(1));

        // Tạm dừng 5 giờ: 9:00 -> 14:00 (thời gian trôi qua không tính vào elapsed)
        var t2 = t0.AddHours(6);
        ticket.GetResolutionElapsed(t2).Should().Be(TimeSpan.FromHours(1));

        // Resume: 14:00
        ticket.ResumeFromPending(t2);
        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.ResolutionSegmentStartedAt.Should().Be(t2);

        // Chạy thêm 2 giờ: 14:00 -> 16:00
        var t3 = t2.AddHours(2);
        ticket.GetResolutionElapsed(t3).Should().Be(TimeSpan.FromHours(3)); // 1h + 2h = 3h
    }

    [Fact]
    public void Resolve_FromPendingExternal_ShouldClosePauseAndResolveSuccessfully()
    {
        var t0 = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(t0, Priority.P2); // Res Target = 480m (8h)
        ticket.Claim(Guid.NewGuid(), t0);

        // Chạy 2 giờ rồi vào PendingExternal
        var t1 = t0.AddHours(2);
        ticket.MarkPending(TicketStatus.PendingExternal, t1);

        // Chờ 3 giờ rồi Resolve trực tiếp từ PendingExternal (DEC-04)
        var t2 = t1.AddHours(3);
        ticket.Resolve(t2);

        ticket.Status.Should().Be(TicketStatus.Resolved);
        ticket.ResolvedAt.Should().Be(t2);
        ticket.ResolutionAccumulatedDuration.Should().Be(TimeSpan.FromHours(2));
        ticket.ResolutionBreached.Should().BeFalse();
        ticket.FirstRespondedAt.Should().Be(t2); // Ghi nhận FirstResponse nếu chưa có
    }

    [Fact]
    public void Reopen_ShouldCarryOverElapsed_AndExcludeResolvedDuration()
    {
        var t0 = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(t0, Priority.P2); // Res Target = 480m (8h)
        ticket.Claim(Guid.NewGuid(), t0);

        // Xử lý 3 giờ -> Resolve đợt 1
        var t1 = t0.AddHours(3);
        ticket.Resolve(t1);
        ticket.ResolutionAccumulatedDuration.Should().Be(TimeSpan.FromHours(3));
        ticket.ResolutionBreached.Should().BeFalse();

        // Khách hàng chờ 2 ngày (48 giờ) rồi Reopen
        var t2 = t1.AddDays(2);
        ticket.Reopen(t2);

        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.ResolutionAccumulatedDuration.Should().Be(TimeSpan.FromHours(3)); // Không bị cộng 48 giờ
        ticket.ResolutionSegmentStartedAt.Should().Be(t2);

        // Xử lý thêm 2 giờ -> Resolve đợt 2
        var t3 = t2.AddHours(2);
        ticket.Resolve(t3);

        ticket.ResolutionAccumulatedDuration.Should().Be(TimeSpan.FromHours(5)); // 3h + 2h = 5h
        ticket.ResolutionBreached.Should().BeFalse(); // 5h <= 8h target
    }

    [Fact]
    public void UpdateSlaPriority_Escalation_CanCauseImmediateBreach()
    {
        var t0 = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(t0, Priority.P3); // Res Target = 1440m (24h)
        ticket.Claim(Guid.NewGuid(), t0);

        // Đã chạy 5 giờ (300m)
        var t1 = t0.AddHours(5);

        // Leo thang Priority từ P3 sang P1 (P1 Res Target = 240m = 4h)
        ticket.UpdateSlaPriority(Priority.P1, t1);

        ticket.Priority.Should().Be(Priority.P1);
        ticket.ResolutionTargetMinutes.Should().Be(240);
        // Elapsed hiện tại = 5h (300m) > Target mới (240m) -> Vi phạm ngay
        ticket.GetResolutionElapsed(t1).Should().Be(TimeSpan.FromHours(5));
        ticket.ResolutionBreached.Should().BeTrue();
    }

    [Fact]
    public void UpdateSlaPriority_Demotion_DoesNotClearExistingBreach()
    {
        var t0 = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(t0, Priority.P1); // Res Target = 240m (4h)
        ticket.Claim(Guid.NewGuid(), t0);

        // Chạy 5 giờ -> Vượt quá 4h của P1
        var t1 = t0.AddHours(5);

        // Hạ cấp Priority từ P1 sang P4 (P4 Res Target = 4320m = 72h)
        ticket.UpdateSlaPriority(Priority.P4, t1);

        ticket.Priority.Should().Be(Priority.P4);
        ticket.ResolutionTargetMinutes.Should().Be(4320);
        ticket.ResolutionBreached.Should().BeTrue(); // Breach đã xảy ra không bị xóa!
    }

    [Fact]
    public void UpdateSlaPriority_CompletedFirstResponse_IsNotRetroactivelyChanged()
    {
        var t0 = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ticket = CreateTestTicket(t0, Priority.P3); // FR Target = 240m
        ticket.RecordFirstResponse(t0.AddMinutes(30)); // Hoàn thành sau 30m

        ticket.FirstResponseTargetMinutes.Should().Be(240);
        ticket.FirstResponseBreached.Should().BeFalse();

        // Đổi sang P1
        ticket.UpdateSlaPriority(Priority.P1, t0.AddHours(2));

        // FirstResponse đã hoàn thành giữ nguyên target và kết quả cũ
        ticket.FirstResponseTargetMinutes.Should().Be(240);
        ticket.FirstResponseBreached.Should().BeFalse();
    }
}
