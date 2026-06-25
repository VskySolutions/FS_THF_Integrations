using IntegrationHub.Application.Abstractions.Email;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Application.Abstractions.UniversalFeatures;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Application.UniversalFeatures;

/// <summary>
/// Default <see cref="INotificationDispatcher"/>. Stages an in-app <see cref="Notification"/> (honouring
/// the user's per-type preference), dedupes duplicates within a 60-second window, and best-effort sends
/// an email via the tenant's active SMTP account. Email failures are swallowed so the triggering
/// operation is never blocked.
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    /// <summary>Duplicate-suppression window — repeated notifications inside it are grouped (no email).</summary>
    private static readonly TimeSpan GroupingWindow = TimeSpan.FromSeconds(60);

    private readonly INotificationRepository _notifications;
    private readonly IUserRepository _users;
    private readonly IEmailDispatcher _emailDispatcher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationRepository notifications,
        IUserRepository users,
        IEmailDispatcher emailDispatcher,
        ITenantContext tenantContext,
        ILogger<NotificationDispatcher> logger)
    {
        _notifications = notifications;
        _users = users;
        _emailDispatcher = emailDispatcher;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task DispatchAsync(CreateNotificationDto notification, CancellationToken cancellationToken = default)
    {
        var preference = await _notifications.GetPreferenceAsync(notification.UserId, notification.Type, cancellationToken);
        var inApp = preference?.InApp ?? true;
        var email = preference?.Email ?? true;

        var isGrouped = await _notifications.HasRecentDuplicateAsync(
            notification.UserId, notification.Type, notification.EntityType, notification.EntityId,
            DateTime.UtcNow - GroupingWindow, cancellationToken);

        if (inApp)
        {
            await _notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = notification.UserId,
                Type = notification.Type,
                Title = notification.Title,
                Body = notification.Body,
                EntityType = notification.EntityType,
                EntityId = notification.EntityId,
                IsRead = false,
                IsGrouped = isGrouped,
            }, cancellationToken);
        }

        // Email fan-out is best-effort and skipped for grouped duplicates to avoid flooding.
        if (email && !isGrouped && _tenantContext.IsResolved && MapTemplate(notification.Type) is { } templateKey)
        {
            await SendEmailAsync(notification, templateKey, cancellationToken);
        }
    }

    private async Task SendEmailAsync(CreateNotificationDto notification, EmailTemplateKey templateKey, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _users.GetByIdAsync(notification.UserId, cancellationToken);
            if (user is null || string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            var names = await _users.GetFullNamesAsync(new[] { notification.UserId }, cancellationToken);
            var fullName = names.TryGetValue(notification.UserId, out var n) ? n : user.DisplayName;

            _emailDispatcher.Enqueue(
                _tenantContext.TenantId,
                templateKey,
                user.Email,
                new Dictionary<string, string?>
                {
                    ["FullName"] = fullName,
                    ["Title"] = notification.Title,
                    ["Body"] = notification.Body,
                });
        }
        catch (Exception ex)
        {
            // Best-effort: a failed notification email must never block the triggering operation.
            _logger.LogWarning(ex, "Failed to send notification email to user {UserId} for {Type}.", notification.UserId, notification.Type);
        }
    }

    private static EmailTemplateKey? MapTemplate(NotificationType type) => type switch
    {
        NotificationType.Mention => EmailTemplateKey.MentionReceived,
        NotificationType.ReminderDue => EmailTemplateKey.ReminderDue,
        _ => null,
    };
}
