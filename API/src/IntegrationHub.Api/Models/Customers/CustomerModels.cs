namespace IntegrationHub.Api.Models.Customers;

// ---- Requests ----

/// <summary>Step 1 (Basic Information) for creating a Customer Request. Super Admins may pass a target tenant.</summary>
public sealed class CreateCustomerRequest
{
    public Guid? TenantId { get; set; }
    public string LegalName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? StateProvince { get; set; }
    public string? City { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? PostalCode { get; set; }
}

/// <summary>Step 1 update for a Draft or Returned request (unlocked fields only).</summary>
public sealed class UpdateCustomerRequest
{
    public string LegalName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? StateProvince { get; set; }
    public string? City { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? PostalCode { get; set; }
}

/// <summary>Submit a Draft/Returned request for approval. Set <see cref="DuplicateAcknowledged"/> to proceed past a duplicate warning.</summary>
public sealed class SubmitCustomerRequest
{
    public bool DuplicateAcknowledged { get; set; }
}

/// <summary>Enrichment (internal Business Information) saved by a Customer Role User.</summary>
public sealed class EnrichCustomerRequest
{
    public string? InternalCustomerCategory { get; set; }
    public string? Territory { get; set; }
    public string? PracticeArea { get; set; }
    public string? SalesRepresentative { get; set; }
    public string? EnrichmentPaymentTerms { get; set; }
    public string? CreditTerms { get; set; }
    public string? CustomerType { get; set; }
    public string? BusinessSegment { get; set; }
    public string? RiskCategory { get; set; }
}

/// <summary>Step 2 Additional Business Details (Customer Approver only).</summary>
public sealed class Step2Fields
{
    public string? TaxNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? BusinessUnit { get; set; }
    public string? Currency { get; set; }
    public string? CustomerGroup { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal? CreditLimit { get; set; }
    public string? Industry { get; set; }
    public string? InvoiceLanguage { get; set; }
    public string? BillingEmail { get; set; }
}

/// <summary>Approve the current stage, supplying/confirming Step 2 fields.</summary>
public sealed class ApproveCustomerRequest
{
    public Step2Fields Step2 { get; set; } = new();
    public bool DuplicateAcknowledged { get; set; }
}

public sealed class RejectCustomerRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Approver sends an awaiting-approval request back to the reviewer, with optional notes.</summary>
public sealed class RevertToReviewerRequest
{
    public string? Notes { get; set; }
}

public sealed class ReturnCustomerRequest
{
    public string Notes { get; set; } = string.Empty;
    public List<string> Fields { get; set; } = new();
}

// ---- Responses ----

public sealed record CustomerSummaryResponse(
    Guid Id,
    string? CustomerRequestNumber,
    string CompanyName,
    string LegalName,
    string Status,
    Guid? SubmittedById,
    Guid TenantId,
    string? TenantName,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

public sealed record CustomerAuditEntryResponse(
    Guid Id,
    string ActionType,
    Guid? PerformedById,
    string? PerformedBy,
    DateTime PerformedOnUtc,
    string? Notes,
    string? FieldsAffected);

public sealed record CustomerDocumentResponse(
    Guid Id,
    string FileName,
    string? MimeType,
    long FileSizeBytes,
    Guid? UploadedById,
    DateTime UploadedOnUtc);

public sealed record CustomerDuplicateMatchResponse(
    Guid Id,
    string? CustomerRequestNumber,
    string CompanyName,
    IReadOnlyList<string> MatchedFields);

public sealed record SubmitCustomerResponse(
    bool Submitted,
    string? CustomerRequestNumber,
    string Status,
    IReadOnlyList<CustomerDuplicateMatchResponse> Duplicates);

public sealed record ApproveCustomerResponse(
    bool Approved,
    string Status,
    IReadOnlyList<CustomerDuplicateMatchResponse> Duplicates);

/// <summary>Full Customer Request detail. Step 2 fields are null for callers without customers.approve.</summary>
public sealed record CustomerDetailResponse
{
    public Guid Id { get; init; }
    public string? CustomerRequestNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid TenantId { get; init; }
    public string? TenantName { get; init; }

    // Step 1
    public string LegalName { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string? ContactPerson { get; init; }
    public string EmailAddress { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string? Website { get; init; }
    public string Country { get; init; } = string.Empty;
    public string? StateProvince { get; init; }
    public string? City { get; init; }
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string? PostalCode { get; init; }

    // Enrichment
    public string? InternalCustomerCategory { get; init; }
    public string? Territory { get; init; }
    public string? PracticeArea { get; init; }
    public string? SalesRepresentative { get; init; }
    public string? EnrichmentPaymentTerms { get; init; }
    public string? CreditTerms { get; init; }
    public string? CustomerType { get; init; }
    public string? BusinessSegment { get; init; }
    public string? RiskCategory { get; init; }

    // Step 2 (null when masked)
    public Step2Fields? Step2 { get; init; }

    // Workflow
    public Guid? SubmittedById { get; init; }
    public DateTime? SubmittedOnUtc { get; init; }
    public Guid? ApprovedById { get; init; }
    public DateTime? ApprovedOnUtc { get; init; }
    public int CurrentApprovalStage { get; init; }
    public int RequiredApprovalStages { get; init; }
    public string? RejectionReason { get; init; }
    public string? ReturnNotes { get; init; }
    public IReadOnlyList<string> UnlockedFields { get; init; } = Array.Empty<string>();
    public DateTime CreatedOnUtc { get; init; }
    public DateTime UpdatedOnUtc { get; init; }

    public IReadOnlyList<string> MissingStep2Fields { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CustomerAuditEntryResponse> AuditTrail { get; init; } = Array.Empty<CustomerAuditEntryResponse>();
    public IReadOnlyList<CustomerDocumentResponse> Documents { get; init; } = Array.Empty<CustomerDocumentResponse>();

    // Caller capability flags (status + permission aware) to drive the UI action set.
    public CustomerActions Actions { get; init; } = new();
}

/// <summary>Which workflow actions the current caller may take on this request, given status + permissions.</summary>
public sealed record CustomerActions
{
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }
    public bool CanSubmit { get; init; }
    public bool CanEnrich { get; init; }
    public bool CanSendForApproval { get; init; }
    public bool CanViewStep2 { get; init; }
    public bool CanEditStep2 { get; init; }
    public bool CanApprove { get; init; }
    /// <summary>Approver rejects the request with a mandatory reason.</summary>
    public bool CanReject { get; init; }
    /// <summary>Approver sends the request back to the reviewer.</summary>
    public bool CanRevertToReviewer { get; init; }
    /// <summary>Reviewer returns the request to data entry.</summary>
    public bool CanReturn { get; init; }
    public bool CanReopen { get; init; }
}
