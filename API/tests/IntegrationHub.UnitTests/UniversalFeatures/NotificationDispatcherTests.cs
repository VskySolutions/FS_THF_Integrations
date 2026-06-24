using FluentAssertions;
using IntegrationHub.Application.Abstractions.Email;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Application.Abstractions.UniversalFeatures;
using IntegrationHub.Application.UniversalFeatures;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace IntegrationHub.UnitTests.UniversalFeatures;

// WO-98: NotificationDispatcher honours preferences and the 60-second grouping window.
public class NotificationDispatcherTests
{
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IEmailNotificationService> _email = new();
    private readonly Mock<ITenantContext> _tenant = new();

    private NotificationDispatcher Create() => new(
        _notifications.Object, _users.Object, _email.Object, _tenant.Object, NullLogger<NotificationDispatcher>.Instance);

    private static CreateNotificationDto Dto(Guid userId) =>
        new(userId, NotificationType.Mention, "X mentioned you", "hello", EntityType.CustomerRequest, Guid.NewGuid());

    [Fact]
    public async Task In_app_disabled_preference_suppresses_the_notification()
    {
        var userId = Guid.NewGuid();
        _notifications.Setup(r => r.GetPreferenceAsync(userId, NotificationType.Mention, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationPreference { UserId = userId, NotificationType = NotificationType.Mention, InApp = false, Email = false });

        await Create().DispatchAsync(Dto(userId));

        _notifications.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Default_preference_creates_an_in_app_notification()
    {
        var userId = Guid.NewGuid();
        _notifications.Setup(r => r.GetPreferenceAsync(userId, It.IsAny<NotificationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null);
        _notifications.Setup(r => r.HasRecentDuplicateAsync(userId, It.IsAny<NotificationType>(), It.IsAny<EntityType?>(), It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Notification? captured = null;
        _notifications.Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => captured = n)
            .Returns(Task.CompletedTask);

        await Create().DispatchAsync(Dto(userId));

        captured.Should().NotBeNull();
        captured!.IsGrouped.Should().BeFalse();
    }

    [Fact]
    public async Task Duplicate_within_window_is_marked_grouped()
    {
        var userId = Guid.NewGuid();
        _notifications.Setup(r => r.GetPreferenceAsync(userId, It.IsAny<NotificationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null);
        _notifications.Setup(r => r.HasRecentDuplicateAsync(userId, It.IsAny<NotificationType>(), It.IsAny<EntityType?>(), It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Notification? captured = null;
        _notifications.Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => captured = n)
            .Returns(Task.CompletedTask);

        await Create().DispatchAsync(Dto(userId));

        captured.Should().NotBeNull();
        captured!.IsGrouped.Should().BeTrue();
    }
}
