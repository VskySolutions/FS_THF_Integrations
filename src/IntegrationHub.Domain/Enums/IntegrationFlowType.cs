namespace IntegrationHub.Domain.Enums;

/// <summary>
/// The integration flows supported by the platform. Each flow is realized by an
/// <c>IIntegrationService</c> wiring a source connector to a target connector.
/// </summary>
public enum IntegrationFlowType
{
    EmployeeSync = 0,
    TimesheetExport = 1,
    ReimbursementExport = 2,
    ExpenseImport = 3,
    InvoiceImport = 4,
    VendorPaymentImport = 5
}
