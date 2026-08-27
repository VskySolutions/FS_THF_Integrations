using EmsPortal.Api.Models.EmailTemplates;
using EmsPortal.Api.Security;
using EmsPortal.Application.Abstractions.Email;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Enums;
using EmsPortal.Shared.Contracts;
using EmsPortal.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmsPortal.Api.Controllers;

/// <summary>
/// Manage transactional email templates. Reads require <c>users.read</c>; writes require
/// <c>email.manage</c>. Tenant Admins manage their tenant's overrides; Super Admins additionally manage
/// the platform-wide defaults (<c>?global=true</c>) and any tenant's overrides (<c>?tenantId=</c>).
/// </summary>
[ApiController]
[Authorize]
[Route("/api/admin/email-templates")]
[Produces("application/json")]
[Tags("Email Templates")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
public sealed class EmailTemplatesController : ControllerBase
{
    // Representative values used when previewing a template.
    private static readonly IReadOnlyDictionary<string, string?> SampleData = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        ["FullName"] = "Jane Doe",
        ["Email"] = "jane.doe@example.com",
        ["TemporaryPassword"] = "Tmp!9x2Kq",
        ["LoginUrl"] = "https://app.example.com",
        ["AppBaseUrl"] = "https://app.example.com",
        ["TenantName"] = "Acme Corporation",
        ["ChangedAtUtc"] = "06/24/2026 02:05 PM",
    };

    private readonly IEmailTemplateService _templates;
    private readonly IUserRepository _users;

    public EmailTemplatesController(IEmailTemplateService templates, IUserRepository users)
    {
        _templates = templates;
        _users = users;
    }

    /// <summary>
    /// Fills in the Created/Updated By display names on a page of descriptors. A template that has never
    /// been overridden carries no actor ids and is returned untouched — there is no edit to attribute.
    /// </summary>
    private async Task<IReadOnlyList<EmailTemplateDescriptor>> WithAuditNamesAsync(
        IEnumerable<EmailTemplateDescriptor> templates, CancellationToken cancellationToken)
    {
        var rows = templates.ToList();
        var names = await _users.GetFullNamesAsync(
            rows.SelectMany(t => new[] { t.CreatedById, t.UpdatedById })
                .Where(id => id.HasValue).Select(id => id!.Value),
            cancellationToken);
        string? NameOf(Guid? id) => id is { } uid && names.TryGetValue(uid, out var n) ? n : null;

        return rows
            .Select(t => t with { CreatedBy = NameOf(t.CreatedById), UpdatedBy = NameOf(t.UpdatedById) })
            .ToList();
    }

    [HttpGet]
    [RequirePermission(Permissions.UsersRead)]
    [ProducesResponseType<ApiResponse<IEnumerable<EmailTemplateDescriptor>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? tenantId, [FromQuery] bool global, CancellationToken cancellationToken)
    {
        var (scope, error) = ResolveScope(tenantId, global);
        if (error is not null)
        {
            return error;
        }

        var templates = await _templates.ListAsync(scope, cancellationToken);
        return Ok(ApiResponseFactory.Success(
            await WithAuditNamesAsync(templates, cancellationToken), "Email templates retrieved."));
    }

    [HttpGet("{key}")]
    [RequirePermission(Permissions.UsersRead)]
    [ProducesResponseType<ApiResponse<EmailTemplateDescriptor>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string key, [FromQuery] Guid? tenantId, [FromQuery] bool global, CancellationToken cancellationToken)
    {
        if (!TryParseKey(key, out var templateKey))
        {
            return NotFound(ApiResponseFactory.NotFound($"Unknown template '{key}'."));
        }

        var (scope, error) = ResolveScope(tenantId, global);
        if (error is not null)
        {
            return error;
        }

        var template = await _templates.GetAsync(scope, templateKey, cancellationToken);
        return template is null
            ? NotFound(ApiResponseFactory.NotFound($"Unknown template '{key}'."))
            : Ok(ApiResponseFactory.Success(template, "Email template retrieved."));
    }

    [HttpPut("{key}")]
    [RequirePermission(Permissions.EmailManage)]
    [ProducesResponseType<ApiResponse<EmailTemplateDescriptor>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Save(string key, [FromQuery] Guid? tenantId, [FromQuery] bool global, [FromBody] SaveEmailTemplateRequest body, CancellationToken cancellationToken)
    {
        if (!TryParseKey(key, out var templateKey))
        {
            return NotFound(ApiResponseFactory.NotFound($"Unknown template '{key}'."));
        }

        var (scope, error) = ResolveScope(tenantId, global);
        if (error is not null)
        {
            return error;
        }

        await _templates.SaveAsync(scope, templateKey, body.Subject, body.Body, cancellationToken);
        var updated = await _templates.GetAsync(scope, templateKey, cancellationToken);
        return Ok(ApiResponseFactory.Success(updated, "Email template saved."));
    }

    [HttpDelete("{key}")]
    [RequirePermission(Permissions.EmailManage)]
    [ProducesResponseType<ApiResponse<EmailTemplateDescriptor>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reset(string key, [FromQuery] Guid? tenantId, [FromQuery] bool global, CancellationToken cancellationToken)
    {
        if (!TryParseKey(key, out var templateKey))
        {
            return NotFound(ApiResponseFactory.NotFound($"Unknown template '{key}'."));
        }

        var (scope, error) = ResolveScope(tenantId, global);
        if (error is not null)
        {
            return error;
        }

        await _templates.ResetAsync(scope, templateKey, cancellationToken);
        var reverted = await _templates.GetAsync(scope, templateKey, cancellationToken);
        return Ok(ApiResponseFactory.Success(reverted, "Email template reset to default."));
    }

    [HttpPost("{key}/preview")]
    [RequirePermission(Permissions.UsersRead)]
    [ProducesResponseType<ApiResponse<RenderedEmail>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(string key, [FromQuery] Guid? tenantId, [FromQuery] bool global, [FromBody] PreviewEmailTemplateRequest body, CancellationToken cancellationToken)
    {
        if (!TryParseKey(key, out var templateKey))
        {
            return NotFound(ApiResponseFactory.NotFound($"Unknown template '{key}'."));
        }

        var (scope, error) = ResolveScope(tenantId, global);
        if (error is not null)
        {
            return error;
        }

        // Render the supplied draft when provided, otherwise the current effective template.
        string? subject = body.Subject;
        string? bodyHtml = body.Body;
        if (subject is null || bodyHtml is null)
        {
            var effective = await _templates.GetAsync(scope, templateKey, cancellationToken);
            if (effective is null)
            {
                return NotFound(ApiResponseFactory.NotFound($"Unknown template '{key}'."));
            }
            subject ??= effective.Subject;
            bodyHtml ??= effective.Body;
        }

        var rendered = _templates.Render(subject, bodyHtml, SampleData);
        return Ok(ApiResponseFactory.Success(rendered, "Preview rendered."));
    }

    private static bool TryParseKey(string key, out EmailTemplateKey templateKey)
        => Enum.TryParse(key, ignoreCase: true, out templateKey) && Enum.IsDefined(templateKey);

    /// <summary>
    /// Resolves the scope to operate on. Super Admins may target the platform default (<paramref name="global"/>)
    /// or a tenant; everyone else is pinned to their active tenant and cannot touch the platform default.
    /// A null scope (with no error) means the platform default.
    /// </summary>
    private (Guid? Scope, IActionResult? Error) ResolveScope(Guid? tenantId, bool global)
    {
        if (User.IsSuperAdmin())
        {
            if (global)
            {
                return (null, null);
            }
            var tid = tenantId ?? User.GetActiveTenantId();
            return tid is null
                ? (null, BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Validation failed.", "tenantId is required.")))
                : (tid, null);
        }

        if (global)
        {
            return (null, StatusCode(StatusCodes.Status403Forbidden,
                ApiResponseFactory.Forbidden("Only a Super Admin can manage the platform default templates.")));
        }

        var active = User.GetActiveTenantId();
        return active is null
            ? (null, StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("No active tenant for the caller.")))
            : (active, null);
    }
}
