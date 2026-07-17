using System.Text.RegularExpressions;
using EmsPortal.Application.Abstractions.Email;
using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Email;

/// <summary>
/// Template management + rendering. The effective template for a scope is its own row when present,
/// otherwise the platform default row, otherwise the built-in default content (<see cref="DefaultEmailTemplates"/>).
/// </summary>
public sealed class EmailTemplateService : IEmailTemplateService
{
    // Matches {{ Placeholder }} with optional surrounding whitespace; name captured for case-insensitive lookup.
    private static readonly Regex PlaceholderPattern = new(@"\{\{\s*(?<name>[A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled);

    private readonly IEmailTemplateRepository _templates;
    private readonly IUnitOfWork _unitOfWork;

    public EmailTemplateService(IEmailTemplateRepository templates, IUnitOfWork unitOfWork)
    {
        _templates = templates;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<EmailTemplateDescriptor>> ListAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await _templates.ListForScopeAsync(tenantId, cancellationToken);
        return DefaultEmailTemplates.All.Select(def => Describe(def, tenantId, rows)).ToList();
    }

    public async Task<EmailTemplateDescriptor?> GetAsync(Guid? tenantId, EmailTemplateKey key, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(key))
        {
            return null;
        }

        var rows = await _templates.ListForScopeAsync(tenantId, cancellationToken);
        return Describe(DefaultEmailTemplates.For(key), tenantId, rows);
    }

    public async Task SaveAsync(Guid? tenantId, EmailTemplateKey key, string subject, string body, CancellationToken cancellationToken = default)
    {
        var existing = await _templates.GetAsync(tenantId, key, cancellationToken);
        if (existing is null)
        {
            await _templates.AddAsync(new EmailTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TemplateKey = key,
                Subject = subject.Trim(),
                Body = body,
            }, cancellationToken);
        }
        else
        {
            existing.Subject = subject.Trim();
            existing.Body = body;
            _templates.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ResetAsync(Guid? tenantId, EmailTemplateKey key, CancellationToken cancellationToken = default)
    {
        var existing = await _templates.GetAsync(tenantId, key, cancellationToken);

        if (tenantId is not null)
        {
            // Tenant scope: removing the override reverts to the platform default.
            if (existing is null)
            {
                return false;
            }
            _templates.Remove(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        // Platform scope: restore the built-in default content (the default row always exists once seeded).
        var def = DefaultEmailTemplates.For(key);
        if (existing is null)
        {
            await _templates.AddAsync(new EmailTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                TemplateKey = key,
                Subject = def.Subject,
                Body = def.Body,
            }, cancellationToken);
        }
        else
        {
            existing.Subject = def.Subject;
            existing.Body = def.Body;
            _templates.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public RenderedEmail Render(string subject, string body, IReadOnlyDictionary<string, string?> model)
        => new(Substitute(subject, model), Substitute(body, model));

    public async Task<RenderedEmail?> RenderEffectiveAsync(Guid? tenantId, EmailTemplateKey key, IReadOnlyDictionary<string, string?> model, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(key))
        {
            return null;
        }

        var effective = await _templates.GetEffectiveAsync(tenantId, key, cancellationToken);
        var def = DefaultEmailTemplates.For(key);
        var subject = effective?.Subject ?? def.Subject;
        var body = effective?.Body ?? def.Body;
        return Render(subject, body, model);
    }

    /// <summary>Builds a descriptor: effective content (override → default → built-in) + override flag + metadata.</summary>
    private static EmailTemplateDescriptor Describe(DefaultEmailTemplates.Definition def, Guid? tenantId, IReadOnlyList<EmailTemplate> rows)
    {
        var tenantOverride = tenantId is { } tid ? rows.FirstOrDefault(r => r.TenantId == tid && r.TemplateKey == def.Key) : null;
        var global = rows.FirstOrDefault(r => r.TenantId == null && r.TemplateKey == def.Key);
        var effective = tenantOverride ?? global;

        return new EmailTemplateDescriptor(
            def.Key.ToString(),
            def.DisplayName,
            def.Description,
            effective?.Subject ?? def.Subject,
            effective?.Body ?? def.Body,
            IsOverridden: tenantOverride is not null,
            def.Placeholders);
    }

    /// <summary>Replaces known <c>{{placeholder}}</c> tokens; unknown tokens are left untouched.</summary>
    private static string Substitute(string template, IReadOnlyDictionary<string, string?> model)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        return PlaceholderPattern.Replace(template, match =>
        {
            var name = match.Groups["name"].Value;
            return model.TryGetValue(name, out var value) ? value ?? string.Empty : match.Value;
        });
    }
}
