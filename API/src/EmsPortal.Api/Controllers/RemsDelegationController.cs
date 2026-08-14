using EmsPortal.Api.Models.Rems;
using EmsPortal.Api.Security;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Application.Abstractions.Tenancy;
using EmsPortal.Domain.Entities;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmsPortal.Api.Controllers;

/// <summary>
/// REMS delegation: a shareholder or CSE naming someone to work their requests, modelled on Concur.
/// <para>
/// Self-service by design — the endpoints below only ever read or write the CALLER's own delegations,
/// either as the principal (managing who may act for me) or as the delegate (who may I act for). Nothing
/// here lets one person arrange a delegation between two others, so no admin permission gates it; the
/// caller's own identity is the whole boundary.
/// </para>
/// <para>
/// Delegation covers preparing and, optionally, sending. It does NOT extend to approving — see
/// <see cref="REMSDelegation"/> for why that is a different decision with a real integrity hazard behind it.
/// </para>
/// </summary>
[ApiController]
[Route("api/rems/delegations")]
[Produces("application/json")]
[Tags("REMS Delegation")]
[Authorize]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
public sealed class RemsDelegationController : ControllerBase
{
    private readonly IRemsDelegationRepository _delegations;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;

    public RemsDelegationController(
        IRemsDelegationRepository delegations,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext)
    {
        _delegations = delegations;
        _users = users;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
    }

    /// <summary>The delegates I have named, in force or not.</summary>
    [HttpGet("mine")]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsDelegationView>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var rows = await _delegations.ListForPrincipalAsync(me, cancellationToken);
        return Ok(ApiResponseFactory.Success(rows.Select(ToView).ToList(), "REMS delegates retrieved."));
    }

    /// <summary>
    /// Who I may act for right now. Filtered to grants in force today, so an expired or future-dated one
    /// is simply not offered rather than being offered and then refused.
    /// </summary>
    [HttpGet("acting-for")]
    [ProducesResponseType<ApiResponse<IEnumerable<RemsActingForView>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ActingFor(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await _delegations.ListActiveForDelegateAsync(me, today, cancellationToken);
        var views = rows
            .Select(d => new RemsActingForView(
                d.PrincipalUserId, d.Principal?.DisplayName ?? "Unknown user", d.CanPrepare, d.CanSend))
            .ToList();
        return Ok(ApiResponseFactory.Success(views, "REMS delegations retrieved."));
    }

    /// <summary>
    /// Name a delegate, or change what an existing one may do. Upserts on the pair rather than adding a
    /// second grant: two live grants for one pair would leave "which rights apply?" unanswerable.
    /// </summary>
    [HttpPut]
    [ProducesResponseType<ApiResponse<RemsDelegationView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Upsert([FromBody] SaveRemsDelegationRequest request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }
        if (request.DelegateUserId == me)
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.", "You cannot delegate to yourself."));
        }
        if (await _users.GetByIdAsync(request.DelegateUserId, cancellationToken) is null)
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.", "Unknown delegateUserId."));
        }
        if (request.StartsOn is { } from && request.EndsOn is { } to && to < from)
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Validation failed.", "The end date is before the start date."));
        }

        var existing = await _delegations.GetAsync(me, request.DelegateUserId, cancellationToken);
        if (existing is null)
        {
            existing = new REMSDelegation
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantContext.TenantId,
                PrincipalUserId = me,
                DelegateUserId = request.DelegateUserId,
            };
            await _delegations.AddAsync(existing, cancellationToken);
        }

        existing.CanPrepare = request.CanPrepare;
        existing.CanSend = request.CanSend;
        existing.StartsOn = request.StartsOn;
        existing.EndsOn = request.EndsOn;
        _delegations.Update(existing);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _delegations.ListForPrincipalAsync(me, cancellationToken);
        var view = saved.FirstOrDefault(d => d.Id == existing.Id);
        return Ok(ApiResponseFactory.Success(ToView(view ?? existing), "REMS delegate saved."));
    }

    /// <summary>Withdraw a delegation. Only the principal who granted it may.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } me)
        {
            return Unauthorized(ApiResponseFactory.Unauthorized("No user context."));
        }

        // Not-mine is a 404 rather than a 403: whether somebody else's delegation exists is not the
        // caller's business either way.
        var row = await _delegations.GetByIdAsync(id, cancellationToken);
        if (row is null || row.PrincipalUserId != me)
        {
            return NotFound(ApiResponseFactory.NotFound("Delegation not found."));
        }

        _delegations.Remove(row);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success<object?>(null, "REMS delegate removed."));
    }

    private static RemsDelegationView ToView(REMSDelegation d)
        => new(d.Id, d.DelegateUserId, d.Delegate?.DisplayName ?? "Unknown user",
            d.CanPrepare, d.CanSend, d.StartsOn, d.EndsOn,
            d.IsActiveOn(DateOnly.FromDateTime(DateTime.UtcNow)));
}
