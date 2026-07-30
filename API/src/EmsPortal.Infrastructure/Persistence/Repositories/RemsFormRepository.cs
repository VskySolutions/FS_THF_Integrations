using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
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

    public Task<REMSForm?> GetByInviteCodeAsync(Guid tenantId, string inviteCode, CancellationToken cancellationToken = default)
        => _dbContext.RemsForms
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && !f.Deleted && f.InviteCode == inviteCode, cancellationToken);

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
}
