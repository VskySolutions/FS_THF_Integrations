using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Application.Abstractions.UniversalFeatures;
using IntegrationHub.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Jobs;

/// <summary>
/// Recurring (1-minute) sweep that dispatches due reminders. It runs without a resolved tenant so the
/// ambient query filter is a no-op and it sees every tenant's due reminders; it then sets the tenant
/// context per reminder so the in-app notification is stamped and the email resolves the right SMTP
/// account. Each reminder is marked dispatched (and overdue) so it fires only once.
/// </summary>
public sealed class ReminderDispatchJob
{
    /// <summary>The Hangfire recurring job id.</summary>
    public const string Name = "uf-reminder-dispatch";

    private readonly IReminderRepository _reminders;
    private readonly INotificationDispatcher _notifications;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReminderDispatchJob> _logger;

    public ReminderDispatchJob(
        IReminderRepository reminders,
        INotificationDispatcher notifications,
        ITenantContext tenantContext,
        IUnitOfWork unitOfWork,
        ILogger<ReminderDispatchJob> logger)
    {
        _reminders = reminders;
        _notifications = notifications;
        _tenantContext = tenantContext;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        // Loaded before any tenant context is set, so this spans all tenants.
        var due = await _reminders.GetDueUndispatchedAsync(now, cancellationToken);
        if (due.Count == 0)
        {
            return;
        }

        var dispatched = 0;
        foreach (var reminder in due)
        {
            try
            {
                // Scope subsequent writes/email to the reminder's tenant.
                _tenantContext.Set(reminder.TenantId, string.Empty);

                await _notifications.DispatchAsync(new CreateNotificationDto(
                    reminder.UserId,
                    NotificationType.ReminderDue,
                    "Reminder due",
                    string.IsNullOrWhiteSpace(reminder.Note) ? "You have a reminder on this record." : reminder.Note!,
                    reminder.EntityType,
                    reminder.EntityId), cancellationToken);

                reminder.IsDispatched = true;
                reminder.IsOverdue = reminder.DueAtUtc < now;
                _reminders.Update(reminder);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                dispatched++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch reminder {ReminderId}.", reminder.Id);
            }
        }

        _logger.LogInformation("Dispatched {Count} reminder(s).", dispatched);
    }
}
