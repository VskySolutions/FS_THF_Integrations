using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

internal sealed class CustomerDocumentRepository : ICustomerDocumentRepository
{
    private readonly IntegrationHubDbContext _dbContext;

    public CustomerDocumentRepository(IntegrationHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<CustomerDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.CustomerDocuments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CustomerDocument>> ListByCustomerAsync(Guid customerRequestId, CancellationToken cancellationToken = default)
        => await _dbContext.CustomerDocuments
            .Where(d => d.CustomerRequestId == customerRequestId)
            .OrderByDescending(d => d.UploadedOnUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(CustomerDocument document, CancellationToken cancellationToken = default)
        => await _dbContext.CustomerDocuments.AddAsync(document, cancellationToken);

    public void Remove(CustomerDocument document) => _dbContext.CustomerDocuments.Remove(document);
}
