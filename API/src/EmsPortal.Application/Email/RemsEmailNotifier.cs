using EmsPortal.Application.Abstractions.Email;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Application.Email;

/// <summary>
/// Default <see cref="IRemsEmailNotifier"/>. Maps the typed REMS models onto the placeholder tokens the
/// seeded templates expect and enqueues them through <see cref="IEmailDispatcher"/> for best-effort
/// background delivery. Template resolution, the no-active-SMTP-account skip and failure swallowing all
/// happen downstream in the shared email pipeline (<c>EmailNotificationService</c>).
/// </summary>
internal sealed class RemsEmailNotifier : IRemsEmailNotifier
{
    private readonly IEmailDispatcher _dispatcher;

    public RemsEmailNotifier(IEmailDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void SendFormLink(Guid tenantId, string toEmail, RemsFormLinkEmail model)
        => _dispatcher.Enqueue(tenantId, EmailTemplateKey.RemsFormLink, toEmail, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ClientName"] = model.ClientName,
            ["FormLink"] = model.FormLink,
            ["RemsNumber"] = model.RemsNumber,
            ["RequestTitle"] = model.RequestTitle,
        });

    public void SendFormSubmitted(Guid tenantId, string toEmail, RemsFormSubmittedEmail model)
        => _dispatcher.Enqueue(tenantId, EmailTemplateKey.RemsFormSubmitted, toEmail, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ClientName"] = model.ClientName,
            ["RemsNumber"] = model.RemsNumber,
            ["RequestLink"] = model.RequestLink,
            ["SubmittedOn"] = model.SubmittedOn,
        });
}
