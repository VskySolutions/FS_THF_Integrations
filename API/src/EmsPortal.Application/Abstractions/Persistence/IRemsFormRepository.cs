using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// Data access for the REMS customer-facing form and its drafts, submissions and email events
/// (WO-110). Submissions and email events are append-only.
/// </summary>
public interface IRemsFormRepository
{
    /// <summary>The form with its drafts, submissions and email events loaded.</summary>
    Task<REMSForm?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

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
}
