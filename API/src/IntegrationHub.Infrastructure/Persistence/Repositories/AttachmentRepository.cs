using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class AttachmentRepository : IAttachmentRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public AttachmentRepository(IntegrationHubDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(Attachment attachment, CancellationToken cancellationToken = default)
        => _dbContext.Attachments.AddAsync(attachment, cancellationToken).AsTask();

    public Task<Attachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Attachments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task HardDeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Attachments.Where(a => a.Id == id).ExecuteDeleteAsync(cancellationToken);

    public async Task<IReadOnlyList<Attachment>> ListAsync(EntityType entityType, Guid entityId, CancellationToken cancellationToken = default)
        => await _dbContext.Attachments
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedOnUtc)
            .ToListAsync(cancellationToken);
}
