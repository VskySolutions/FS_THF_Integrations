// Shared option lists and label maps for SMTP email accounts (WO-81). Centralised so the form
// drawer, the list table, and the test dialog all use identical values/labels (DRY).

// Encryption types — values mirror EmsPortal.Domain.Enums.SmtpEncryptionType names. "Auto" (the
// default) negotiates transport security from the server's capabilities and port.
export const SMTP_ENCRYPTION_OPTIONS = [
  { label: "Auto", value: "Auto" },
  { label: "None", value: "None" },
  { label: "STARTTLS", value: "StartTls" },
  { label: "SSL/TLS", value: "SslTls" }
];

// Auth types — values mirror EmsPortal.Domain.Enums.SmtpAuthType names.
export const SMTP_AUTH_OPTIONS = [
  { label: "None", value: "None" },
  { label: "Plain", value: "Plain" },
  { label: "Login", value: "Login" },
  { label: "CRAM-MD5", value: "CramMd5" }
];

// Friendly labels for the categorised send-failure reasons (mirror SmtpErrorCategory names).
const ERROR_CATEGORY_LABELS = {
  AuthenticationFailure: "Authentication Failed",
  ConnectionRefused: "Connection Refused",
  TlsHandshakeFailure: "TLS Error",
  InvalidRecipient: "Recipient Rejected",
  Timeout: "Timeout",
  Unknown: "Send Failed"
};

export function useSmtpOptions () {
  const encryptionLabel = (value) =>
    SMTP_ENCRYPTION_OPTIONS.find((o) => o.value === value)?.label || value || "—";
  const authLabel = (value) =>
    SMTP_AUTH_OPTIONS.find((o) => o.value === value)?.label || value || "—";
  const errorCategoryLabel = (value) => ERROR_CATEGORY_LABELS[value] || "Send Failed";

  return {
    encryptionOptions: SMTP_ENCRYPTION_OPTIONS,
    authOptions: SMTP_AUTH_OPTIONS,
    encryptionLabel,
    authLabel,
    errorCategoryLabel
  };
}
