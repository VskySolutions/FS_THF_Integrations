using System.Text.RegularExpressions;
using EmsPortal.Api.Models.UniversalFeatures;
using EmsPortal.Api.Security;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.UniversalFeatures;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmsPortal.Api.Controllers;

/// <summary>
/// Notes — freeform, @mention-aware annotations attachable to any entity record via the shared
/// <c>(EntityType, EntityId)</c> key. Access requires the read permission of the parent entity; editing
/// is author-only; deletion is author-or-admin. Tenant-scoped via the ambient query filter.
/// </summary>
[ApiController]
[Authorize]
[Route("/api/uf/notes")]
[Produces("application/json")]
[Tags("Universal Features — Notes")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
public sealed class NotesController : ControllerBase
{
    /// <summary>Matches an @mention token of the form <c>@[Display Name](guid)</c>.</summary>
    private static readonly Regex MentionPattern = new(
        @"@\[[^\]]*\]\((?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\)",
        RegexOptions.Compiled);

    private readonly INoteRepository _notes;
    private readonly IUserRepository _users;
    private readonly IActivityEventWriter _activity;
    private readonly INotificationDispatcher _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public NotesController(
        INoteRepository notes,
        IUserRepository users,
        IActivityEventWriter activity,
        INotificationDispatcher notifications,
        IUnitOfWork unitOfWork)
    {
        _notes = notes;
        _users = users;
        _activity = activity;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    [ProducesResponseType<ApiResponse<IEnumerable<NoteResponse>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] EntityType entityType,
        [FromQuery] Guid entityId,
        [FromQuery] string? search = null,
        [FromQuery] Guid? authorId = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!User.CanAccess(entityType))
        {
            return Forbid();
        }

        var (items, total) = await _notes.ListAsync(entityType, entityId, search, authorId, page, limit, cancellationToken);
        var authorNames = await _users.GetFullNamesAsync(
            items.Where(n => n.CreatedById.HasValue).Select(n => n.CreatedById!.Value), cancellationToken);

        var data = items.Select(n => ToResponse(n, authorNames));
        return Ok(ApiResponseFactory.Paginated(data, "Notes retrieved.", page, limit, total));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<NoteResponse>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateNoteRequest request, CancellationToken cancellationToken)
    {
        if (!User.CanAccess(request.EntityType))
        {
            return Forbid();
        }

        var note = new Note
        {
            Id = Guid.NewGuid(),
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Body = request.Body,
        };
        await _notes.AddAsync(note, cancellationToken);

        var mentionIds = ResolveMentions(request.Body, request.MentionedUserIds);
        foreach (var userId in mentionIds)
        {
            await _notes.AddMentionAsync(new NoteMention { Id = Guid.NewGuid(), NoteId = note.Id, MentionedUserId = userId }, cancellationToken);
        }

        await _activity.WriteAsync(new CreateActivityEventDto(request.EntityType, request.EntityId, ActivityEventTypes.NoteAdded), cancellationToken);
        await NotifyMentionsAsync(note, mentionIds, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var authorNames = await ResolveAuthorNameAsync(note.CreatedById, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.Success(ToResponse(note, authorNames), "Note created."));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<NoteResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNoteRequest request, CancellationToken cancellationToken)
    {
        var note = await _notes.GetByIdAsync(id, cancellationToken);
        if (note is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Note not found."));
        }

        // Only the author may edit their own note.
        if (note.CreatedById != User.GetUserId())
        {
            return Forbid();
        }

        note.Body = request.Body;
        note.IsEdited = true;
        note.EditedOnUtc = DateTime.UtcNow;
        _notes.Update(note);

        // Re-notify only mentions newly added in this edit.
        var existing = note.Mentions.Select(m => m.MentionedUserId).ToHashSet();
        var resolved = ResolveMentions(request.Body, request.MentionedUserIds);
        var added = resolved.Where(uid => !existing.Contains(uid)).ToList();
        foreach (var userId in added)
        {
            await _notes.AddMentionAsync(new NoteMention { Id = Guid.NewGuid(), NoteId = note.Id, MentionedUserId = userId }, cancellationToken);
        }

        await NotifyMentionsAsync(note, added, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var authorNames = await ResolveAuthorNameAsync(note.CreatedById, cancellationToken);
        return Ok(ApiResponseFactory.Success(ToResponse(note, authorNames), "Note updated."));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var note = await _notes.GetByIdAsync(id, cancellationToken);
        if (note is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Note not found."));
        }

        // Author may delete their own note; Tenant Admin / Super Admin may delete any.
        var isAdmin = User.IsSuperAdmin() || User.HasPermission(Permissions.SettingsManage);
        if (note.CreatedById != User.GetUserId() && !isAdmin)
        {
            return Forbid();
        }

        _notes.Remove(note);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { noteId = id }, "Note deleted."));
    }

    private async Task NotifyMentionsAsync(Note note, IReadOnlyCollection<Guid> mentionIds, CancellationToken cancellationToken)
    {
        if (mentionIds.Count == 0)
        {
            return;
        }

        var actorId = User.GetUserId();
        var actorNames = actorId is { } aid
            ? await _users.GetFullNamesAsync(new[] { aid }, cancellationToken)
            : new Dictionary<Guid, string>();
        var actorName = actorId is { } a && actorNames.TryGetValue(a, out var n) ? n : "Someone";
        var preview = note.Body.Length > 140 ? note.Body[..140] + "…" : note.Body;

        foreach (var userId in mentionIds.Where(uid => uid != actorId))
        {
            await _notifications.DispatchAsync(new CreateNotificationDto(
                userId, NotificationType.Mention, $"{actorName} mentioned you in a note", preview, note.EntityType, note.EntityId),
                cancellationToken);
        }
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveAuthorNameAsync(Guid? authorId, CancellationToken cancellationToken)
        => authorId is { } id
            ? await _users.GetFullNamesAsync(new[] { id }, cancellationToken)
            : new Dictionary<Guid, string>();

    private static List<Guid> ResolveMentions(string body, IEnumerable<Guid>? explicitIds)
    {
        var ids = new HashSet<Guid>(explicitIds ?? Enumerable.Empty<Guid>());
        foreach (Match match in MentionPattern.Matches(body))
        {
            if (Guid.TryParse(match.Groups["id"].Value, out var id))
            {
                ids.Add(id);
            }
        }

        return ids.ToList();
    }

    private static NoteResponse ToResponse(Note note, IReadOnlyDictionary<Guid, string> authorNames)
        => new(
            note.Id,
            note.EntityType,
            note.EntityId,
            note.Body,
            note.CreatedById,
            note.CreatedById is { } id && authorNames.TryGetValue(id, out var name) ? name : null,
            note.IsEdited,
            note.EditedOnUtc,
            note.CreatedOnUtc,
            note.Mentions.Select(m => m.MentionedUserId).ToList());
}
