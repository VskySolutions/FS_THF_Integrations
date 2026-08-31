using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IRemsDelegationRepository"/>
public sealed class RemsDelegationRepository : IRemsDelegationRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public RemsDelegationRepository(EmsPortalDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<REMSDelegation>> ListForPrincipalAsync(
        Guid principalUserId, CancellationToken cancellationToken = default)
        => await _dbContext.RemsDelegations
            .Include(d => d.Delegate)
            .Where(d => d.PrincipalUserId == principalUserId)
            .OrderBy(d => d.CreatedOnUtc)
            .ToListAsync(cancellationToken);

    // Date-window filtering runs in SQL on the nullable bounds rather than through IsActiveOn, which EF
    // cannot translate. The two must agree — IsActiveOn is what authorises the action itself.
    public async Task<IReadOnlyList<REMSDelegation>> ListActiveForDelegateAsync(
        Guid delegateUserId, DateOnly on, CancellationToken cancellationToken = default)
        => await _dbContext.RemsDelegations
            .Include(d => d.Principal)
            .Where(d => d.DelegateUserId == delegateUserId
                && (d.StartsOn == null || d.StartsOn <= on)
                && (d.EndsOn == null || d.EndsOn >= on))
            .OrderBy(d => d.Principal!.DisplayName)
            .ToListAsync(cancellationToken);

    // Same SQL date-window as ListActiveForDelegateAsync, and for the same reason: IsActiveOn is the
    // authority but EF cannot translate it. AnyAsync so the answer costs an EXISTS rather than a row.
    public Task<bool> HasActiveDelegateAsync(
        Guid principalUserId, DateOnly on, CancellationToken cancellationToken = default)
        => _dbContext.RemsDelegations
            .AnyAsync(
                d => d.PrincipalUserId == principalUserId
                    && (d.StartsOn == null || d.StartsOn <= on)
                    && (d.EndsOn == null || d.EndsOn >= on),
                cancellationToken);

    public Task<REMSDelegation?> GetAsync(
        Guid principalUserId, Guid delegateUserId, CancellationToken cancellationToken = default)
        => _dbContext.RemsDelegations
            .FirstOrDefaultAsync(
                d => d.PrincipalUserId == principalUserId && d.DelegateUserId == delegateUserId,
                cancellationToken);

    public Task<REMSDelegation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.RemsDelegations.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task AddAsync(REMSDelegation delegation, CancellationToken cancellationToken = default)
        => await _dbContext.RemsDelegations.AddAsync(delegation, cancellationToken);

    public void Update(REMSDelegation delegation) => _dbContext.RemsDelegations.Update(delegation);

    public void Remove(REMSDelegation delegation) => _dbContext.RemsDelegations.Remove(delegation);
}
