using EmsPortal.Api.Models.Rems;
using EmsPortal.Api.Security;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;
using Microsoft.AspNetCore.Mvc;

namespace EmsPortal.Api.Controllers;

/// <summary>
/// Per-tenant REMS engagement settings (WO-114): the managing shareholder (a required approver on every
/// engagement) and the department-to-director mapping used to prefill an engagement's department director.
/// Admin/staff only (<see cref="Permissions.RemsEngagementsManage"/>); tenant isolation is ambient.
/// </summary>
[ApiController]
[Route("api/rems/settings")]
[Produces("application/json")]
[Tags("REMS Settings")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class RemsSettingsController : ControllerBase
{
    private readonly IRemsSettingsRepository _settings;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;

    public RemsSettingsController(IRemsSettingsRepository settings, IUserRepository users, IUnitOfWork unitOfWork)
    {
        _settings = settings;
        _users = users;
        _unitOfWork = unitOfWork;
    }

    /// <summary>The tenant's REMS settings: the managing shareholder and the department-director map (WO-114).</summary>
    [HttpGet]
    [RequirePermission(Permissions.RemsEngagementsManage)]
    [ProducesResponseType<ApiResponse<RemsSettingsView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        var view = await BuildViewAsync(settings, cancellationToken);
        return Ok(ApiResponseFactory.Success(view, "REMS settings retrieved."));
    }

    /// <summary>Set the managing shareholder and fully replace the department-director map (WO-114).</summary>
    [HttpPut]
    [RequirePermission(Permissions.RemsEngagementsManage)]
    [ProducesResponseType<ApiResponse<RemsSettingsView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update([FromBody] UpdateRemsSettingsRequest request, CancellationToken cancellationToken)
    {
        if (User.GetActiveTenantId() is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("No active tenant context."));
        }

        // Every referenced user must resolve to a real user.
        if (request.ManagingShareholderUserId is { } msId && await _users.GetByIdAsync(msId, cancellationToken) is null)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Validation failed.", "Unknown managingShareholderUserId."));
        }
        foreach (var director in request.DepartmentDirectors)
        {
            if (await _users.GetByIdAsync(director.DirectorUserId, cancellationToken) is null)
            {
                return BadRequest(ApiResponseFactory.Error(
                    ApiErrorCodes.ValidationFailed, "Validation failed.", $"Unknown directorUserId for department '{director.Department}'."));
            }
        }

        var settings = await _settings.GetAsync(cancellationToken);
        if (settings is null)
        {
            settings = new RemsSettings { Id = Guid.NewGuid() };
            await _settings.AddAsync(settings, cancellationToken);
        }

        settings.ManagingShareholderUserId = request.ManagingShareholderUserId;
        _settings.Update(settings);

        await ReconcileDepartmentDirectorsAsync(settings, request.DepartmentDirectors, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _settings.GetAsync(cancellationToken);
        var view = await BuildViewAsync(refreshed, cancellationToken);
        return Ok(ApiResponseFactory.Success(view, "REMS settings updated."));
    }

    // -------------------- Helpers --------------------

    /// <summary>Upserts the supplied department-director rows by (normalized) department and removes any not present.</summary>
    private async Task ReconcileDepartmentDirectorsAsync(
        RemsSettings settings, IReadOnlyList<RemsDepartmentDirectorInput> desired, CancellationToken cancellationToken)
    {
        var existing = settings.DepartmentDirectors.Where(d => !d.Deleted).ToList();
        var desiredByDept = desired.ToDictionary(d => Normalize(d.Department), d => d.DirectorUserId);

        // Remove mappings that are no longer wanted.
        foreach (var row in existing)
        {
            if (!desiredByDept.ContainsKey(Normalize(row.Department)))
            {
                _settings.RemoveDepartmentDirector(row);
            }
        }

        // Upsert wanted mappings.
        foreach (var (department, directorId) in desiredByDept)
        {
            var row = existing.FirstOrDefault(d => Normalize(d.Department) == department);
            if (row is null)
            {
                await _settings.AddDepartmentDirectorAsync(new RemsDepartmentDirector
                {
                    Id = Guid.NewGuid(),
                    RemsSettingsId = settings.Id,
                    Department = department,
                    DirectorUserId = directorId,
                }, cancellationToken);
            }
            else if (row.DirectorUserId != directorId)
            {
                row.DirectorUserId = directorId;
                _settings.UpdateDepartmentDirector(row);
            }
        }
    }

    private async Task<RemsSettingsView> BuildViewAsync(RemsSettings? settings, CancellationToken cancellationToken)
    {
        if (settings is null)
        {
            return new RemsSettingsView(null, Array.Empty<RemsDepartmentDirectorView>());
        }

        var userIds = new List<Guid>();
        if (settings.ManagingShareholderUserId is { } ms)
        {
            userIds.Add(ms);
        }
        userIds.AddRange(settings.DepartmentDirectors.Where(d => !d.Deleted).Select(d => d.DirectorUserId));

        var names = await _users.GetFullNamesAsync(userIds, cancellationToken);

        var managingShareholder = settings.ManagingShareholderUserId is { } msId
            ? new RemsUserRef(msId, names.TryGetValue(msId, out var n) ? n : string.Empty)
            : null;

        var directors = settings.DepartmentDirectors
            .Where(d => !d.Deleted)
            .OrderBy(d => d.Department)
            .Select(d => new RemsDepartmentDirectorView(
                d.Department,
                new RemsUserRef(d.DirectorUserId, names.TryGetValue(d.DirectorUserId, out var dn) ? dn : string.Empty)))
            .ToList();

        return new RemsSettingsView(managingShareholder, directors);
    }

    private static string Normalize(string department) => department.Trim().ToLowerInvariant();
}
