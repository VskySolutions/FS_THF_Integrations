using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// A customer onboarding request that moves through a structured workflow (Draft → Submitted →
/// Under Review → Pending Approval → Approved). Step 1 (Basic Information) is completed by any
/// platform user; Step 2 (additional business details) is completed exclusively by a Customer
/// Approver. Fully tenant-scoped. Final approval is the terminal success state.
/// </summary>
public class CustomerRequest : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped). Set from the active tenant or a Super Admin override.</summary>
    public Guid TenantId { get; set; }

    /// <summary>System-generated reference (e.g. CUS-2026-000123); assigned on first submit, null while Draft.</summary>
    public string? CustomerRequestNumber { get; set; }

    /// <summary>Current lifecycle status.</summary>
    public CustomerRequestStatus Status { get; set; } = CustomerRequestStatus.Draft;

    // ---- Step 1: Basic Information (any platform user) ----
    public string LegalName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }

    /// <summary>Reference to the shared <see cref="Entities.Address"/> record (the common address table).</summary>
    public Guid? AddressId { get; set; }

    // ---- Enrichment: internal Business Information (customers.review) ----
    public string? InternalCustomerCategory { get; set; }
    public string? Territory { get; set; }
    public string? PracticeArea { get; set; }
    public string? SalesRepresentative { get; set; }
    public string? EnrichmentPaymentTerms { get; set; }
    public string? CreditTerms { get; set; }
    public string? CustomerType { get; set; }
    public string? BusinessSegment { get; set; }

    /// <summary>Risk classification. Optional tracked field — a tenant may disable its change history.</summary>
    [TrackedField(Enums.EntityType.CustomerRequest, "Risk Category", isSystemTracked: false)]
    public string? RiskCategory { get; set; }

    // ---- Step 2: Additional Business Details (customers.approve only) ----
    public string? TaxNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? BusinessUnit { get; set; }
    public string? Currency { get; set; }
    public string? CustomerGroup { get; set; }

    /// <summary>Payment terms. System Tracked field — change history is always captured.</summary>
    [TrackedField(Enums.EntityType.CustomerRequest, "Payment Terms", isSystemTracked: true)]
    public string? PaymentTerms { get; set; }

    /// <summary>Approved credit limit. System Tracked field — change history is always captured.</summary>
    [TrackedField(Enums.EntityType.CustomerRequest, "Credit Limit", isSystemTracked: true)]
    public decimal? CreditLimit { get; set; }
    public string? Industry { get; set; }
    public string? InvoiceLanguage { get; set; }
    public string? BillingEmail { get; set; }

    // ---- Workflow metadata ----
    /// <summary>User who first submitted the request.</summary>
    public Guid? SubmittedById { get; set; }

    /// <summary>UTC timestamp of the first submission.</summary>
    public DateTime? SubmittedOnUtc { get; set; }

    /// <summary>Current zero-based approval stage in a multi-stage flow (MVP: single stage).</summary>
    public int CurrentApprovalStage { get; set; }

    /// <summary>Total number of approval stages required (MVP: 1).</summary>
    public int RequiredApprovalStages { get; set; } = 1;

    /// <summary>User who granted the final approval.</summary>
    public Guid? ApprovedById { get; set; }

    /// <summary>UTC timestamp of final approval.</summary>
    public DateTime? ApprovedOnUtc { get; set; }

    /// <summary>Reason captured when the request was rejected (preserved across reopen).</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Approver's correction notes captured on a Return for Corrections action.</summary>
    public string? ReturnNotes { get; set; }

    /// <summary>JSON array of field names unlocked for editing by a Return/Reopen action.</summary>
    public string? UnlockedFields { get; set; }

    // ---- Navigations ----
    public Tenant? Tenant { get; set; }
    public Address? Address { get; set; }
    public ICollection<CustomerAuditEntry> AuditEntries { get; set; } = new List<CustomerAuditEntry>();
    public ICollection<CustomerDocument> Documents { get; set; } = new List<CustomerDocument>();
}
