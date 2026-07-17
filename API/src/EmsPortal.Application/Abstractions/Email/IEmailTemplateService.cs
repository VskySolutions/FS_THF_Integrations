using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Abstractions.Email;

/// <summary>
/// Manages transactional email templates and renders them. Reads resolve the effective template for a
/// scope (a tenant's override, else the platform default, else the built-in default); writes upsert the
/// scope's row. Rendering substitutes <c>{{placeholder}}</c> tokens from a supplied model.
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>The effective template for every key in the given scope (null tenant = the platform defaults).</summary>
    Task<IReadOnlyList<EmailTemplateDescriptor>> ListAsync(Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>The effective template for one key in the given scope, or null for an unknown key.</summary>
    Task<EmailTemplateDescriptor?> GetAsync(Guid? tenantId, EmailTemplateKey key, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates the template row for the scope + key (null tenant = the platform default).</summary>
    Task SaveAsync(Guid? tenantId, EmailTemplateKey key, string subject, string body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a template to its default. For a tenant scope this removes the override (reverting to the
    /// platform default); for the platform scope it restores the built-in default content. Returns false
    /// when there was nothing to reset (no tenant override existed).
    /// </summary>
    Task<bool> ResetAsync(Guid? tenantId, EmailTemplateKey key, CancellationToken cancellationToken = default);

    /// <summary>Substitutes <c>{{placeholder}}</c> tokens (case-insensitive, whitespace-tolerant) in a subject + body.</summary>
    RenderedEmail Render(string subject, string body, IReadOnlyDictionary<string, string?> model);

    /// <summary>Resolves the effective template for the scope + key and renders it with the model.</summary>
    Task<RenderedEmail?> RenderEffectiveAsync(Guid? tenantId, EmailTemplateKey key, IReadOnlyDictionary<string, string?> model, CancellationToken cancellationToken = default);
}
