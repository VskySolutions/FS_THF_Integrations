using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>The dashboard "view" a request list is filtered to (WO-111).</summary>
public enum RemsListScope
{
    /// <summary>No view filter — every request the caller is allowed to see (record-level predicate only).</summary>
    All = 0,

    /// <summary>The partner "my requests" view: created or involved by the caller (AC-REMS-002.1).</summary>
    Partner = 1,

    /// <summary>The Admin Pool view: submitted (non-draft) requests.</summary>
    Pool = 2,
}

/// <summary>Additional Admin-Pool narrowing applied when <see cref="RemsListScope.Pool"/> is requested.</summary>
public enum RemsPoolFilter
{
    /// <summary>All pool requests, regardless of assignment.</summary>
    All = 0,

    /// <summary>Only pool requests with no admin assigned.</summary>
    Unassigned = 1,

    /// <summary>Only pool requests assigned to the caller.</summary>
    Mine = 2,
}

/// <summary>
/// The resolved query for a REMS request dashboard list (WO-111). Carries the caller identity and the
/// caller's privilege (Admin role or Super Admin) so the repository can apply the record-level
/// visibility predicate, plus the server-side filters and the view scope.
/// </summary>
public sealed record RemsRequestListOptions(
    Guid CallerUserId,
    bool CallerIsPrivileged,
    string? ClientName,
    string? Contact,
    string? Status,
    // Option-set CODE (REMS.Type), matched exactly — the label is the tenant's to
    // rename, the code is what the row stores.
    string? Type,
    /// <summary>A specific owning admin. Distinct from <see cref="PoolFilter"/>, which asks
    /// unassigned/mine/any rather than naming somebody.</summary>
    Guid? AssignedAdminUserId,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    RemsListScope Scope,
    RemsPoolFilter PoolFilter,
    int Page,
    int Limit);

/// <summary>
/// How many pool requests fall into each Admin Pool view, under the caller's visibility and the filters
/// currently applied. <see cref="All"/> is the total the other two are drawn from, not their sum: a
/// request assigned to someone else is in neither.
/// </summary>
public sealed record RemsPoolCounts(int Unassigned, int Mine, int All);

/// <summary>
/// The EMS-form and client-submission state for a request, projected from the (at most one active)
/// <see cref="REMSForm"/> and its submissions. Absent when the form has not been started (WO-111 left-join;
/// forms/submissions are populated by later WOs).
/// </summary>
public sealed record RemsFormStateInfo(
    Guid RemsId,
    string? IndustryGroup,
    RemsFormStatus? FormStatus,
    DateTime? FormSentOnUtc,
    DateTime? FormSubmittedOnUtc,
    bool HasSubmission);

/// <summary>
/// Data access for the REMS request aggregate root and its file links (WO-110). Tenant isolation is
/// applied by the DbContext global query filter; the number-generation helpers deliberately bypass it
/// to see every row for the tenant.
/// </summary>
public interface IRemsRepository
{
    /// <summary>The request with its file links (and media) loaded; tenant-scoped by the ambient filter.</summary>
    Task<REMS?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Active requests for the current tenant.</summary>
    Task<IReadOnlyList<REMS>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Paginated dashboard list. Applies the WO-111 record-level visibility predicate (drafts are
    /// creator-only; privileged callers see all non-draft; partner-only callers see created-or-involved)
    /// plus the requested filters and view scope, tenant-scoped by the ambient filter.
    /// </summary>
    Task<(IReadOnlyList<REMS> Items, int Total)> ListRequestsAsync(
        RemsRequestListOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts for the Admin Pool's Unassigned / Assigned-to-me / All views in one round trip, under the
    /// same visibility predicate and field filters as <see cref="ListRequestsAsync"/>. The options'
    /// <see cref="RemsRequestListOptions.PoolFilter"/> is ignored — it is what is being counted.
    /// </summary>
    Task<RemsPoolCounts> CountPoolScopesAsync(
        RemsRequestListOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// EMS-form / client-submission state for the given requests (one active form per request), for the
    /// dashboard rows. Requests with no form are simply absent from the result.
    /// </summary>
    Task<IReadOnlyList<RemsFormStateInfo>> GetFormStatesAsync(
        IReadOnlyCollection<Guid> remsIds, CancellationToken cancellationToken = default);

    Task AddAsync(REMS rems, CancellationToken cancellationToken = default);

    void Update(REMS rems);

    void Remove(REMS rems);

    Task AddFileAsync(REMSFiles file, CancellationToken cancellationToken = default);

    void RemoveFile(REMSFiles file);

    /// <summary>Stage another business the client named at intake (WO-116, initiator-first rebuild).</summary>
    Task AddAdditionalEntityAsync(REMSAdditionalEntity additionalEntity, CancellationToken cancellationToken = default);

    /// <summary>
    /// The other businesses declared on a request's intake, newest last. Backs the follow-up flag on the
    /// Partner/CSE list — rows with no <c>CreatedREMSId</c> are the ones still needing an EMS.
    /// </summary>
    Task<IReadOnlyList<REMSAdditionalEntity>> ListAdditionalEntitiesAsync(Guid remsId, CancellationToken cancellationToken = default);

    Task<REMSAdditionalEntity?> GetAdditionalEntityAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// REMS numbers for the given ids, keyed by id. Lets an additional-entity row link to the request it
    /// produced by NAME rather than only claiming one exists.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNumbersAsync(
        IReadOnlyCollection<Guid> remsIds, CancellationToken cancellationToken = default);

    void UpdateAdditionalEntity(REMSAdditionalEntity additionalEntity);

    /// <summary>Record an admin's return of a request to its initiator, with the reason they gave.</summary>
    Task AddSendBackAsync(REMSSendBack sendBack, CancellationToken cancellationToken = default);

    /// <summary>The still-open return for a request, or null when it is not currently back with its initiator.</summary>
    Task<REMSSendBack?> GetOpenSendBackAsync(Guid remsId, CancellationToken cancellationToken = default);

    /// <summary>Every return of a request, oldest first — the send-back half of its history.</summary>
    Task<IReadOnlyList<REMSSendBack>> ListSendBacksAsync(Guid remsId, CancellationToken cancellationToken = default);

    void UpdateSendBack(REMSSendBack sendBack);

    /// <summary>
    /// Count of active REMS requests for a tenant, ignoring the ambient query filter. Backs
    /// <c>REMS-{seq}</c> number generation (seq = count + 1).
    /// </summary>
    Task<int> CountActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a REMS number is already taken (active) for the tenant. Advisory only — the filtered
    /// unique index <c>(TenantId, REMSNumber) WHERE [Deleted] = 0</c> is the definitive guard.
    /// </summary>
    Task<bool> NumberExistsAsync(Guid tenantId, string number, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any request other than <paramref name="excludingRemsId"/> names this person as its client —
    /// either as the client person or as the existing-client it matched at intake. A person only one
    /// request has ever referred to is that request's to keep in step with; once a second request points
    /// at them they are a shared client record, and editing one request must not rename them underneath
    /// the others.
    /// </summary>
    Task<bool> IsClientPersonSharedAsync(Guid personId, Guid excludingRemsId, CancellationToken cancellationToken = default);

}
