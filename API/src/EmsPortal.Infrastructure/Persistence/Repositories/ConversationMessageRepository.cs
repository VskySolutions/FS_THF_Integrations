using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Common;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence.Repositories;

internal sealed class ConversationMessageRepository : IConversationMessageRepository
{
    private readonly EmsPortalDbContext _dbContext;

    public ConversationMessageRepository(EmsPortalDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(ConversationMessage message, CancellationToken cancellationToken = default)
        => _dbContext.ConversationMessages.AddAsync(message, cancellationToken).AsTask();

    public Task<ConversationMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.ConversationMessages.Include(m => m.Mentions).FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public void Update(ConversationMessage message) => _dbContext.ConversationMessages.Update(message);

    public void Remove(ConversationMessage message) => _dbContext.ConversationMessages.Remove(message);

    public async Task<(IReadOnlyList<ConversationMessage> Items, int Total)> ListAsync(
        EntityType entityType, Guid entityId, string? search, Guid? authorId, int page, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ConversationMessages
            .Include(m => m.Mentions)
            .Where(m => m.EntityType == entityType && m.EntityId == entityId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m => EF.Functions.Like(m.Body, $"%{search}%"));
        }
        if (authorId is { } author)
        {
            query = query.Where(m => m.CreatedById == author);
        }

        var ordered = query.OrderByDescending(m => m.CreatedOnUtc);
        var total = await ordered.CountAsync(cancellationToken);
        var items = await ordered.Skip((page - 1) * limit).Take(limit).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task AddMentionAsync(ConversationMessageMention mention, CancellationToken cancellationToken = default)
        => _dbContext.ConversationMessageMentions.AddAsync(mention, cancellationToken).AsTask();

    public void RemoveMention(ConversationMessageMention mention) => _dbContext.ConversationMessageMentions.Remove(mention);

    public async Task<(IReadOnlyList<(ConversationMessageMention Mention, ConversationMessage Message)> Items, int Total)> ListMentionsForUserAsync(
        Guid userId, EntityType? entityType, bool? isRead, SortRequest sort, int page, int limit,
        CancellationToken cancellationToken = default)
    {
        var query =
            from mention in _dbContext.ConversationMessageMentions
            join message in _dbContext.ConversationMessages on mention.ConversationMessageId equals message.Id
            where mention.MentionedUserId == userId
            select new { mention, message };

        if (entityType is { } et)
        {
            query = query.Where(x => x.message.EntityType == et);
        }
        if (isRead is { } read)
        {
            query = query.Where(x => x.mention.IsRead == read);
        }

        // A mention is an event: Date is both its default order and the only date it has.
        var sorts = SortMap.For(query, "createdOnUtc")
            .Add("entity", x => x.message.EntityType, x => x.message.CreatedOnUtc)
            .Add("preview", x => x.message.Body, x => x.message.CreatedOnUtc)
            .Add("status", x => x.mention.IsRead, x => x.message.CreatedOnUtc)
            .Add("createdOnUtc", x => x.message.CreatedOnUtc);

        var ordered = sorts.Apply(query, sort.SortBy, sort.Descending);
        var total = await ordered.CountAsync(cancellationToken);
        var rows = await ordered.Skip((page - 1) * limit).Take(limit).ToListAsync(cancellationToken);
        return (rows.Select(x => (x.mention, x.message)).ToList(), total);
    }

    public Task<ConversationMessageMention?> GetMentionForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.ConversationMessageMentions.FirstOrDefaultAsync(m => m.Id == id && m.MentionedUserId == userId, cancellationToken);
}
