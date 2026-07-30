using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// The EMS-Inbox query for the REMS form pipeline (WO-112). Every request that has a
/// <see cref="REMSForm"/> is a candidate row; the optional <see cref="FormState"/> narrows to a single
/// <see cref="RemsFormStatus"/>. Tenant isolation is applied by the ambient query filter.
/// </summary>
public sealed record RemsInboxQuery(
    RemsFormStatus? FormState,
    int Page,
    int Limit);

/// <summary>
/// One EMS-Inbox row (WO-112): a request that has a form, projected with the request context, the form
/// state, its creator, and the latest email-delivery event (send/delivered/opened/failed). User ids are
/// resolved to display names by the controller.
/// </summary>
public sealed record RemsInboxItem(
    Guid RemsId,
    string RemsNumber,
    string ClientName,
    string EngagementType,
    string RequestStatus,
    RemsFormStatus FormStatus,
    DateTime FormModifiedOnUtc,
    Guid FormCreatedByUserId,
    DateTime? FormSentOnUtc,
    RemsFormEmailEventType? LatestEmailEventType,
    DateTime? LatestEmailEventOnUtc);

/// <summary>
/// Data access for the REMS customer-facing form and its drafts, submissions and email events
/// (WO-110). Submissions and email events are append-only.
/// </summary>
public interface IRemsFormRepository
{
    /// <summary>The form with its drafts, submissions and email events loaded.</summary>
    Task<REMSForm?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The active form (with its email events) for a request, or null when none has been built (WO-112).</summary>
    Task<REMSForm?> GetByRemsIdAsync(Guid remsId, CancellationToken cancellationToken = default);

    /// <summary>The active form for a tenant's invite code (public link resolution).</summary>
    Task<REMSForm?> GetByInviteCodeAsync(Guid tenantId, string inviteCode, CancellationToken cancellationToken = default);

    /// <summary>Whether an invite code is already taken (active) for the tenant.</summary>
    Task<bool> InviteCodeExistsAsync(Guid tenantId, string inviteCode, CancellationToken cancellationToken = default);

    Task AddAsync(REMSForm form, CancellationToken cancellationToken = default);

    void Update(REMSForm form);

    void Remove(REMSForm form);

    Task AddDraftAsync(REMSFormDraft draft, CancellationToken cancellationToken = default);

    void UpdateDraft(REMSFormDraft draft);

    Task AddSubmissionAsync(REMSFormSubmission submission, CancellationToken cancellationToken = default);

    Task AddEmailEventAsync(REMSFormEmailEvent emailEvent, CancellationToken cancellationToken = default);

    /// <summary>Email-delivery events for a form, newest first (WO-112 email log, AC-REMS-008.6).</summary>
    Task<IReadOnlyList<REMSFormEmailEvent>> ListEmailEventsAsync(Guid remsFormId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The paginated EMS Inbox: every request that has a form, newest-modified first, with the latest
    /// email-event state (WO-112, AC-REMS-009). Tenant-scoped by the ambient query filter.
    /// </summary>
    Task<(IReadOnlyList<RemsInboxItem> Items, int Total)> ListInboxAsync(
        RemsInboxQuery query, CancellationToken cancellationToken = default);
}
