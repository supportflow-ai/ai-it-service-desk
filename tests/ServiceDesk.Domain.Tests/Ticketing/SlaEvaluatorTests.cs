using FluentAssertions;
using ServiceDesk.Modules.Ticketing.Domain;
using Xunit;

namespace ServiceDesk.Domain.Tests.Ticketing;

public class SlaEvaluatorTests
{
    private static readonly DateTime BaseTime = new(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Evaluate_LegacyTicket_ReturnsNotApplicable()
    {
        // Ticket không áp dụng SLA (IsSlaApplicable = false)
        var ticket = Ticket.Create(Guid.NewGuid(), "Legacy", "Legacy description long enough", TicketCategories.HW, BaseTime);

        var result = SlaEvaluator.Evaluate(ticket, BaseTime.AddHours(5));

        result.IsApplicable.Should().BeFalse();
        result.OverallStatus.Should().Be("NotApplicable");
        result.FirstResponse.Should().BeNull();
        result.Resolution.Should().BeNull();
        result.EvaluatedAt.Should().Be(BaseTime.AddHours(5));
    }

    [Fact]
    public void Evaluate_Boundary_EEqualsZero_ReturnsRunningOnTrack()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "EZero", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        // Boundary E = 0 (tại đúng thời điểm tạo)
        var result = SlaEvaluator.Evaluate(ticket, BaseTime);

        result.OverallStatus.Should().Be("OnTrack");
        result.FirstResponse!.ElapsedMinutes.Should().Be(0);
        result.FirstResponse.RemainingMinutes.Should().Be(240);
        result.FirstResponse.State.Should().Be("RunningOnTrack");
        result.FirstResponse.IsBreached.Should().BeFalse();

        result.Resolution!.ElapsedMinutes.Should().Be(0);
        result.Resolution.RemainingMinutes.Should().Be(1440);
        result.Resolution.State.Should().Be("RunningOnTrack");
        result.Resolution.IsBreached.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_OnTrackTicket_ReturnsRunningOnTrack_AndCalculatesDeadlines()
    {
        // Ticket P3: FR = 240m (4h), Res = 1440m (24h)
        var ticket = Ticket.Create(Guid.NewGuid(), "OnTrack", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        // Đánh giá sau 30 phút (30m < 180m = 75% của 240m)
        var evaluatedAt = BaseTime.AddMinutes(30);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        result.IsApplicable.Should().BeTrue();
        result.OverallStatus.Should().Be("OnTrack");

        // First Response
        result.FirstResponse.Should().NotBeNull();
        result.FirstResponse!.TargetMinutes.Should().Be(240);
        result.FirstResponse.ElapsedMinutes.Should().Be(30);
        result.FirstResponse.RemainingMinutes.Should().Be(210);
        result.FirstResponse.Deadline.Should().Be(BaseTime.AddMinutes(240));
        result.FirstResponse.IsBreached.Should().BeFalse();
        result.FirstResponse.State.Should().Be("RunningOnTrack");

        // Resolution
        result.Resolution.Should().NotBeNull();
        result.Resolution!.TargetMinutes.Should().Be(1440);
        result.Resolution.ElapsedMinutes.Should().Be(30);
        result.Resolution.RemainingMinutes.Should().Be(1410);
        result.Resolution.Deadline.Should().Be(BaseTime.AddMinutes(1440));
        result.Resolution.IsBreached.Should().BeFalse();
        result.Resolution.State.Should().Be("RunningOnTrack");
    }

    [Fact]
    public void Evaluate_Boundary_JustBelow75Percent_ReturnsRunningOnTrack()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Below75", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        // Boundary E = 0.75T - 1 second (180 phút - 1 giây = 179m 59s: vẫn OnTrack)
        var evaluatedAt = BaseTime.AddMinutes(180).AddSeconds(-1);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        result.OverallStatus.Should().Be("OnTrack");
        result.FirstResponse!.State.Should().Be("RunningOnTrack");
        result.FirstResponse.IsBreached.Should().BeFalse();
        result.FirstResponse.RemainingMinutes.Should().BeGreaterThan(60);
    }

    [Fact]
    public void Evaluate_AtRiskTicket_Boundary75Percent_ReturnsRunningAtRisk()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "AtRisk75", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        // Đúng 75% của 240m = 180m
        var evaluatedAt = BaseTime.AddMinutes(180);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        result.OverallStatus.Should().Be("AtRisk");
        result.FirstResponse!.State.Should().Be("RunningAtRisk");
        result.FirstResponse.IsBreached.Should().BeFalse();
        result.FirstResponse.RemainingMinutes.Should().Be(60);
    }

