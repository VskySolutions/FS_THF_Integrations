namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>
/// Commits all pending changes staged on the repositories sharing the current
/// scoped database context as a single atomic transaction. This is the seam that
/// lets an action and its audit-trail entry be written together (see
/// <see cref="IAuditTrailRepository"/>).
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists all staged changes in one transaction.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
