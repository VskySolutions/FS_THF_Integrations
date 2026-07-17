using EmsPortal.Api.Models.UniversalFeatures;
using EmsPortal.Api.Security;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmsPortal.Api.Controllers;

/// <summary>
/// Saved Views — per-user and tenant-shared list-page configurations (filters, sort, columns). Private
/// views belong to their creator; shared views are visible tenant-wide and managed by their owner or a
/// <c>settings.manage</c> user. Tenant-scoped via the ambient query filter.
/// </summary>
[ApiController]
[Authorize]
[Route("/api/uf/saved-views")]
[Produces("application/json")]
[Tags("Universal Features — Saved Views")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
public sealed class SavedViewsController : ControllerBase
{
    private readonly ISavedViewRepository _views;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;

    public SavedViewsController(ISavedViewRepository views, IUserRepository users, IUnitOfWork unitOfWork)
    {
        _views = views;
        _users = users;
        _unitOfWork = unitOfWork;
    }

    [HttpGet("shared")]
    [RequirePermission(Permissions.SettingsManage)]
    [ProducesResponseType<ApiResponse<IEnumerable<SharedSavedViewResponse>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListShared(CancellationToken cancellationToken)
    {
        var views = await _views.ListSharedAsync(cancellationToken);
        var ownerNames = await _users.GetFullNamesAsync(
            views.Where(v => v.UserId.HasValue).Select(v => v.UserId!.Value), cancellationToken);
        var data = views.Select(v => new SharedSavedViewResponse(
            v.Id, v.Name, v.ListPage, v.UserId,
            v.UserId is { } id && ownerNames.TryGetValue(id, out var name) ? name : null, v.CreatedOnUtc));
        return Ok(ApiResponseFactory.Success(data, "Shared views retrieved."));
    }

    [HttpGet]
    [ProducesResponseType<ApiResponse<IEnumerable<SavedViewResponse>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string listPage, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var views = await _views.ListForUserAsync(userId, listPage ?? string.Empty, cancellationToken);
        var data = views.Select(v => ToResponse(v, userId));
        return Ok(ApiResponseFactory.Success(data, "Saved views retrieved."));
    }

    [HttpPost]
    [ProducesResponseType<ApiResponse<SavedViewResponse>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateSavedViewRequest request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        // Creating a shared (tenant) view requires settings.manage; private views are open to any user.
        if (request.IsShared && !User.IsSuperAdmin() && !User.HasPermission(Permissions.SettingsManage))
        {
            return Forbid();
        }

        var view = new SavedView
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            ListPage = request.ListPage.Trim(),
            FiltersJson = request.FiltersJson,
            SortJson = request.SortJson,
            ColumnsJson = request.ColumnsJson,
            IsShared = request.IsShared,
        };
        await _views.AddAsync(view, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.Success(ToResponse(view, userId), "Saved view created."));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<SavedViewResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSavedViewRequest request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var view = await _views.GetByIdAsync(id, cancellationToken);
        if (view is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Saved view not found."));
        }
        if (!CanManage(view, userId))
        {
            return Forbid();
        }

        view.Name = request.Name.Trim();
        view.FiltersJson = request.FiltersJson;
        view.SortJson = request.SortJson;
        view.ColumnsJson = request.ColumnsJson;
        view.IsShared = request.IsShared;
        _views.Update(view);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(ToResponse(view, userId), "Saved view updated."));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var view = await _views.GetByIdAsync(id, cancellationToken);
        if (view is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Saved view not found."));
        }
        if (!CanManage(view, userId))
        {
            return Forbid();
        }

        _views.Remove(view);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { savedViewId = id }, "Saved view deleted."));
    }

    private bool CanManage(SavedView view, Guid userId)
        => view.UserId == userId || (view.IsShared && (User.IsSuperAdmin() || User.HasPermission(Permissions.SettingsManage)));

    private static SavedViewResponse ToResponse(SavedView v, Guid currentUserId)
        => new(v.Id, v.Name, v.ListPage, v.FiltersJson, v.SortJson, v.ColumnsJson, v.IsShared, v.UserId == currentUserId, v.UserId);
}
