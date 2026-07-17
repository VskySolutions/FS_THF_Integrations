using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class EmailTemplateRepository : IEmailTemplateRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public EmailTemplateRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<EmailTemplate>> ListForScopeAsync(Guid? tenantId, CancellationToken cancellationToken = default)
        => await _dbContext.EmailTemplates
            .Where(t => t.TenantId == null || t.TenantId == tenantId)
            .ToListAsync(cancellationToken);

    public Task<EmailTemplate?> GetAsync(Guid? tenantId, EmailTemplateKey key, CancellationToken cancellationToken = default)
        => _dbContext.EmailTemplates.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.TemplateKey == key, cancellationToken);

    public async Task<EmailTemplate?> GetEffectiveAsync(Guid? tenantId, EmailTemplateKey key, CancellationToken cancellationToken = default)
    {
        if (tenantId is { } tid)
        {
            var overrideTemplate = await _dbContext.EmailTemplates
                .FirstOrDefaultAsync(t => t.TenantId == tid && t.TemplateKey == key, cancellationToken);
            if (overrideTemplate is not null)
            {
                return overrideTemplate;
            }
        }

        return await _dbContext.EmailTemplates
            .FirstOrDefaultAsync(t => t.TenantId == null && t.TemplateKey == key, cancellationToken);
    }

    public async Task AddAsync(EmailTemplate template, CancellationToken cancellationToken = default)
        => await _dbContext.EmailTemplates.AddAsync(template, cancellationToken);

    public void Update(EmailTemplate template) => _dbContext.EmailTemplates.Update(template);

    public void Remove(EmailTemplate template) => _dbContext.EmailTemplates.Remove(template);
}
