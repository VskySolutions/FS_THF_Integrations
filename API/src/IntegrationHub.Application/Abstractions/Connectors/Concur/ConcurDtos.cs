namespace IntegrationHub.Application.Abstractions.Connectors.Concur;

/// <summary>Concur expense report header with its line items.</summary>
public sealed record ConcurExpenseReport(
    string ReportId,
    string EmployeeId,
    string Status,
    DateTime? SubmittedDate,
    decimal TotalAmount,
    string CurrencyCode,
    IReadOnlyList<ConcurExpenseLine> Lines);

/// <summary>A single expense report line.</summary>
public sealed record ConcurExpenseLine(
    string LineId,
    string ExpenseType,
    decimal Amount,
    DateTime? TransactionDate,
    string? Description);

/// <summary>Concur vendor invoice header with its line items.</summary>
public sealed record ConcurVendorInvoice(
    string InvoiceId,
    string VendorId,
    string InvoiceNumber,
    DateTime? InvoiceDate,
    decimal Amount,
    string CurrencyCode,
    IReadOnlyList<ConcurVendorInvoiceLine> Lines);

/// <summary>A single vendor invoice line.</summary>
public sealed record ConcurVendorInvoiceLine(
    string LineId,
    string Description,
    decimal Amount);

/// <summary>Concur vendor payment detail.</summary>
public sealed record ConcurVendorPayment(
    string PaymentId,
    string InvoiceId,
    string VendorId,
    decimal Amount,
    DateTime? PaymentDate);
