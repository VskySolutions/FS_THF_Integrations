namespace IntegrationHub.Application.Abstractions.Connectors.Maconomy;

/// <summary>Result of a Maconomy write. <see cref="Duplicate"/> indicates the record already existed.</summary>
public sealed record MaconomyWriteResult(string EntityId, bool Duplicate);

public sealed record MaconomyEmployee(string EmployeeId, string Name, string Status, string? Email);

public sealed record MaconomyTimesheet(string EmployeeId, DateTime PeriodStart, decimal Hours);

public sealed record MaconomyReimbursement(string EmployeeId, decimal Amount, string CurrencyCode);

public sealed record MaconomyExpenseReport(
    string ReportId,
    string EmployeeId,
    decimal TotalAmount,
    string CurrencyCode,
    IReadOnlyList<MaconomyExpenseLine> Lines);

public sealed record MaconomyExpenseLine(string Description, decimal Amount);

public sealed record MaconomyVendorInvoice(
    string InvoiceNumber,
    string VendorId,
    decimal Amount,
    string CurrencyCode,
    IReadOnlyList<MaconomyVendorInvoiceLine> Lines);

public sealed record MaconomyVendorInvoiceLine(string Description, decimal Amount);

public sealed record MaconomyVendorPayment(string PaymentId, string InvoiceNumber, decimal Amount);
