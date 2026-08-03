using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Email;

/// <summary>
/// A delivery-tracked email that never reached its recipient. <paramref name="MessageId"/> is the
/// outbound Message-ID pinned at enqueue time (WO-121), which is what correlates the failure back to the
/// record whose log it belongs in.
/// </summary>
public sealed record EmailDeliveryFailure(
    Guid TenantId,
    EmailTemplateKey Key,
    string MessageId,
    string? Recipient,
    EmailDeliveryFailureReason Reason,
    string? Detail);

/// <summary>
/// Receives the failures the best-effort email pipeline would otherwise only write to the log. Keeps
/// <c>EmailNotificationService</c> free of any per-feature knowledge: it reports a failed Message-ID and
/// the sink decides whose delivery log that belongs in.
/// <para>
/// Implementations must never throw — a failure to record a failure must not escalate into one.
/// </para>
/// </summary>
public interface IEmailDeliveryFailureSink
{
    Task RecordAsync(EmailDeliveryFailure failure, CancellationToken cancellationToken = default);
}
