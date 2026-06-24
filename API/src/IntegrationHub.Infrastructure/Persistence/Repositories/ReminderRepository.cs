using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class ReminderRepository : IReminderRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public ReminderRepository(IntegrationHubDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(Reminder reminder, CancellationToken cancellationToken = default)
        => _dbContext.Reminders.AddAsync(reminder, cancellationToken).AsTask();

    public Task<Reminder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Reminders.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public void Update(Reminder reminder) => _dbContext.Reminders.Update(reminder);

    public void Remove(Reminder reminder) => _dbContext.Reminders.Remove(reminder);

    public async Task<(IReadOnlyList<Reminder> Items, int Total)> ListByUserAsync(Guid userId, int page, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Reminders.Where(r => r.UserId == userId).OrderBy(r => r.DueAtUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * limit).Take(limit).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<Reminder>> GetDueUndispatchedAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
        => await _dbContext.Reminders
            .Where(r => !r.IsDispatched && r.DueAtUtc <= nowUtc)
            .OrderBy(r => r.DueAtUtc)
            .ToListAsync(cancellationToken);
}
