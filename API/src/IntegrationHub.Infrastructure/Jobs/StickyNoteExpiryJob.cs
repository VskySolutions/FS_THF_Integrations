using IntegrationHub.Application.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Jobs;

/// <summary>
/// Recurring (hourly) sweep that soft-deletes expired tenant sticky notes. Runs without a resolved
/// tenant so the ambient filter is a no-op and it sees every tenant's expired notes; soft-delete is
/// handled by the DbContext interceptor on remove.
/// </summary>
public sealed class StickyNoteExpiryJob
{
    /// <summary>The Hangfire recurring job id.</summary>
    public const string Name = "uf-sticky-note-expiry";

    private readonly IStickyNoteRepository _notes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StickyNoteExpiryJob> _logger;

    public StickyNoteExpiryJob(IStickyNoteRepository notes, IUnitOfWork unitOfWork, ILogger<StickyNoteExpiryJob> logger)
    {
        _notes = notes;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var expired = await _notes.GetExpiredTenantNotesAsync(DateTime.UtcNow, cancellationToken);
        if (expired.Count == 0)
        {
            return;
        }

        foreach (var note in expired)
        {
            _notes.Remove(note);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Expired {Count} tenant sticky note(s).", expired.Count);
    }
}
