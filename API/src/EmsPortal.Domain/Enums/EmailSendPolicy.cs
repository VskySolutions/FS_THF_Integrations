namespace EmsPortal.Domain.Enums;

/// <summary>
/// The transactional email allowlist (WO-124, AC-ETPL-005.5). Only account-security and REMS external
/// templates may be delivered by email; every other <see cref="EmailTemplateKey"/> — mentions, reminders
/// and any future internal workflow alert — is in-app only. Enforced centrally by the email dispatch
/// pipeline so no caller can send email for a non-allowlisted type.
/// </summary>
public static class EmailSendPolicy
{
    private static readonly HashSet<EmailTemplateKey> EmailAllowed = new()
    {
        EmailTemplateKey.UserInvitation,    // account security — invitation
        EmailTemplateKey.PasswordReset,     // account security — admin-initiated password reset
        EmailTemplateKey.PasswordChanged,   // account security — password-change confirmation
        EmailTemplateKey.Welcome,           // account security — account activated
        EmailTemplateKey.RemsFormLink,      // REMS external — client form link
        EmailTemplateKey.RemsFormReminder,  // REMS external — nudge a client who has not submitted yet
        EmailTemplateKey.RemsFormSubmitted, // REMS external — form submitted to Admin + CSE
    };

    /// <summary>True when the template key is permitted to be delivered by email.</summary>
    public static bool IsEmailAllowed(EmailTemplateKey key) => EmailAllowed.Contains(key);

    /// <summary>The template keys permitted to be delivered by email.</summary>
    public static IReadOnlyCollection<EmailTemplateKey> AllowedKeys => EmailAllowed;
}
