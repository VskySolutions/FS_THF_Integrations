using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.UniversalFeatures;

/// <summary>The payload for raising one in-app (and optionally email) notification.</summary>
/// <param name="UserId">The recipient user.</param>
/// <param name="Type">The notification category (drives the preference matrix).</param>
/// <param name="Title">Short headline.</param>
/// <param name="Body">Body / detail text.</param>
/// <param name="EntityType">Optional entity the notification deep-links to.</param>
/// <param name="EntityId">Optional id of the linked entity.</param>
public sealed record CreateNotificationDto(
    Guid UserId,
    NotificationType Type,
    string Title,
    string Body,
    EntityType? EntityType = null,
    Guid? EntityId = null);

/// <summary>
/// Fans a notification out to the recipient's enabled channels: an in-app
/// <see cref="Domain.Entities.Notification"/> record plus a best-effort email via the tenant's active
/// SMTP account. Honours per-user <see cref="Domain.Entities.NotificationPreference"/> and dedupes
/// duplicate notifications within a 60-second window. Sending is best-effort — failures are logged and
/// never block the triggering operation.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>Stages the in-app notification and dispatches email (best-effort) per user preference.</summary>
    Task DispatchAsync(CreateNotificationDto notification, CancellationToken cancellationToken = default);
}
