namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// Allocates the next per-tenant REMS request number in the form <c>REMS-{seq}</c> (WO-110), where
/// <c>seq</c> is the count of active requests for the tenant plus one. Callers must allocate inside
/// <see cref="IUnitOfWork.ExecuteInTransactionAsync"/> and persist in the same transaction; the
/// filtered unique index <c>(TenantId, REMSNumber) WHERE [Deleted] = 0</c> is the definitive
/// uniqueness guard under concurrency.
/// </summary>
public interface IRemsNumberGenerator
{
    /// <summary>Produces the next available <c>REMS-{seq}</c> number for the tenant.</summary>
    Task<string> GenerateAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
