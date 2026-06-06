using System.Text.Json;
using IntegrationHub.Api.Models.Tenants;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Auditing;
using IntegrationHub.Application.Abstractions.Connectors.Concur;
using IntegrationHub.Application.Abstractions.Connectors.Maconomy;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Tenant management (WO-40): lifecycle (Super Admin) plus per-tenant credential and
/// mapping management (Tenant Admin or above, scoped to their own tenant).
/// </summary>
[ApiController]
[Route("/api/admin/tenants")]
[Produces("application/json")]
[Tags("Tenants")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class TenantsController : ControllerBase
{
    private readonly ITenantRepository _tenants;
    private readonly ITenantApiConfigurationRepository _configs;
    private readonly IMappingConfigurationRepository _mappings;
    private readonly IIntegrationJobRepository _jobs;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ITenantContext _tenantContext;
    private readonly IConcurConnector _concur;
    private readonly IMaconomyConnector _maconomy;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public TenantsController(
        ITenantRepository tenants,
        ITenantApiConfigurationRepository configs,
        IMappingConfigurationRepository mappings,
        IIntegrationJobRepository jobs,
        ICredentialEncryptionService encryption,
        ITenantContext tenantContext,
        IConcurConnector concur,
        IMaconomyConnector maconomy,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _tenants = tenants;
        _configs = configs;
        _mappings = mappings;
        _jobs = jobs;
        _encryption = encryption;
        _tenantContext = tenantContext;
        _concur = concur;
        _maconomy = maconomy;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    // ---- Tenant lifecycle (Super Admin) ----

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    [ProducesResponseType<ApiResponse<TenantResponse>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (await _tenants.IdentifierExistsAsync(request.Identifier, cancellationToken))
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.DuplicateIdentifier, "Identifier already in use.", request.Identifier));
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Identifier = request.Identifier,
            Status = TenantStatus.Active,
            CreatedDate = DateTime.UtcNow,
        };
        await _tenants.AddAsync(tenant, cancellationToken);
        await _audit.AddAsync(nameof(Tenant), tenant.Id.ToString(), "Created", details: tenant.Identifier, cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponseFactory.Success(new TenantResponse(tenant.Id, tenant.Identifier, tenant.Status.ToString()), "Tenant created."));
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int limit = 20, [FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var all = await _tenants.ListAsync(cancellationToken);
        var filtered = (includeArchived ? all : all.Where(t => t.Status != TenantStatus.Archived)).ToList();
        var pageItems = filtered.Skip((page - 1) * limit).Take(limit)
            .Select(t => new TenantSummary(t.Id, t.Name, t.Identifier, t.Status.ToString()));

        return Ok(ApiResponseFactory.Paginated(pageItems, "Tenants retrieved.", page, limit, filtered.Count));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    [ProducesResponseType<ApiResponse<TenantDetail>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(id, cancellationToken);
        if (tenant is null)
        {
            return NotFound(ApiResponseFactory.Error(ApiErrorCodes.TenantNotFound, "Tenant not found.", id.ToString()));
        }

        var configs = await _configs.ListByTenantAsync(id, cancellationToken);
        var detail = new TenantDetail(
            tenant.Id, tenant.Name, tenant.Identifier, tenant.Status.ToString(),
            new CredentialIndicator(configs.Any(c => c.System == SystemName.Concur)),
            new CredentialIndicator(configs.Any(c => c.System == SystemName.Maconomy)));

        return Ok(ApiResponseFactory.Success(detail, "Tenant retrieved."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(id, cancellationToken);
        if (tenant is null)
        {
            return NotFound(ApiResponseFactory.Error(ApiErrorCodes.TenantNotFound, "Tenant not found.", id.ToString()));
        }

        tenant.Name = request.Name; // identifier is immutable
        _tenants.Update(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(
            new TenantResponse(tenant.Id, tenant.Identifier, tenant.Status.ToString()), "Tenant updated."));
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UpdateTenantStatusRequest request, CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(id, cancellationToken);
        if (tenant is null)
        {
            return NotFound(ApiResponseFactory.Error(ApiErrorCodes.TenantNotFound, "Tenant not found.", id.ToString()));
        }

        tenant.Status = request.IsActive ? TenantStatus.Active : TenantStatus.Inactive;
        _tenants.Update(tenant);
        await _audit.AddAsync(nameof(Tenant), tenant.Id.ToString(), request.IsActive ? "Activated" : "Deactivated", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { tenantId = tenant.Id, status = tenant.Status.ToString() }, "Status updated."));
    }

    [HttpPut("{id:guid}/archive")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(id, cancellationToken);
        if (tenant is null)
        {
            return NotFound(ApiResponseFactory.Error(ApiErrorCodes.TenantNotFound, "Tenant not found.", id.ToString()));
        }

        if (await _jobs.HasActiveJobsAsync(id, cancellationToken))
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.ActiveJobsExist, "Tenant has active jobs.", id.ToString()));
        }

        tenant.Status = TenantStatus.Archived;
        _tenants.Update(tenant);
        await _audit.AddAsync(nameof(Tenant), tenant.Id.ToString(), "Archived", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { tenantId = tenant.Id, status = tenant.Status.ToString() }, "Tenant archived."));
    }

    // ---- Credentials (Tenant Admin or above, own tenant) ----

    [HttpPut("{id:guid}/concur-config")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public Task<IActionResult> SetConcurConfig(Guid id, [FromBody] ConcurCredentialsRequest request, CancellationToken cancellationToken)
        => StoreCredentialsAsync(id, SystemName.Concur,
            new ConcurConfigDto(request.ClientId, request.ClientSecret, request.BaseUrl, request.CompanyUuid), cancellationToken);

    [HttpPut("{id:guid}/maconomy-config")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public Task<IActionResult> SetMaconomyConfig(Guid id, [FromBody] MaconomyCredentialsRequest request, CancellationToken cancellationToken)
        => StoreCredentialsAsync(id, SystemName.Maconomy,
            new MaconomyConfigDto(request.BaseUrl, request.Username, request.Password), cancellationToken);

    [HttpDelete("{id:guid}/concur-config")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public Task<IActionResult> ClearConcurConfig(Guid id, CancellationToken cancellationToken)
        => ClearCredentialsAsync(id, SystemName.Concur, cancellationToken);

    [HttpDelete("{id:guid}/maconomy-config")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public Task<IActionResult> ClearMaconomyConfig(Guid id, CancellationToken cancellationToken)
        => ClearCredentialsAsync(id, SystemName.Maconomy, cancellationToken);

    [HttpPost("{id:guid}/concur-config/test")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public async Task<IActionResult> TestConcurConfig(Guid id, CancellationToken cancellationToken)
    {
        var guard = await EnsureScopeAsync(id, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        // Scope the connector to the target tenant, then attempt a live authentication.
        _tenantContext.Set(id, string.Empty);
        var result = await _concur.AuthenticateAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(
            new CredentialTestResponse(result.Success, result.Success ? "Connected." : result.ErrorMessage ?? "Failed."), "Test complete."));
    }

    [HttpPost("{id:guid}/maconomy-config/test")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public async Task<IActionResult> TestMaconomyConfig(Guid id, CancellationToken cancellationToken)
    {
        var guard = await EnsureScopeAsync(id, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        _tenantContext.Set(id, string.Empty);
        var result = await _maconomy.AuthenticateAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(
            new CredentialTestResponse(result.Success, result.Success ? "Connected." : result.ErrorMessage ?? "Failed."), "Test complete."));
    }

    // ---- Mapping configuration CRUD (Tenant Admin or above, own tenant) ----

    [HttpGet("{id:guid}/mappings")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public async Task<IActionResult> ListMappings(Guid id, [FromQuery] int page = 1, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var guard = await EnsureScopeAsync(id, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        page = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);
        var (items, total) = await _mappings.ListByTenantAsync(id, page, limit, cancellationToken);
        return Ok(ApiResponseFactory.Paginated(items.Select(Map), "Mappings retrieved.", page, limit, total));
    }

    [HttpPost("{id:guid}/mappings")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public async Task<IActionResult> CreateMapping(Guid id, [FromBody] CreateMappingRequest request, CancellationToken cancellationToken)
    {
        var guard = await EnsureScopeAsync(id, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        var source = Enum.Parse<SystemName>(request.SourceSystem);
        var destination = Enum.Parse<SystemName>(request.DestinationSystem);

        // Replace an existing active mapping for the same field rather than duplicating (AC-TNT-012.6).
        var existing = await _mappings.GetActiveForFieldAsync(id, source, destination, request.SourceField, cancellationToken);
        if (existing is not null)
        {
            existing.DestinationField = request.DestinationField;
            existing.TransformationRule = request.TransformationRule;
            existing.IsActive = request.IsActive;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            _mappings.Update(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Ok(ApiResponseFactory.Success(Map(existing), "Mapping replaced."));
        }

        var mapping = new MappingConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = id,
            SourceSystem = source,
            TargetSystem = destination,
            SourceField = request.SourceField,
            DestinationField = request.DestinationField,
            TransformationRule = request.TransformationRule,
            IsActive = request.IsActive,
            Version = 1,
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _mappings.AddAsync(mapping, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.Success(Map(mapping), "Mapping created."));
    }

    [HttpPut("{id:guid}/mappings/{mappingId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public async Task<IActionResult> UpdateMapping(Guid id, Guid mappingId, [FromBody] UpdateMappingRequest request, CancellationToken cancellationToken)
    {
        var guard = await EnsureScopeAsync(id, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        var mapping = await _mappings.GetByIdForTenantAsync(mappingId, id, cancellationToken);
        if (mapping is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Mapping not found."));
        }

        if (request.DestinationField is not null)
        {
            mapping.DestinationField = request.DestinationField;
        }

        if (request.TransformationRule is not null)
        {
            mapping.TransformationRule = request.TransformationRule;
        }

        if (request.IsActive is { } active)
        {
            mapping.IsActive = active;
        }

        mapping.UpdatedAtUtc = DateTime.UtcNow;
        _mappings.Update(mapping);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(Map(mapping), "Mapping updated."));
    }

    [HttpDelete("{id:guid}/mappings/{mappingId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdminOrAbove)]
    public async Task<IActionResult> DeleteMapping(Guid id, Guid mappingId, CancellationToken cancellationToken)
    {
        var guard = await EnsureScopeAsync(id, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        var mapping = await _mappings.GetByIdForTenantAsync(mappingId, id, cancellationToken);
        if (mapping is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Mapping not found."));
        }

        _mappings.Remove(mapping);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { message = "Mapping deleted." }, "Mapping deleted."));
    }

    // ---- helpers ----

    private async Task<IActionResult> StoreCredentialsAsync<T>(Guid tenantId, SystemName system, T config, CancellationToken cancellationToken)
    {
        var guard = await EnsureScopeAsync(tenantId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        var ciphertext = _encryption.Encrypt(JsonSerializer.Serialize(config));
        var existing = await _configs.GetAsync(tenantId, system, cancellationToken);
        if (existing is not null)
        {
            existing.EncryptedCredentials = ciphertext;
            existing.UpdatedDate = DateTime.UtcNow;
            _configs.Update(existing);
        }
        else
        {
            await _configs.AddAsync(new TenantApiConfiguration
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                System = system,
                EncryptedCredentials = ciphertext,
                CreatedDate = DateTime.UtcNow,
            }, cancellationToken);
        }

        await _audit.AddAsync("TenantApiConfiguration", tenantId.ToString(), $"{system}CredentialsStored", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { message = $"{system} credentials stored." }, "Credentials stored."));
    }

    private async Task<IActionResult> ClearCredentialsAsync(Guid tenantId, SystemName system, CancellationToken cancellationToken)
    {
        var guard = await EnsureScopeAsync(tenantId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        var existing = await _configs.GetAsync(tenantId, system, cancellationToken);
        if (existing is null)
        {
            return NotFound(ApiResponseFactory.NotFound($"No {system} credentials configured."));
        }

        _configs.Remove(existing);
        await _audit.AddAsync("TenantApiConfiguration", tenantId.ToString(), $"{system}CredentialsCleared", cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { message = $"{system} credentials cleared." }, "Credentials cleared."));
    }

    /// <summary>Returns a 403 result when a non-Super-Admin acts on a tenant other than their active one; null when allowed.</summary>
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

    private static MappingResponse Map(MappingConfiguration m) => new(
        m.Id, m.SourceSystem.ToString(), m.TargetSystem.ToString(),
        m.SourceField, m.DestinationField, m.TransformationRule, m.IsActive);
}
