namespace EmsPortal.Application.Abstractions.Email;

/// <summary>A rendered email — subject and body with all placeholders substituted.</summary>
public sealed record RenderedEmail(string Subject, string Body);

/// <summary>
/// The effective template for a key in a scope, plus presentation metadata for the management UI.
/// <see cref="IsOverridden"/> is true when a tenant has its own override (vs. inheriting the default).
/// </summary>
public sealed record EmailTemplateDescriptor(
    string Key,
    string DisplayName,
    string Description,
    string Subject,
    string Body,
    bool IsOverridden,
    IReadOnlyList<string> Placeholders,
    // Audit trail of the row the content actually came from — the tenant override if there is one, else
    // the platform default row. NULL when neither exists: an untouched template is still just the built-in
    // definition in code, and there is no edit to attribute. The *By ids are resolved to names by the
    // controller. Nullable dates on purpose, so the list shows "—" rather than 0001-01-01.
    Guid? CreatedById = null,
    DateTime? CreatedOnUtc = null,
    Guid? UpdatedById = null,
    DateTime? UpdatedOnUtc = null,
    string? CreatedBy = null,
    string? UpdatedBy = null);
