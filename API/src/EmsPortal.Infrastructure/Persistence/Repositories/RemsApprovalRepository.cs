using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class RemsApprovalRepository : IRemsApprovalRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public RemsApprovalRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<REMSApprovalRound?> GetRoundByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.RemsApprovalRounds
            .Include(r => r.Tasks).ThenInclude(t => t.ChecklistItems)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<REMSApprovalRound>> GetRoundsByEngagementAsync(Guid engagementId, CancellationToken cancellationToken = default)
        => await _dbContext.RemsApprovalRounds
            .Include(r => r.Tasks).ThenInclude(t => t.ChecklistItems)
            .Where(r => r.REMSEngagementId == engagementId)
            .OrderByDescending(r => r.RoundNumber)
            .ToListAsync(cancellationToken);

    public async Task<int> GetNextRoundNumberAsync(Guid engagementId, CancellationToken cancellationToken = default)
    {
        // Round numbers are immutable history and must never be reused, so include soft-deleted rows.
        var max = await _dbContext.RemsApprovalRounds
            .IgnoreQueryFilters()
            .Where(r => r.REMSEngagementId == engagementId)
            .Select(r => (int?)r.RoundNumber)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    public Task<REMSApprovalTask?> GetTaskByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.RemsApprovalTasks
            .Include(t => t.ChecklistItems)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<REMSApprovalTask?> GetTaskWithContextAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.RemsApprovalTasks
            .Include(t => t.ChecklistItems)
            .Include(t => t.Round).ThenInclude(r => r!.Tasks)
            .Include(t => t.Round).ThenInclude(r => r!.Engagement).ThenInclude(e => e!.CommissionSplits)
            .Include(t => t.Round).ThenInclude(r => r!.Engagement).ThenInclude(e => e!.MarketingMethods)
            .Include(t => t.Round).ThenInclude(r => r!.Engagement).ThenInclude(e => e!.Entity)
                .ThenInclude(en => en!.Client).ThenInclude(c => c!.Rems)
            .Include(t => t.Round).ThenInclude(r => r!.Engagement).ThenInclude(e => e!.Entity)
                .ThenInclude(en => en!.Client).ThenInclude(c => c!.Entities)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<REMSApprovalTask>> ListTasksByApproverAsync(Guid approverId, CancellationToken cancellationToken = default)
        => await _dbContext.RemsApprovalTasks
            .Include(t => t.ChecklistItems)
            .Include(t => t.Round).ThenInclude(r => r!.Engagement).ThenInclude(e => e!.Entity)
                .ThenInclude(en => en!.Client).ThenInclude(c => c!.Rems)
            .Where(t => t.ApproverId == approverId)
            .OrderByDescending(t => t.Round!.SentOnUtc)
            .ToListAsync(cancellationToken);

    public async Task AddRoundAsync(REMSApprovalRound round, CancellationToken cancellationToken = default)
        => await _dbContext.RemsApprovalRounds.AddAsync(round, cancellationToken);

    public void UpdateRound(REMSApprovalRound round) => _dbContext.RemsApprovalRounds.Update(round);

    public async Task AddTaskAsync(REMSApprovalTask task, CancellationToken cancellationToken = default)
        => await _dbContext.RemsApprovalTasks.AddAsync(task, cancellationToken);

    public void UpdateTask(REMSApprovalTask task) => _dbContext.RemsApprovalTasks.Update(task);

    public async Task AddChecklistItemAsync(REMSApprovalChecklistItem item, CancellationToken cancellationToken = default)
        => await _dbContext.RemsApprovalChecklistItems.AddAsync(item, cancellationToken);

    public void UpdateChecklistItem(REMSApprovalChecklistItem item) => _dbContext.RemsApprovalChecklistItems.Update(item);
}
