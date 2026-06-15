using IntegrationHub.Api.Integrations;
using IntegrationHub.Api.Models.Mappings;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Field-mapping management, scoped per tenant + flow. Each flow (e.g. Expense Reports) owns a
/// flat list of field rules; a job for that flow applies them at run time. Tenant Admins manage
/// their own tenant; Super Admins manage any.
/// </summary>
[ApiController]
[Route("/api/admin/tenants/{tenantId:guid}/flow-mappings")]
[Produces("application/json")]
[Tags("FlowMappings")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class FlowMappingsController : ControllerBase
{
    private readonly IMappingConfigurationRepository _mappings;
    private readonly ITenantRepository _tenants;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public FlowMappingsController(
        IMappingConfigurationRepository mappings,
        ITenantRepository tenants,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _mappings = mappings;
        _tenants = tenants;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Lists every flow with its current field-rule count for the tenant.</summary>
    [HttpGet]
    [RequirePermission(Permissions.MappingsRead)]
    public async Task<IActionResult> List(Guid tenantId, CancellationToken cancellationToken)
    {
        var guard = await EnsureScopeAsync(tenantId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        var result = new List<FlowMappingSummary>();
        foreach (var flow in IntegrationFlows.All())
        {
            var fields = await _mappings.ListByTenantFlowAsync(tenantId, flow.InterfaceName, cancellationToken);
            var updated = fields.Count > 0 ? fields.Max(f => f.UpdatedOnUtc) : (DateTime?)null;
            result.Add(new FlowMappingSummary(
                flow.InterfaceName, flow.Label, flow.Source.ToString(), flow.Target.ToString(), fields.Count, updated));
        }

        return Ok(ApiResponseFactory.Success(result, "Flow mappings retrieved."));
    }

    [HttpGet("{interfaceName}")]
    [RequirePermission(Permissions.MappingsRead)]
    public async Task<IActionResult> Get(Guid tenantId, string interfaceName, CancellationToken cancellationToken)
    {
        var guard = await EnsureScopeAsync(tenantId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!IntegrationFlows.TryGetPair(interfaceName, out var source, out var target))
        {
            return NotFound(ApiResponseFactory.NotFound("Unknown flow."));
        }

        var fields = await _mappings.ListByTenantFlowAsync(tenantId, interfaceName, cancellationToken);
        var detail = new FlowMappingDetail(
            interfaceName, IntegrationFlows.Label(source, target, interfaceName), source.ToString(), target.ToString(),
            fields.Select(f => new MappingFieldResponse(f.SourceField, f.DestinationField, f.TransformationRule)).ToList());
        return Ok(ApiResponseFactory.Success(detail, "Flow mapping retrieved."));
    }

    /// <summary>Replaces the entire field set for a (tenant, flow).</summary>
    [HttpPut("{interfaceName}")]
    [RequirePermission(Permissions.MappingsWrite)]
    public async Task<IActionResult> Save(Guid tenantId, string interfaceName, [FromBody] SaveFlowMappingRequest request, CancellationToken cancellationToken)
    {
        var guard = await EnsureScopeAsync(tenantId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!IntegrationFlows.TryGetPair(interfaceName, out var source, out var target))
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Unknown flow.", interfaceName));
        }

        var fields = request.Fields ?? new List<MappingFieldModel>();
        if (fields.Any(f => string.IsNullOrWhiteSpace(f.SourceField) || string.IsNullOrWhiteSpace(f.DestinationField)))
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Every field needs a source and destination field.", string.Empty));
        }
        var dupes = fields.GroupBy(f => f.SourceField.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupes.Count > 0)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Duplicate source field(s).", string.Join(", ", dupes)));
        }

        // Replace: soft-delete the existing rows, add the incoming set.
        var existing = await _mappings.ListByTenantFlowAsync(tenantId, interfaceName, cancellationToken);
        foreach (var row in existing)
        {
            _mappings.Remove(row);
        }

        var now = DateTime.UtcNow;
        foreach (var f in fields)
        {
            await _mappings.AddAsync(new MappingConfiguration
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                InterfaceName = interfaceName,
                SourceSystem = source,
                TargetSystem = target,
                SourceField = f.SourceField.Trim(),
                DestinationField = f.DestinationField.Trim(),
                TransformationRule = string.IsNullOrWhiteSpace(f.TransformationRule) ? null : f.TransformationRule.Trim(),
                IsActive = true,
                Version = 1,
                CreatedAtUtc = now,
            }, cancellationToken);
        }

        await _audit.AddAsync(nameof(MappingConfiguration), $"{tenantId}:{interfaceName}", "FlowMappingSaved", details: $"{fields.Count} field(s)", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { interfaceName, fieldCount = fields.Count }, "Flow mapping saved."));
    }

    [HttpDelete("{interfaceName}")]
    [RequirePermission(Permissions.MappingsWrite)]
    public async Task<IActionResult> Clear(Guid tenantId, string interfaceName, CancellationToken cancellationToken)
    {
        var guard = await EnsureScopeAsync(tenantId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        var existing = await _mappings.ListByTenantFlowAsync(tenantId, interfaceName, cancellationToken);
        foreach (var row in existing)
        {
            _mappings.Remove(row);
        }
        await _audit.AddAsync(nameof(MappingConfiguration), $"{tenantId}:{interfaceName}", "FlowMappingCleared", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { message = "Flow mapping cleared." }, "Flow mapping cleared."));
    }

    /// <summary>Returns a 403/404/409 result when the caller may not act on the tenant; null when allowed.</summary>
    private async Task<IActionResult?> EnsureScopeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!User.IsSuperAdmin() && User.GetActiveTenantId() != tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("Not permitted for this tenant."));
        }

        var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return NotFound(ApiResponseFactory.Error(ApiErrorCodes.TenantNotFound, "Tenant not found.", tenantId.ToString()));
        }
        if (tenant.Status == TenantStatus.Archived)
        {
            return StatusCode(StatusCodes.Status409Conflict, ApiResponseFactory.Error(ApiErrorCodes.TenantArchived, "Tenant is archived.", tenantId.ToString()));
        }
        return null;
    }
}
