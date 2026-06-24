using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Abstractions.Persistence;

/// <summary>Data access for entity <see cref="Attachment"/> metadata.</summary>
public interface IAttachmentRepository
{
    Task AddAsync(Attachment attachment, CancellationToken cancellationToken = default);

    Task<Attachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Permanently removes the metadata row, bypassing soft-delete (the file is deleted separately).</summary>
    Task HardDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Attachment>> ListAsync(EntityType entityType, Guid entityId, CancellationToken cancellationToken = default);
}
