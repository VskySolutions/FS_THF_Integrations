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
            .OrderByDescending(r => r.UpdatedOnUtc)
            .ThenByDescending(r => r.CreatedOnUtc)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<REMS> Items, int Total)> ListRequestsAsync(
        RemsRequestListOptions options, CancellationToken cancellationToken = default)
    {
        var query = ApplyFieldFilters(ApplyScope(ApplyVisibility(options), options), options);

        var total = await query.CountAsync(cancellationToken);
        // Most-recently-touched first, so a request that anything has moved on — a form sent, an
        // engagement edited, an approval decided — surfaces at the top rather than staying wherever its
        // creation date put it. CreatedOnUtc breaks ties for rows written in the same tick.
        var items = await query
            .OrderByDescending(r => r.UpdatedOnUtc)
            .ThenByDescending(r => r.CreatedOnUtc)
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

        // A DRAFT used to be its author's alone even to the admins, on the reading that an unfinished
        // referral nobody has been pointed at is nobody else's business. It is the admins' business: a
        // referral that stalls half-written stalls invisibly, and the person whose job it is to keep the
        // pipeline moving could not see it, let alone finish it. So a REMS Admin now sees the whole tenant,
        // drafts included, and may work one (RemsSetupAccess.CanWork) — the list's Created By Me / All
        // toggle is what keeps that out of their own way, and it defaults to their own work.
        return options.CallerIsPrivileged
            // Admin / Super Admin: every request in the tenant, whatever stage it is at and whoever raised it.
            ? _dbContext.Rems.AsQueryable()
            // Partner-only: own drafts and drafts naming them; non-draft when created or involved.
            //
            // OnBehalfOfUserId is half of "own" here, and was missing: a delegate raising a request in a
            // shareholder's seat produces the SHAREHOLDER's request (RemsSetupAccess.IsInitiator and the
            // controller's IsMine both say so), but this predicate only ever asked who typed it, so the
            // principal could not see their own work in any list. The controller's CanSee already admitted
            // them, which made the gap the asymmetry its own docs warn against, running the wrong way: a
            // request GetById would open that no list would ever offer. The (TenantId, OnBehalfOfUserId,
            // Status) index exists for exactly this predicate.
            : _dbContext.Rems.Where(r =>
                (r.Status == draft
                    && (r.CreatedById == me || r.OnBehalfOfUserId == me || r.AdminAssignedToId == me)) ||
                (r.Status != draft
                    && (r.CreatedById == me || r.OnBehalfOfUserId == me
                        || r.AdminAssignedToId == me || r.CSEId == me)));
    }

    /// <summary>The requested VIEW, layered on top of the security predicate — never a substitute for it.</summary>
    private static IQueryable<REMS> ApplyScope(IQueryable<REMS> query, RemsRequestListOptions options)
    {
        var me = options.CallerUserId;
        const string draft = RemsRequestStatuses.Draft;

        if (options.Scope == RemsListScope.Partner)
        {
            // "All" is the whole of what the caller may see — for a REMS Admin the tenant's requests,
            // other people's drafts included; for everybody else, everything they created or are named on.
            // It narrows nothing on top of the visibility predicate, which is what makes it "all".
            //
            // "Created By Me" is authorship and nothing else: raised BY the caller, or FOR them by a
            // delegate acting in their seat (the same pair the rest of REMS calls IsMine — a delegate's
            // work is the principal's work). It deliberately excludes the requests that merely NAME the
            // caller as CSE or reviewing admin. Those are somebody else's referral that landed on their
            // desk, and a view labelled by who created something cannot be the view that lists them.
            return options.Ownership == RemsListOwnership.All
                ? query
                : query.Where(r => r.CreatedById == me || r.OnBehalfOfUserId == me);
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
            // Against the name WITH its suffix as well as without: the list shows "John Smith Jr.", so
            // typing what is on the row has to find the row. (ClientDisplayName says the same thing but
            // is [NotMapped] and cannot cross into SQL.)
            query = query.Where(r =>
                r.RequestedClientName.Contains(t)
                || (r.RequestedClientName + " " + r.ClientNameSuffix).Contains(t));
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
        if (!string.IsNullOrWhiteSpace(options.Type))
        {
            var t = options.Type.Trim();
            query = query.Where(r => r.Type == t);
        }
        if (options.AssignedAdminUserId is { } adminId)
        {
            query = query.Where(r => r.AdminAssignedToId == adminId);
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
                f.Submissions.Any(),
                f.InviteCode))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(REMS rems, CancellationToken cancellationToken = default)
        => await _dbContext.Rems.AddAsync(rems, cancellationToken);

    public void Update(REMS rems) => _dbContext.Rems.Update(rems);

    public void Remove(REMS rems) => _dbContext.Rems.Remove(rems);

    public async Task AddFileAsync(REMSFiles file, CancellationToken cancellationToken = default)
        => await _dbContext.RemsFiles.AddAsync(file, cancellationToken);

    public void RemoveFile(REMSFiles file) => _dbContext.RemsFiles.Remove(file);

    public async Task AddAdditionalEntityAsync(REMSAdditionalEntity additionalEntity, CancellationToken cancellationToken = default)
        => await _dbContext.RemsAdditionalEntities.AddAsync(additionalEntity, cancellationToken);

    public async Task<IReadOnlyList<REMSAdditionalEntity>> ListAdditionalEntitiesAsync(Guid remsId, CancellationToken cancellationToken = default)
        => await _dbContext.RemsAdditionalEntities
            .Where(a => a.REMSId == remsId)
            .OrderBy(a => a.CreatedOnUtc)
            .ToListAsync(cancellationToken);

    public Task<REMSAdditionalEntity?> GetAdditionalEntityAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.RemsAdditionalEntities.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetNumbersAsync(
        IReadOnlyCollection<Guid> remsIds, CancellationToken cancellationToken = default)
        => remsIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.Rems
                .Where(r => remsIds.Contains(r.Id))
                .Select(r => new { r.Id, r.REMSNumber })
                .ToDictionaryAsync(r => r.Id, r => r.REMSNumber, cancellationToken);

    public void UpdateAdditionalEntity(REMSAdditionalEntity additionalEntity)
        => _dbContext.RemsAdditionalEntities.Update(additionalEntity);

    public async Task AddSendBackAsync(REMSSendBack sendBack, CancellationToken cancellationToken = default)
        => await _dbContext.RemsSendBacks.AddAsync(sendBack, cancellationToken);

    public Task<REMSSendBack?> GetOpenSendBackAsync(Guid remsId, CancellationToken cancellationToken = default)
        => _dbContext.RemsSendBacks
            .FirstOrDefaultAsync(s => s.REMSId == remsId && s.ResolvedOnUtc == null, cancellationToken);

    public async Task<IReadOnlyList<REMSSendBack>> ListSendBacksAsync(Guid remsId, CancellationToken cancellationToken = default)
        => await _dbContext.RemsSendBacks
            .Where(s => s.REMSId == remsId)
            .OrderBy(s => s.CreatedOnUtc)
            .ToListAsync(cancellationToken);

    public void UpdateSendBack(REMSSendBack sendBack) => _dbContext.RemsSendBacks.Update(sendBack);

    public Task<int> CountActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _dbContext.Rems
            .IgnoreQueryFilters()
            .CountAsync(r => r.TenantId == tenantId && !r.Deleted, cancellationToken);

    public Task<bool> NumberExistsAsync(Guid tenantId, string number, CancellationToken cancellationToken = default)
        => _dbContext.Rems
            .IgnoreQueryFilters()
            .AnyAsync(r => r.TenantId == tenantId && !r.Deleted && r.REMSNumber == number, cancellationToken);

    public Task<bool> IsClientPersonSharedAsync(Guid personId, Guid excludingRemsId, CancellationToken cancellationToken = default)
        => _dbContext.Rems
            .AnyAsync(
                r => r.Id != excludingRemsId
                    && (r.ClientPersonId == personId || r.ExistingClientReferenceId == personId),
                cancellationToken);
}
