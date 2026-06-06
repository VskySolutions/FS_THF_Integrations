using IntegrationHub.Shared.Connectors;
using MediatR;

namespace IntegrationHub.Application.Concur;

public sealed record ImportVendorInvoicesCommand(Guid JobId) : IRequest<IntegrationFlowResult>;

public sealed class VendorInvoiceImportHandler : IRequestHandler<ImportVendorInvoicesCommand, IntegrationFlowResult>
{
    private readonly VendorInvoiceImportIntegrationService _service;

    public VendorInvoiceImportHandler(VendorInvoiceImportIntegrationService service)
    {
        _service = service;
    }

    public Task<IntegrationFlowResult> Handle(ImportVendorInvoicesCommand request, CancellationToken cancellationToken)
        => _service.ExecuteAsync(request.JobId, cancellationToken);
}
