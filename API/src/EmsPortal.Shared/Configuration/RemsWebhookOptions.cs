namespace EmsPortal.Shared.Configuration;

/// <summary>
/// Configuration for the REMS provider email-event webhook (WO-121). Bound from the
/// <c>Rems:EmailWebhook</c> section. The <see cref="Secret"/> is a shared secret a delivery provider must
/// present in the <c>X-Rems-Webhook-Secret</c> header; the endpoint compares it in constant time and
/// <b>fails closed</b> — when the secret is empty (unset) every call is rejected with 401.
/// </summary>
public sealed class RemsWebhookOptions
{
    /// <summary>
    /// Shared secret compared (constant-time) against the inbound webhook header. Empty disables the
    /// endpoint (fail closed). Provisioned via secrets at deploy time — not stored in appsettings.json.
    /// HMAC-signature verification is the production upgrade once a specific provider is chosen.
    /// </summary>
    public string Secret { get; set; } = string.Empty;
}
