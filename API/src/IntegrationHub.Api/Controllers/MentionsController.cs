using IntegrationHub.Api.Models.UniversalFeatures;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Mention Inbox + @mention autocomplete. Lists the authenticated user's @mentions across all records,
/// and resolves candidate users (tenant people who hold a login) for the note editor's autocomplete.
/// Scoped to the calling user / active tenant.
/// </summary>
[ApiController]
[Authorize]
[Route("/api/uf")]
[Produces("application/json")]
[Tags("Universal Features — Mentions")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
public sealed class MentionsController : ControllerBase
{
    private readonly INoteRepository _notes;
    private readonly IPersonRepository _persons;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;

    public MentionsController(INoteRepository notes, IPersonRepository persons, IUserRepository users, IUnitOfWork unitOfWork)
    {
        _notes = notes;
        _persons = persons;
        _users = users;
        _unitOfWork = unitOfWork;
    }

    [HttpGet("mentions")]
    [ProducesResponseType<ApiResponse<IEnumerable<MentionResponse>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] EntityType? entityType = null,
        [FromQuery] bool? isRead = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var (items, total) = await _notes.ListMentionsForUserAsync(userId, entityType, isRead, page, limit, cancellationToken);
        var authorNames = await _users.GetFullNamesAsync(
            items.Where(x => x.Note.CreatedById.HasValue).Select(x => x.Note.CreatedById!.Value), cancellationToken);

        var data = items.Select(x => new MentionResponse(
            x.Mention.Id, x.Note.Id, x.Note.EntityType, x.Note.EntityId, x.Note.CreatedById,
            x.Note.CreatedById is { } id && authorNames.TryGetValue(id, out var name) ? name : null,
            x.Note.Body.Length > 160 ? x.Note.Body[..160] + "…" : x.Note.Body,
            x.Mention.IsRead, x.Note.CreatedOnUtc));
        return Ok(ApiResponseFactory.Paginated(data, "Mentions retrieved.", page, limit, total));
    }

    [HttpPut("mentions/{id:guid}/read")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var mention = await _notes.GetMentionForUserAsync(id, userId, cancellationToken);
        if (mention is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Mention not found."));
        }

        if (!mention.IsRead)
        {
            // The mention is tracked; flipping the flag and saving persists it.
            mention.IsRead = true;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Ok(ApiResponseFactory.Success(new { mentionId = id }, "Mention marked read."));
    }

    [HttpGet("mention-candidates")]
    [ProducesResponseType<ApiResponse<IEnumerable<MentionCandidateResponse>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Candidates([FromQuery] string? search = null, CancellationToken cancellationToken = default)
    {
        // Tenant people who hold a login account are valid @mention targets.
        var (people, _) = await _persons.ListAsync(search, null, isUser: true, isActive: true, page: 1, limit: 20, cancellationToken);
        var data = people
            .Where(p => p.UserId.HasValue)
            .Select(p => new MentionCandidateResponse(
                p.UserId!.Value,
                string.IsNullOrWhiteSpace(p.DisplayName) ? $"{p.FirstName} {p.LastName}".Trim() : p.DisplayName,
                p.PrimaryEmail));
        return Ok(ApiResponseFactory.Success(data, "Mention candidates retrieved."));
    }
}
