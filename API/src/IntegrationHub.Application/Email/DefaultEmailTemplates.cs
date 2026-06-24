using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Email;

/// <summary>
/// The built-in default content and metadata for each <see cref="EmailTemplateKey"/>. Used to seed the
/// platform-wide defaults, to drive the management UI (display name, description, available
/// placeholders), and as the fallback when no row exists yet.
/// </summary>
public static class DefaultEmailTemplates
{
    /// <summary>Default content + metadata for one template key.</summary>
    public sealed record Definition(
        EmailTemplateKey Key,
        string DisplayName,
        string Description,
        string Subject,
        string Body,
        IReadOnlyList<string> Placeholders);

    private static readonly IReadOnlyDictionary<EmailTemplateKey, Definition> Map = new[]
    {
        new Definition(
            EmailTemplateKey.UserInvitation,
            "User Invitation",
            "Sent when a person is given a login account. Includes their temporary password and the login link.",
            "You've been invited to {{TenantName}}",
            """
            <p>Hello {{FullName}},</p>
            <p>An account has been created for you on <strong>{{TenantName}}</strong>.</p>
            <p>Sign in with the credentials below and you'll be asked to set a new password on first login:</p>
            <ul>
              <li><strong>Email:</strong> {{Email}}</li>
              <li><strong>Temporary password:</strong> {{TemporaryPassword}}</li>
            </ul>
            <p><a href="{{LoginUrl}}">Sign in to your account</a></p>
            <p>If you did not expect this email, please contact your administrator.</p>
            """,
            new[] { "FullName", "Email", "TemporaryPassword", "LoginUrl", "TenantName" }),

        new Definition(
            EmailTemplateKey.PasswordReset,
            "Password Reset",
            "Sent when an administrator resets a user's password. Includes the new temporary password.",
            "Your {{TenantName}} password has been reset",
            """
            <p>Hello {{FullName}},</p>
            <p>Your password for <strong>{{TenantName}}</strong> has been reset by an administrator.</p>
            <p>Sign in with the temporary password below; you'll be prompted to choose a new one:</p>
            <ul>
              <li><strong>Temporary password:</strong> {{TemporaryPassword}}</li>
            </ul>
            <p><a href="{{LoginUrl}}">Sign in to your account</a></p>
            <p>If you did not request this, please contact your administrator immediately.</p>
            """,
            new[] { "FullName", "TemporaryPassword", "LoginUrl", "TenantName" }),

        new Definition(
            EmailTemplateKey.PasswordChanged,
            "Password Changed",
            "Confirmation sent after a user changes their own password.",
            "Your {{TenantName}} password was changed",
            """
            <p>Hello {{FullName}},</p>
            <p>This is a confirmation that your password for <strong>{{TenantName}}</strong> was changed on {{ChangedAtUtc}} (UTC).</p>
            <p>If you did not make this change, please contact your administrator immediately.</p>
            """,
            new[] { "FullName", "TenantName", "ChangedAtUtc" }),

        new Definition(
            EmailTemplateKey.Welcome,
            "Welcome",
            "A general welcome message sent when an account becomes active.",
            "Welcome to {{TenantName}}",
            """
            <p>Hello {{FullName}},</p>
            <p>Welcome to <strong>{{TenantName}}</strong>! Your account is now active.</p>
            <p><a href="{{LoginUrl}}">Sign in to get started</a></p>
            """,
            new[] { "FullName", "TenantName", "LoginUrl" }),

        // ---- Customer workflow ----

        new Definition(
            EmailTemplateKey.CustomerSubmitted,
            "Customer Submitted (needs review)",
            "Sent to reviewers when a customer request is submitted and awaits review.",
            "Customer request {{CustomerRequestNumber}} needs review",
            """
            <p>A new customer request has been submitted and is awaiting review.</p>
            <ul>
              <li><strong>Customer:</strong> {{CustomerName}}</li>
              <li><strong>Reference:</strong> {{CustomerRequestNumber}}</li>
              <li><strong>Submitted by:</strong> {{SubmitterName}}</li>
            </ul>
            <p><a href="{{LoginUrl}}">Review the request</a></p>
            """,
            new[] { "CustomerName", "CustomerRequestNumber", "SubmitterName", "TenantName", "LoginUrl" }),

        new Definition(
            EmailTemplateKey.CustomerSentForApproval,
            "Customer Sent for Approval (needs approval)",
            "Sent to approvers when a customer request is sent for approval.",
            "Customer request {{CustomerRequestNumber}} needs approval",
            """
            <p>A customer request has been sent for approval.</p>
            <ul>
              <li><strong>Customer:</strong> {{CustomerName}}</li>
              <li><strong>Reference:</strong> {{CustomerRequestNumber}}</li>
            </ul>
            <p><a href="{{LoginUrl}}">Review and approve</a></p>
            """,
            new[] { "CustomerName", "CustomerRequestNumber", "TenantName", "LoginUrl" }),

        new Definition(
            EmailTemplateKey.CustomerApproved,
            "Customer Approved",
            "Sent to the submitter when their customer request is approved.",
            "Your customer request {{CustomerRequestNumber}} was approved",
            """
            <p>Hello {{SubmitterName}},</p>
            <p>Your customer request for <strong>{{CustomerName}}</strong> ({{CustomerRequestNumber}}) has been approved by {{ApproverName}} and is being synced to Maconomy.</p>
            <p><a href="{{LoginUrl}}">View the request</a></p>
            """,
            new[] { "SubmitterName", "CustomerName", "CustomerRequestNumber", "ApproverName", "TenantName", "LoginUrl" }),

        new Definition(
            EmailTemplateKey.CustomerRejected,
            "Customer Rejected",
            "Sent to the submitter when their customer request is rejected.",
            "Your customer request {{CustomerRequestNumber}} was rejected",
            """
            <p>Hello {{SubmitterName}},</p>
            <p>Your customer request for <strong>{{CustomerName}}</strong> ({{CustomerRequestNumber}}) has been rejected by {{ApproverName}}.</p>
            <p><strong>Reason:</strong> {{Notes}}</p>
            <p><a href="{{LoginUrl}}">View the request</a></p>
            """,
            new[] { "SubmitterName", "CustomerName", "CustomerRequestNumber", "ApproverName", "Notes", "TenantName", "LoginUrl" }),

        new Definition(
            EmailTemplateKey.CustomerReturned,
            "Customer Returned for Corrections",
            "Sent to the submitter when their customer request is returned for corrections.",
            "Customer request {{CustomerRequestNumber}} needs corrections",
            """
            <p>Hello {{SubmitterName}},</p>
            <p>Your customer request for <strong>{{CustomerName}}</strong> ({{CustomerRequestNumber}}) was returned for corrections.</p>
            <p><strong>Notes:</strong> {{Notes}}</p>
            <p><a href="{{LoginUrl}}">Make the corrections</a></p>
            """,
            new[] { "SubmitterName", "CustomerName", "CustomerRequestNumber", "Notes", "TenantName", "LoginUrl" }),

        new Definition(
            EmailTemplateKey.CustomerSynced,
            "Customer Synced",
            "Sent to the submitter when their customer request syncs to Maconomy successfully.",
            "Customer {{CustomerName}} synced to Maconomy",
            """
            <p>Hello {{SubmitterName}},</p>
            <p>Your customer request for <strong>{{CustomerName}}</strong> ({{CustomerRequestNumber}}) has been synced to Maconomy as customer <strong>{{MaconomyCustomerNumber}}</strong>.</p>
            <p><a href="{{LoginUrl}}">View the request</a></p>
            """,
            new[] { "SubmitterName", "CustomerName", "CustomerRequestNumber", "MaconomyCustomerNumber", "TenantName", "LoginUrl" }),

        new Definition(
            EmailTemplateKey.CustomerSyncFailed,
            "Customer Sync Failed",
            "Sent to the submitter when their customer request fails to sync to Maconomy.",
            "Customer request {{CustomerRequestNumber}} failed to sync",
            """
            <p>Hello {{SubmitterName}},</p>
            <p>Your customer request for <strong>{{CustomerName}}</strong> ({{CustomerRequestNumber}}) could not be synced to Maconomy.</p>
            <p><strong>Error:</strong> {{ErrorMessage}}</p>
            <p>The team has been notified. You can retry the sync from the request once the issue is resolved.</p>
            <p><a href="{{LoginUrl}}">View the request</a></p>
            """,
            new[] { "SubmitterName", "CustomerName", "CustomerRequestNumber", "ErrorMessage", "TenantName", "LoginUrl" }),

        new Definition(
            EmailTemplateKey.MentionReceived,
            "Mention Received",
            "Sent when a user is @mentioned in a note on any record (Universal Features).",
            "{{Title}}",
            """
            <p>Hello {{FullName}},</p>
            <p>{{Title}}</p>
            <blockquote>{{Body}}</blockquote>
            <p><a href="{{LoginUrl}}">Open the record</a></p>
            """,
            new[] { "FullName", "Title", "Body", "TenantName", "LoginUrl" }),

        new Definition(
            EmailTemplateKey.ReminderDue,
            "Reminder Due",
            "Sent when a reminder a user set on a record reaches its due time (Universal Features).",
            "{{Title}}",
            """
            <p>Hello {{FullName}},</p>
            <p>{{Title}}</p>
            <blockquote>{{Body}}</blockquote>
            <p><a href="{{LoginUrl}}">Open the record</a></p>
            """,
            new[] { "FullName", "Title", "Body", "TenantName", "LoginUrl" }),
    }.ToDictionary(d => d.Key);

    /// <summary>All template definitions, in key order.</summary>
    public static IReadOnlyList<Definition> All { get; } = Map.Values.OrderBy(d => d.Key).ToList();

    /// <summary>The default definition for a key.</summary>
    public static Definition For(EmailTemplateKey key) => Map[key];
}
