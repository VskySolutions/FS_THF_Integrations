using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRepository : INotificationRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public NotificationRepository(EmsPortalDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
        => _dbContext.Notifications.AddAsync(notification, cancellationToken).AsTask();

    public void Update(Notification notification) => _dbContext.Notifications.Update(notification);

    public Task<Notification?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

    public async Task<(IReadOnlyList<Notification> Items, int Total)> ListAsync(
        Guid userId, bool? isRead, NotificationType? type, string? search,
        DateTime? createdFromUtc, DateTime? createdToUtc,
        int page, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notifications.Where(n => n.UserId == userId);
        if (isRead is { } read)
        {
            query = query.Where(n => n.IsRead == read);
        }
        if (type is { } t)
        {
            query = query.Where(n => n.Type == t);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Title and body together: the headline names the thing, the body carries the record number
            // people actually search for ("REMS-0042").
            var term = search.Trim();
            query = query.Where(n => n.Title.Contains(term) || n.Body.Contains(term));
        }
        if (createdFromUtc is { } from)
        {
            query = query.Where(n => n.CreatedOnUtc >= from);
        }
        if (createdToUtc is { } to)
        {
            query = query.Where(n => n.CreatedOnUtc <= to);
        }

        var ordered = query.OrderByDescending(n => n.CreatedOnUtc);
        var total = await ordered.CountAsync(cancellationToken);
        var items = await ordered.Skip((page - 1) * limit).Take(limit).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _dbContext.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(cancellationToken);

    public Task<bool> HasRecentDuplicateAsync(
        Guid userId, NotificationType type, EntityType? entityType, Guid? entityId, DateTime sinceUtc, CancellationToken cancellationToken = default)
        => _dbContext.Notifications.AnyAsync(
            n => n.UserId == userId
                && n.Type == type
                && n.EntityType == entityType
                && n.EntityId == entityId
                && n.CreatedOnUtc >= sinceUtc,
            cancellationToken);

    public async Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _dbContext.NotificationPreferences.Where(p => p.UserId == userId).ToListAsync(cancellationToken);

    public Task<NotificationPreference?> GetPreferenceAsync(Guid userId, NotificationType type, CancellationToken cancellationToken = default)
        => _dbContext.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationType == type, cancellationToken);

    public Task AddPreferenceAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
        => _dbContext.NotificationPreferences.AddAsync(preference, cancellationToken).AsTask();

    public void UpdatePreference(NotificationPreference preference) => _dbContext.NotificationPreferences.Update(preference);
}
