namespace IntegrationHub.Api.Models.EmailTemplates;

/// <summary>Create/update payload for an email template (subject + HTML body).</summary>
public sealed class SaveEmailTemplateRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// Preview payload: render the supplied draft (or the effective template when omitted) with sample
/// placeholder values so the author can see the result before saving.
/// </summary>
public sealed class PreviewEmailTemplateRequest
{
    public string? Subject { get; set; }
    public string? Body { get; set; }
}
