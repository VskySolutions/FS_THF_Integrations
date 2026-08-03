using EmsPortal.Domain.Entities;

namespace EmsPortal.Application.Abstractions.Persistence;

/// <summary>
/// Data access for <see cref="UserDepartment"/> — a user's department placement and headship within a
/// tenant. Tenant isolation is applied by the ambient query filter, so every read/write here is already
/// scoped to the caller's active tenant.
/// </summary>
public interface IUserDepartmentRepository
{
    /// <summary>The user's active department row in the current tenant, or null when unplaced.</summary>
    Task<UserDepartment?> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The current head of a department in this tenant, or null when it has none.</summary>
    Task<UserDepartment?> GetHeadAsync(string department, CancellationToken cancellationToken = default);

    /// <summary>Every department head in this tenant (one row per headed department).</summary>
    Task<IReadOnlyList<UserDepartment>> ListHeadsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(UserDepartment department, CancellationToken cancellationToken = default);

    void Update(UserDepartment department);

    void Remove(UserDepartment department);
}
