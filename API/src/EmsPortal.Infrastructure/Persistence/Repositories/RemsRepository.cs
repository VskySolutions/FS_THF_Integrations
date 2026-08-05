using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class RemsRepository : IRemsRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public RemsRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<REMS?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Rems
            .Include(r => r.Files.Where(f => !f.Deleted))
                .ThenInclude(f => f.Media)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<REMS>> ListAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Rems
            .OrderByDescending(r => r.CreatedOnUtc)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<REMS> Items, int Total)> ListRequestsAsync(
        RemsRequestListOptions options, CancellationToken cancellationToken = default)
    {
        var query = ApplyFieldFilters(ApplyScope(ApplyVisibility(options), options), options);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.CreatedOnUtc)
            .Skip((options.Page - 1) * options.Limit)
            .Take(options.Limit)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <summary>
    /// How many pool requests fall into each of the Admin Pool's three views, in one round trip. Built on
    /// the SAME visibility predicate and field filters as <see cref="ListRequestsAsync"/> — minus the pool
    /// sub-filter, which is what is being counted — so the badge on a view can never disagree with the
    /// number of rows clicking it produces.
    /// </summary>
    public async Task<RemsPoolCounts> CountPoolScopesAsync(
        RemsRequestListOptions options, CancellationToken cancellationToken = default)
    {
        var me = options.CallerUserId;
        const string draft = RemsRequestStatuses.Draft;

        var query = ApplyFieldFilters(ApplyVisibility(options), options)
            .Where(r => r.Status != draft);

        // GroupBy over a constant collapses the three counts into a single aggregate query.
        var counts = await query
            .GroupBy(_ => 1)
            .Select(g => new RemsPoolCounts(
                g.Count(r => r.AdminAssignedToId == null),
                g.Count(r => r.AdminAssignedToId == me),
                g.Count()))
            .FirstOrDefaultAsync(cancellationToken);

        // No matching rows => no groups => no row at all, which is a legitimate all-zero result.
        return counts ?? new RemsPoolCounts(0, 0, 0);
    }

    /// <summary>
    /// The record-level visibility predicate (WO-111) — who may see which requests at all. Tenant
    /// isolation is ambient and applied on top of this by the DbContext query filter.
    /// </summary>
    private IQueryable<REMS> ApplyVisibility(RemsRequestListOptions options)
    {
        var me = options.CallerUserId;
        const string draft = RemsRequestStatuses.Draft;

        return options.CallerIsPrivileged
            // Admin / Super Admin: every non-draft tenant request, plus the caller's own drafts.
            ? _dbContext.Rems.Where(r => r.Status != draft || r.CreatedById == me)
            // Partner-only: own drafts; non-draft only when created or involved (creator/assignee/CSE).
            : _dbContext.Rems.Where(r =>
                (r.Status == draft && r.CreatedById == me) ||
                (r.Status != draft && (r.CreatedById == me || r.AdminAssignedToId == me || r.CSEId == me)));
    }

    /// <summary>The requested VIEW, layered on top of the security predicate — never a substitute for it.</summary>
    private static IQueryable<REMS> ApplyScope(IQueryable<REMS> query, RemsRequestListOptions options)
    {
        var me = options.CallerUserId;
        const string draft = RemsRequestStatuses.Draft;

        if (options.Scope == RemsListScope.Partner)
        {
            return query.Where(r => r.CreatedById == me || r.AdminAssignedToId == me || r.CSEId == me);
        }

        if (options.Scope != RemsListScope.Pool)
        {
            return query;
        }

        query = query.Where(r => r.Status != draft);
        return options.PoolFilter switch
        {
            RemsPoolFilter.Unassigned => query.Where(r => r.AdminAssignedToId == null),
            RemsPoolFilter.Mine => query.Where(r => r.AdminAssignedToId == me),
            _ => query,
        };
    }

    /// <summary>The user-supplied search/filter narrowing, shared by the list and its pool counts.</summary>
    private static IQueryable<REMS> ApplyFieldFilters(IQueryable<REMS> query, RemsRequestListOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ClientName))
        {
            var t = options.ClientName.Trim();
            query = query.Where(r => r.RequestedClientName.Contains(t));
        }
        if (!string.IsNullOrWhiteSpace(options.Contact))
        {
            var t = options.Contact.Trim();
            query = query.Where(r =>
                (r.CustomerEmail != null && r.CustomerEmail.Contains(t)) ||
                (r.CustomerMobileNumber != null && r.CustomerMobileNumber.Contains(t)));
        }
        if (!string.IsNullOrWhiteSpace(options.Status))
        {
            var t = options.Status.Trim();
            query = query.Where(r => r.Status == t);
        }
        if (options.CreatedFromUtc is { } from)
        {
            query = query.Where(r => r.CreatedOnUtc >= from);
        }
        if (options.CreatedToUtc is { } to)
        {
            query = query.Where(r => r.CreatedOnUtc <= to);
        }

        return query;
    }

    public async Task<IReadOnlyList<RemsFormStateInfo>> GetFormStatesAsync(
        IReadOnlyCollection<Guid> remsIds, CancellationToken cancellationToken = default)
    {
        if (remsIds.Count == 0)
        {
            return Array.Empty<RemsFormStateInfo>();
        }

        // At most one active form per request (filtered unique index on (TenantId, REMSId)).
        return await _dbContext.RemsForms
            .Where(f => remsIds.Contains(f.REMSId))
            .Select(f => new RemsFormStateInfo(
                f.REMSId,
                f.IndustryGroup,
                f.Status,
                f.SentOnUtc,
                f.SubmittedOnUtc,
                f.Submissions.Any()))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(REMS rems, CancellationToken cancellationToken = default)
        => await _dbContext.Rems.AddAsync(rems, cancellationToken);

    public void Update(REMS rems) => _dbContext.Rems.Update(rems);

    public void Remove(REMS rems) => _dbContext.Rems.Remove(rems);

    public async Task AddFileAsync(REMSFiles file, CancellationToken cancellationToken = default)
        => await _dbContext.RemsFiles.AddAsync(file, cancellationToken);

    public void RemoveFile(REMSFiles file) => _dbContext.RemsFiles.Remove(file);

    public Task<int> CountActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _dbContext.Rems
            .IgnoreQueryFilters()
            .CountAsync(r => r.TenantId == tenantId && !r.Deleted, cancellationToken);

    public Task<bool> NumberExistsAsync(Guid tenantId, string number, CancellationToken cancellationToken = default)
        => _dbContext.Rems
            .IgnoreQueryFilters()
            .AnyAsync(r => r.TenantId == tenantId && !r.Deleted && r.REMSNumber == number, cancellationToken);
}
