using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>Data access for in-app <see cref="Notification"/>s and per-user delivery preferences.</summary>
public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    void Update(Notification notification);

    Task<Notification?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Notification> Items, int Total)> ListAsync(
        Guid userId, bool? isRead, NotificationType? type, int page, int limit, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> GetUnreadAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>True when an equivalent notification was created for the user since <paramref name="sinceUtc"/>.</summary>
    Task<bool> HasRecentDuplicateAsync(
        Guid userId, NotificationType type, EntityType? entityType, Guid? entityId, DateTime sinceUtc, CancellationToken cancellationToken = default);

    // ---- Preferences ----
    Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<NotificationPreference?> GetPreferenceAsync(Guid userId, NotificationType type, CancellationToken cancellationToken = default);

    Task AddPreferenceAsync(NotificationPreference preference, CancellationToken cancellationToken = default);

    void UpdatePreference(NotificationPreference preference);
}
