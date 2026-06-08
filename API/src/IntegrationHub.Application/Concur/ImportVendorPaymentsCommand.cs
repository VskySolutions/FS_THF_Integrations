using IntegrationHub.Shared.Connectors;
using MediatR;

namespace IntegrationHub.Application.Concur;

public sealed record ImportVendorPaymentsCommand(Guid JobId) : IRequest<IntegrationFlowResult>;

public sealed class VendorPaymentImportHandler : IRequestHandler<ImportVendorPaymentsCommand, IntegrationFlowResult>
{
    private readonly VendorPaymentImportIntegrationService _service;

    public VendorPaymentImportHandler(VendorPaymentImportIntegrationService service)
    {
        _service = service;
    }

    public Task<IntegrationFlowResult> Handle(ImportVendorPaymentsCommand request, CancellationToken cancellationToken)
        => _service.ExecuteAsync(request.JobId, cancellationToken);
}
