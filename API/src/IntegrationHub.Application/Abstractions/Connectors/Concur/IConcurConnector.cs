using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Connectors;

namespace IntegrationHub.Application.Abstractions.Connectors.Concur;

/// <summary>
/// Connector for the Concur Travel &amp; Expense API (source system, fetch-only). Resolves
/// its credentials per-tenant at runtime and returns normalized <see cref="ConnectorResult{T}"/>
/// values. Callers depend only on this interface (AC-COF-001.2).
/// </summary>
public interface IConcurConnector
{
    SystemName System => SystemName.Concur;

    /// <summary>Authenticates with Concur (OAuth2), caching the token in-memory.</summary>
    Task<ConnectorResult<bool>> AuthenticateAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches approved expense report headers and lines.</summary>
    Task<ConnectorResult<IReadOnlyList<ConcurExpenseReport>>> GetApprovedExpenseReportsAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches vendor invoice headers and lines.</summary>
    Task<ConnectorResult<IReadOnlyList<ConcurVendorInvoice>>> GetVendorInvoicesAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches vendor payment detail records.</summary>
    Task<ConnectorResult<IReadOnlyList<ConcurVendorPayment>>> GetVendorPaymentsAsync(CancellationToken cancellationToken = default);
}
