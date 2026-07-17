using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class MediaRepository : IMediaRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public MediaRepository(EmsPortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Media?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Media.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task AddAsync(Media media, CancellationToken cancellationToken = default)
        => await _dbContext.Media.AddAsync(media, cancellationToken);

    public void Update(Media media) => _dbContext.Media.Update(media);
}
