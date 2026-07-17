using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class NoteRepository : INoteRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public NoteRepository(EmsPortalDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(Note note, CancellationToken cancellationToken = default)
        => _dbContext.Notes.AddAsync(note, cancellationToken).AsTask();

    public Task<Note?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Notes.Include(n => n.Mentions).FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public void Update(Note note) => _dbContext.Notes.Update(note);

    public void Remove(Note note) => _dbContext.Notes.Remove(note);

    public async Task<(IReadOnlyList<Note> Items, int Total)> ListAsync(
        EntityType entityType, Guid entityId, string? search, Guid? authorId, int page, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notes
            .Include(n => n.Mentions)
            .Where(n => n.EntityType == entityType && n.EntityId == entityId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(n => EF.Functions.Like(n.Body, $"%{search}%"));
        }
        if (authorId is { } author)
        {
            query = query.Where(n => n.CreatedById == author);
        }

        var ordered = query.OrderByDescending(n => n.CreatedOnUtc);
        var total = await ordered.CountAsync(cancellationToken);
        var items = await ordered.Skip((page - 1) * limit).Take(limit).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task AddMentionAsync(NoteMention mention, CancellationToken cancellationToken = default)
        => _dbContext.NoteMentions.AddAsync(mention, cancellationToken).AsTask();

    public void RemoveMention(NoteMention mention) => _dbContext.NoteMentions.Remove(mention);

    public async Task<(IReadOnlyList<(NoteMention Mention, Note Note)> Items, int Total)> ListMentionsForUserAsync(
        Guid userId, EntityType? entityType, bool? isRead, int page, int limit, CancellationToken cancellationToken = default)
    {
        var query =
            from mention in _dbContext.NoteMentions
            join note in _dbContext.Notes on mention.NoteId equals note.Id
            where mention.MentionedUserId == userId
            select new { mention, note };

        if (entityType is { } et)
        {
            query = query.Where(x => x.note.EntityType == et);
        }
        if (isRead is { } read)
        {
            query = query.Where(x => x.mention.IsRead == read);
        }

        var ordered = query.OrderByDescending(x => x.note.CreatedOnUtc);
        var total = await ordered.CountAsync(cancellationToken);
        var rows = await ordered.Skip((page - 1) * limit).Take(limit).ToListAsync(cancellationToken);
        return (rows.Select(x => (x.mention, x.note)).ToList(), total);
    }

    public Task<NoteMention?> GetMentionForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.NoteMentions.FirstOrDefaultAsync(m => m.Id == id && m.MentionedUserId == userId, cancellationToken);
}
