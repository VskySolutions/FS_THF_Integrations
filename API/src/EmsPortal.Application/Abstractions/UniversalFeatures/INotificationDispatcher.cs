using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.UniversalFeatures;

/// <summary>The payload for raising one in-app notification.</summary>
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
/// Stages an in-app <see cref="Domain.Entities.Notification"/> for the recipient. Honours the per-user
/// in-app <see cref="Domain.Entities.NotificationPreference"/> and dedupes duplicate notifications within
/// a 60-second window. In-app only since WO-124 — notification types are never emailed; external email is
/// limited to the account-security and REMS templates on the email allowlist.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>Stages the in-app notification per the user's in-app preference.</summary>
    Task DispatchAsync(CreateNotificationDto notification, CancellationToken cancellationToken = default);
}
