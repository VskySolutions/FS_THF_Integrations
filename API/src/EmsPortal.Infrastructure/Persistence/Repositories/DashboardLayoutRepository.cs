using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class DashboardLayoutRepository : IDashboardLayoutRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public DashboardLayoutRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<DashboardLayout?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.DashboardLayouts.FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);

    public async Task UpsertAsync(DashboardLayout layout, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.DashboardLayouts.FirstOrDefaultAsync(d => d.UserId == layout.UserId, cancellationToken);
        if (existing is null)
        {
            await _dbContext.DashboardLayouts.AddAsync(layout, cancellationToken);
            return;
        }

        existing.WidgetOrderJson = layout.WidgetOrderJson;
        existing.HiddenWidgetsJson = layout.HiddenWidgetsJson;
        existing.CollapsedWidgetsJson = layout.CollapsedWidgetsJson;
        _dbContext.DashboardLayouts.Update(existing);
    }
}
