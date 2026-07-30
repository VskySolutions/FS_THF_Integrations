using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class RemsFormRepository : IRemsFormRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public RemsFormRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<REMSForm?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.RemsForms
            .Include(f => f.Drafts)
            .Include(f => f.Submissions)
            .Include(f => f.EmailEvents)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<REMSForm?> GetByRemsIdAsync(Guid remsId, CancellationToken cancellationToken = default)
        // At most one active form per request (filtered unique index on (TenantId, REMSId)); tenant + soft-delete
        // scoped by the ambient query filter. Email events are loaded so callers can read sent/locked state.
        => _dbContext.RemsForms
            .Include(f => f.EmailEvents)
            .FirstOrDefaultAsync(f => f.REMSId == remsId, cancellationToken);

    public Task<REMSForm?> GetByInviteCodeAsync(Guid tenantId, string inviteCode, CancellationToken cancellationToken = default)
        => _dbContext.RemsForms
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && !f.Deleted && f.InviteCode == inviteCode, cancellationToken);

    public Task<REMSForm?> GetByInviteCodeUnscopedAsync(string inviteCode, CancellationToken cancellationToken = default)
        // Public form (no tenant context): resolve by invite code across tenants (IgnoreQueryFilters), and
        // include the owning request — even soft-deleted — plus the drafts so the caller can inspect request
        // state and upsert/submit. Tracked so the submit transaction can update the form + request.
        => _dbContext.RemsForms
            .IgnoreQueryFilters()
            .Include(f => f.Rems)
            .Include(f => f.Drafts)
            .FirstOrDefaultAsync(f => !f.Deleted && f.InviteCode == inviteCode, cancellationToken);

    public Task<bool> InviteCodeExistsAsync(Guid tenantId, string inviteCode, CancellationToken cancellationToken = default)
        => _dbContext.RemsForms
            .IgnoreQueryFilters()
            .AnyAsync(f => f.TenantId == tenantId && !f.Deleted && f.InviteCode == inviteCode, cancellationToken);

    public async Task AddAsync(REMSForm form, CancellationToken cancellationToken = default)
        => await _dbContext.RemsForms.AddAsync(form, cancellationToken);

    public void Update(REMSForm form) => _dbContext.RemsForms.Update(form);

    public void Remove(REMSForm form) => _dbContext.RemsForms.Remove(form);

    public async Task AddDraftAsync(REMSFormDraft draft, CancellationToken cancellationToken = default)
        => await _dbContext.RemsFormDrafts.AddAsync(draft, cancellationToken);

    public void UpdateDraft(REMSFormDraft draft) => _dbContext.RemsFormDrafts.Update(draft);

    public async Task AddSubmissionAsync(REMSFormSubmission submission, CancellationToken cancellationToken = default)
        => await _dbContext.RemsFormSubmissions.AddAsync(submission, cancellationToken);

    public async Task AddEmailEventAsync(REMSFormEmailEvent emailEvent, CancellationToken cancellationToken = default)
        => await _dbContext.RemsFormEmailEvents.AddAsync(emailEvent, cancellationToken);

    public async Task<IReadOnlyList<REMSFormEmailEvent>> ListEmailEventsAsync(Guid remsFormId, CancellationToken cancellationToken = default)
        => await _dbContext.RemsFormEmailEvents
            .Where(e => e.REMSFormId == remsFormId)
            .OrderByDescending(e => e.OccurredOnUtc)
            .ThenByDescending(e => e.CreatedOnUtc)
            .ToListAsync(cancellationToken);

    public Task<REMSFormEmailEvent?> GetSentEventByProviderMessageIdUnscopedAsync(
        string providerMessageId, CancellationToken cancellationToken = default)
        // No tenant/user context on a webhook: ignore query filters so the anchor resolves across tenants,
        // and read-only (AsNoTracking) since we only need its TenantId + REMSFormId.
        => _dbContext.RemsFormEmailEvents
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.ProviderMessageId == providerMessageId && e.EventType == RemsFormEmailEventType.Sent,
                cancellationToken);

    public Task<bool> EmailEventExistsAsync(
        Guid tenantId, string providerMessageId, RemsFormEmailEventType eventType, CancellationToken cancellationToken = default)
        => _dbContext.RemsFormEmailEvents
            .IgnoreQueryFilters()
            .AnyAsync(
                e => e.TenantId == tenantId && e.ProviderMessageId == providerMessageId && e.EventType == eventType,
                cancellationToken);

    public async Task<bool> TryAppendProviderEmailEventAsync(
        REMSFormEmailEvent emailEvent, CancellationToken cancellationToken = default)
    {
        await _dbContext.RemsFormEmailEvents.AddAsync(emailEvent, cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // A concurrent post beat us to the filtered unique index (TenantId, ProviderMessageId, EventType).
            // Detach the rejected entity so the shared context stays usable for the remaining events.
            _dbContext.Entry(emailEvent).State = EntityState.Detached;
            return false;
        }
    }

    // SQL Server duplicate-key errors: 2627 (unique constraint) / 2601 (unique index).
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException is SqlException sql
            && sql.Errors.Cast<SqlError>().Any(e => e.Number is 2601 or 2627);

    public async Task<(IReadOnlyList<RemsInboxItem> Items, int Total)> ListInboxAsync(
        RemsInboxQuery query, CancellationToken cancellationToken = default)
    {
        // Every request that has a form is a candidate; the request must resolve through the (soft-delete +
        // tenant) filter on REMS. Tenant isolation on the form itself is ambient.
        var forms = _dbContext.RemsForms.Where(f => f.Rems != null);
        if (query.FormState is { } state)
        {
            forms = forms.Where(f => f.Status == state);
        }

        var total = await forms.CountAsync(cancellationToken);

        // Project the latest email event via an anonymous-type subquery (FirstOrDefault => null when the
        // form has no events yet), then shape the record in memory.
        var rows = await forms
            .OrderByDescending(f => f.UpdatedOnUtc)
            .ThenByDescending(f => f.CreatedOnUtc)
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .Select(f => new
            {
                f.REMSId,
                f.Rems!.REMSNumber,
                ClientName = f.Rems!.RequestedClientName,
                EngagementType = f.Rems!.Type,
                RequestStatus = f.Rems!.Status,
                f.Status,
                f.UpdatedOnUtc,
                f.CreatedByUserId,
                f.SentOnUtc,
                LatestEvent = f.EmailEvents
                    .OrderByDescending(e => e.OccurredOnUtc)
                    .Select(e => new { e.EventType, e.OccurredOnUtc })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new RemsInboxItem(
                x.REMSId,
                x.REMSNumber,
                x.ClientName,
                x.EngagementType,
                x.RequestStatus,
                x.Status,
                x.UpdatedOnUtc,
                x.CreatedByUserId,
                x.SentOnUtc,
                x.LatestEvent is null ? null : x.LatestEvent.EventType,
                x.LatestEvent is null ? null : x.LatestEvent.OccurredOnUtc))
            .ToList();

        return (items, total);
    }
}