    [Fact]
    public void Evaluate_AtRiskTicket_Boundary100Percent_ReturnsRunningAtRisk()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "AtRisk100", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        // Đúng 100% của 240m = 240m (E = T: chưa breach, thuộc AtRisk)
        var evaluatedAt = BaseTime.AddMinutes(240);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        result.OverallStatus.Should().Be("AtRisk");
        result.FirstResponse!.State.Should().Be("RunningAtRisk");
        result.FirstResponse.IsBreached.Should().BeFalse();
        result.FirstResponse.RemainingMinutes.Should().Be(0);
    }

    [Fact]
    public void Evaluate_BreachedTicket_StrictInequality_ReturnsRunningBreached()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Breached", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        // 240 phút + 1 giây (E > T: breach!)
        var evaluatedAt = BaseTime.AddMinutes(240).AddSeconds(1);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        result.OverallStatus.Should().Be("Breached");
        result.FirstResponse!.State.Should().Be("RunningBreached");
        result.FirstResponse.IsBreached.Should().BeTrue();
        result.FirstResponse.RemainingMinutes.Should().BeLessThan(0);
    }

    [Fact]
    public void Evaluate_PersistedBreachMarker_PreservesBreachEvenIfEarlier()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "BreachMarker", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        // Giả lập worker đã đánh dấu breach
        ticket.EvaluateSlaBreach(BaseTime.AddHours(5));
        ticket.FirstResponseBreached.Should().BeTrue();

        // Đánh giá tại mốc sớm hơn
        var result = SlaEvaluator.Evaluate(ticket, BaseTime.AddMinutes(10));

        result.OverallStatus.Should().Be("Breached");
        result.FirstResponse!.IsBreached.Should().BeTrue();
        result.FirstResponse.State.Should().Be("RunningBreached");
    }

    [Fact]
    public void Evaluate_PausedTicket_FreezesResolutionElapsed_DeadlineIsNull()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Paused", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        // Ghi nhận phản hồi đầu tiên tại phút 30
        ticket.RecordFirstResponse(BaseTime.AddMinutes(30));

        // Claim (chuyển sang InProgress) và chuyển PendingUser tại phút 60 (ResolutionAccumulated = 60m)
        ticket.Claim(Guid.NewGuid(), BaseTime.AddMinutes(40));
        ticket.MarkPending(TicketStatus.PendingUser, BaseTime.AddMinutes(60));

        // Đánh giá tại phút 300 (sau 5 tiếng)
        var evaluatedAt = BaseTime.AddMinutes(300);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        // First Response đã xong
        result.FirstResponse!.State.Should().Be("CompletedAchieved");
        result.FirstResponse.IsBreached.Should().BeFalse();
        result.FirstResponse.ElapsedMinutes.Should().Be(30);

        // Resolution bị tạm dừng: Elapsed đóng băng ở 60m, Deadline = null
        result.Resolution!.State.Should().Be("PausedOnTrack");
        result.Resolution.IsBreached.Should().BeFalse();
        result.Resolution.ElapsedMinutes.Should().Be(60);
        result.Resolution.Deadline.Should().BeNull("khi tạm dừng, deadline là null để tránh trôi nổi");
        result.Resolution.RemainingMinutes.Should().Be(1440 - 60);

        result.OverallStatus.Should().Be("OnTrack");
    }

    [Fact]
    public void Evaluate_PausedTicket_WhenAccumulatedExceedsTarget_ReturnsPausedBreached()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "PausedBreached", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);
        ticket.RecordFirstResponse(BaseTime.AddMinutes(30));

        ticket.Claim(Guid.NewGuid(), BaseTime.AddMinutes(40));
        // Chuyển sang Pending sau 1500 phút làm việc (> 1440 phút)
        ticket.MarkPending(TicketStatus.PendingUser, BaseTime.AddMinutes(1500));

        var result = SlaEvaluator.Evaluate(ticket, BaseTime.AddMinutes(2000));

        result.OverallStatus.Should().Be("Breached");
        result.Resolution!.State.Should().Be("PausedBreached");
        result.Resolution.IsBreached.Should().BeTrue();
        result.Resolution.Deadline.Should().BeNull();
    }

    [Fact]
    public void Evaluate_ResolvedTicket_WithinTarget_ReturnsResolvedAchieved_DeadlineIsNull()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "ResolvedAchieved", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        ticket.Claim(Guid.NewGuid(), BaseTime.AddMinutes(10));
        // Resolve tại 120 phút (tự động record FirstResponse nếu chưa có)
        ticket.Resolve(BaseTime.AddMinutes(120));

        var result = SlaEvaluator.Evaluate(ticket, BaseTime.AddMinutes(500));

        result.OverallStatus.Should().Be("Completed");
        result.FirstResponse!.State.Should().Be("CompletedAchieved");
        result.FirstResponse.IsBreached.Should().BeFalse();
        result.Resolution!.State.Should().Be("ResolvedAchieved");
        result.Resolution.IsBreached.Should().BeFalse();
        result.Resolution.Deadline.Should().BeNull();
    }

    [Fact]
    public void Evaluate_ResolvedTicket_ExceedingTarget_ReturnsResolvedBreached()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "ResolvedBreached", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);
        ticket.RecordFirstResponse(BaseTime.AddMinutes(30));

        ticket.Claim(Guid.NewGuid(), BaseTime.AddMinutes(10));
        // Resolve tại 1500 phút (> 1440 phút)
        ticket.Resolve(BaseTime.AddMinutes(1500));

        var result = SlaEvaluator.Evaluate(ticket, BaseTime.AddMinutes(2000));

        result.OverallStatus.Should().Be("Breached");
        result.Resolution!.State.Should().Be("ResolvedBreached");
        result.Resolution.IsBreached.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_CancelledTicket_DoesNotBreach_ReturnsCancelled_AndFreezesElapsedAtCancellation()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Cancelled", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        ticket.Cancel(BaseTime.AddMinutes(10));

        // Đánh giá 10 ngày sau
        var result = SlaEvaluator.Evaluate(ticket, BaseTime.AddDays(10));

        result.OverallStatus.Should().Be("Completed");
        result.FirstResponse!.State.Should().Be("Cancelled");
        result.FirstResponse.IsBreached.Should().BeFalse();
        result.FirstResponse.ElapsedMinutes.Should().Be(10, "First Response elapsed phải đóng băng tại thời điểm Cancel, không trôi theo evaluatedAt");
        result.Resolution!.State.Should().Be("Cancelled");
        result.Resolution.IsBreached.Should().BeFalse();
        result.Resolution.ElapsedMinutes.Should().Be(10, "Resolution elapsed phải đóng băng tại thời điểm Cancel");
        result.Resolution.Deadline.Should().BeNull();
    }

    [Fact]
    public void Evaluate_CancelledTicket_WhenCancelledAfterTarget_ReturnsCancelledBreached()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "CancelledBreached", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        // Cancel tại phút 300 (> 240m First Response Target của P3)
        ticket.Cancel(BaseTime.AddMinutes(300));

        var result = SlaEvaluator.Evaluate(ticket, BaseTime.AddDays(10));

        result.OverallStatus.Should().Be("Breached");
        result.FirstResponse!.State.Should().Be("CancelledBreached");
        result.FirstResponse.IsBreached.Should().BeTrue();
        result.FirstResponse.ElapsedMinutes.Should().Be(300);
    }

    [Fact]
    public void Evaluate_IsPure_DoesNotMutateTicketOrChangeRevision()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Pure", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        var firstResponseBreachedBefore = ticket.FirstResponseBreached;
        var resolutionBreachedBefore = ticket.ResolutionBreached;
        var updatedAtBefore = ticket.UpdatedAt;

        // Đánh giá tại mốc quá hạn
        _ = SlaEvaluator.Evaluate(ticket, BaseTime.AddDays(10));

        // Khẳng định ticket entity không bị sửa đổi
        ticket.FirstResponseBreached.Should().Be(firstResponseBreachedBefore);
        ticket.ResolutionBreached.Should().Be(resolutionBreachedBefore);
        ticket.UpdatedAt.Should().Be(updatedAtBefore);
    }

    [Fact]
    public void Evaluate_Boundary_TickPrecision_075T_Minus_1Tick_ReturnsRunningOnTrack()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Below75Tick", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        // Boundary E = 0.75T - 1 tick (180 phút - 1 tick: vẫn RunningOnTrack)
        var evaluatedAt = BaseTime.AddMinutes(180).AddTicks(-1);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        result.OverallStatus.Should().Be("OnTrack");
        result.FirstResponse!.State.Should().Be("RunningOnTrack");
        result.FirstResponse.IsBreached.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_Boundary_TickPrecision_075T_ReturnsRunningAtRisk()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "AtRisk75Tick", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        // Boundary E = 0.75T (đúng 180 phút: RunningAtRisk)
        var evaluatedAt = BaseTime.AddMinutes(180);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        result.OverallStatus.Should().Be("AtRisk");
        result.FirstResponse!.State.Should().Be("RunningAtRisk");
        result.FirstResponse.IsBreached.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_Boundary_TickPrecision_T_ReturnsRunningAtRisk_NotBreached()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "AtTargetTick", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        // Boundary E = T (đúng 240 phút: RunningAtRisk, chưa vi phạm vì E <= T)
        var evaluatedAt = BaseTime.AddMinutes(240);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        result.OverallStatus.Should().Be("AtRisk");
        result.FirstResponse!.State.Should().Be("RunningAtRisk");
        result.FirstResponse.IsBreached.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_Boundary_TickPrecision_T_Plus_1Tick_ReturnsRunningBreached()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Breached1Tick", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);

        // Boundary E = T + 1 tick (240 phút + 1 tick: vi phạm ngay lập tức vì E > T)
        var evaluatedAt = BaseTime.AddMinutes(240).AddTicks(1);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        result.OverallStatus.Should().Be("Breached");
        result.FirstResponse!.State.Should().Be("RunningBreached");
        result.FirstResponse.IsBreached.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Resolution_Boundary_TickPrecision_075T_Minus_1Tick_ReturnsRunningOnTrack()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "ResBelow75Tick", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);
        ticket.RecordFirstResponse(BaseTime.AddMinutes(10));

        // Resolution P3: Target = 1440m. 0.75 * 1440m = 1080m.
        var evaluatedAt = BaseTime.AddMinutes(1080).AddTicks(-1);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        result.OverallStatus.Should().Be("OnTrack");
        result.Resolution!.State.Should().Be("RunningOnTrack");
        result.Resolution.IsBreached.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_Resolution_Boundary_TickPrecision_075T_ReturnsRunningAtRisk()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Res75Tick", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);
        ticket.RecordFirstResponse(BaseTime.AddMinutes(10));

        var evaluatedAt = BaseTime.AddMinutes(1080);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        result.OverallStatus.Should().Be("AtRisk");
        result.Resolution!.State.Should().Be("RunningAtRisk");
        result.Resolution.IsBreached.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_Resolution_Boundary_TickPrecision_T_ReturnsRunningAtRisk_NotBreached()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "ResTargetTick", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);
        ticket.RecordFirstResponse(BaseTime.AddMinutes(10));

        var evaluatedAt = BaseTime.AddMinutes(1440);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        result.OverallStatus.Should().Be("AtRisk");
        result.Resolution!.State.Should().Be("RunningAtRisk");
        result.Resolution.IsBreached.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_Resolution_Boundary_TickPrecision_T_Plus_1Tick_ReturnsRunningBreached()
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "ResBreached1Tick", "Description long enough for ticket", TicketCategories.HW, BaseTime);
        ticket.InitializeSla(PrioritySlaTargets.GetFor(Priority.P3), BaseTime);
        ticket.RecordFirstResponse(BaseTime.AddMinutes(10));

        var evaluatedAt = BaseTime.AddMinutes(1440).AddTicks(1);
        var result = SlaEvaluator.Evaluate(ticket, evaluatedAt);

        result.OverallStatus.Should().Be("Breached");
        result.Resolution!.State.Should().Be("RunningBreached");
        result.Resolution.IsBreached.Should().BeTrue();
    }
}
