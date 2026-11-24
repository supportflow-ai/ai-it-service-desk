using Microsoft.EntityFrameworkCore;
using ServiceDesk.Modules.Ticketing.Application.Authorization;
using ServiceDesk.Modules.Ticketing.Application.Contracts;
using ServiceDesk.Modules.Ticketing.Application.Ports;
using ServiceDesk.Modules.Ticketing.Domain;

namespace ServiceDesk.Modules.Ticketing.Infrastructure;

internal sealed class PostgresTicketReportQueries(
    DbContext context,
    IAgentDirectory agentDirectory) : ITicketReportQueries
{
    private IQueryable<Ticket> ApplyFilters(
        TicketReadScope readScope,
        DateTime? from,
        DateTime? to,
        Guid? teamId)
    {
        var query = context.Set<Ticket>().AsNoTracking();

        if (from.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= to.Value);
        }

        if (teamId.HasValue)
        {
            query = query.Where(t => t.SupportTeamId == teamId.Value);
        }

        return TicketScopeQuery.Apply(query, context, readScope);
    }

    public async Task<SlaPerformanceReportDto> GetSlaPerformanceAsync(
        TicketReadScope readScope,
        DateTime? from,
        DateTime? to,
        Guid? teamId,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(readScope, from, to, teamId);

        var total = await query.CountAsync(cancellationToken);
        if (total == 0)
        {
            return new SlaPerformanceReportDto(0, 0, 0, 100.0, 0, 100.0, 0, 0);
        }

        var resolved = await query.CountAsync(
            t => t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed,
            cancellationToken);

        var firstBreached = await query.CountAsync(t => t.FirstResponseBreached, cancellationToken);
        var resolutionBreached = await query.CountAsync(t => t.ResolutionBreached, cancellationToken);

        var firstComplianceRate = Math.Round((total - firstBreached) / (double)total * 100.0, 1);
        var resolutionComplianceRate = Math.Round((total - resolutionBreached) / (double)total * 100.0, 1);

        var mttaDiffs = await query
            .Where(t => t.FirstRespondedAt != null)
            .Select(t => new { t.CreatedAt, t.FirstRespondedAt })
            .ToListAsync(cancellationToken);

        var mttaMinutes = mttaDiffs.Count == 0 ? 0 : mttaDiffs.Average(t => (t.FirstRespondedAt!.Value - t.CreatedAt).TotalMinutes);

        var mttrDiffs = await query
            .Where(t => t.ResolvedAt != null)
            .Select(t => new { t.CreatedAt, t.ResolvedAt })
            .ToListAsync(cancellationToken);

        var mttrMinutes = mttrDiffs.Count == 0 ? 0 : mttrDiffs.Average(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalMinutes);

        return new SlaPerformanceReportDto(
            total,
            resolved,
            firstBreached,
            firstComplianceRate,
            resolutionBreached,
            resolutionComplianceRate,
            Math.Round(mttaMinutes, 1),
            Math.Round(mttrMinutes, 1));
    }

    public async Task<TicketDistributionReportDto> GetDistributionAsync(
        TicketReadScope readScope,
        DateTime? from,
        DateTime? to,
        Guid? teamId,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(readScope, from, to, teamId);

        // Phân bố theo trạng thái
        var statusGroups = await query
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byStatus = statusGroups.ToDictionary(g => g.Status.ToString(), g => g.Count);

        // Phân bố theo mức ưu tiên
        var priorityGroups = await query
            .GroupBy(t => t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byPriority = priorityGroups.ToDictionary(
            g => g.Priority.HasValue ? g.Priority.Value.ToString() : "Unassigned",
            g => g.Count);

        // Phân bố theo danh mục
        var categoryGroups = await query
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byCategory = categoryGroups.ToDictionary(g => g.CategoryId, g => g.Count);

        // Phân bố khối lượng công việc theo Kỹ thuật viên
        var assigneeGroups = await query
            .Where(t => t.AssignedToId != null)
            .GroupBy(t => t.AssignedToId!.Value)
            .Select(g => new
            {
                AgentId = g.Key,
                ActiveCount = g.Count(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled),
                ResolvedCount = g.Count(t => t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed)
            })
            .ToListAsync(cancellationToken);

        var workloads = new List<AgentWorkloadDto>(assigneeGroups.Count);
        foreach (var group in assigneeGroups)
        {
            var name = await agentDirectory.GetDisplayNameAsync(group.AgentId, cancellationToken);
            workloads.Add(new AgentWorkloadDto(group.AgentId, name, group.ActiveCount, group.ResolvedCount));
        }

        return new TicketDistributionReportDto(byStatus, byPriority, byCategory, workloads);
    }
}
