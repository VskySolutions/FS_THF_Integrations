using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Connectors;

namespace IntegrationHub.Application.Abstractions.Connectors.Maconomy;

/// <summary>
/// Connector for the Maconomy ERP API (target system). Creates and updates employees,
/// timesheets, expense entries, vendor invoices, and vendor payments, and reads employees
/// for duplicate/existence checks. Returns normalized <see cref="ConnectorResult{T}"/>.
/// </summary>
public interface IMaconomyConnector
{
    SystemName System => SystemName.Maconomy;

    Task<ConnectorResult<bool>> AuthenticateAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads an employee by id; payload is null when not found.</summary>
    Task<ConnectorResult<MaconomyEmployee?>> GetEmployeeAsync(string employeeId, CancellationToken cancellationToken = default);

    Task<ConnectorResult<MaconomyWriteResult>> CreateEmployeeAsync(MaconomyEmployee employee, CancellationToken cancellationToken = default);

    Task<ConnectorResult<MaconomyWriteResult>> UpdateEmployeeAsync(MaconomyEmployee employee, CancellationToken cancellationToken = default);

    Task<ConnectorResult<MaconomyWriteResult>> UpdateEmployeeStatusAsync(string employeeId, string status, CancellationToken cancellationToken = default);

    Task<ConnectorResult<MaconomyWriteResult>> WriteTimesheetAsync(MaconomyTimesheet timesheet, CancellationToken cancellationToken = default);

    Task<ConnectorResult<MaconomyWriteResult>> WriteReimbursementAsync(MaconomyReimbursement reimbursement, CancellationToken cancellationToken = default);

    Task<ConnectorResult<MaconomyWriteResult>> WriteExpenseReportAsync(MaconomyExpenseReport report, CancellationToken cancellationToken = default);

    Task<ConnectorResult<MaconomyWriteResult>> WriteVendorInvoiceAsync(MaconomyVendorInvoice invoice, CancellationToken cancellationToken = default);

    Task<ConnectorResult<MaconomyWriteResult>> WriteVendorPaymentAsync(MaconomyVendorPayment payment, CancellationToken cancellationToken = default);
}
