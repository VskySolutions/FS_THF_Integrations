using IntegrationHub.Shared.Connectors;
using MediatR;

namespace IntegrationHub.Application.Concur;

/// <summary>Runs the expense import flow for an existing integration job.</summary>
public sealed record ImportConcurExpensesCommand(Guid JobId) : IRequest<IntegrationFlowResult>;

/// <summary>MediatR handler delegating to <see cref="ExpenseImportIntegrationService"/>.</summary>
public sealed class ExpenseImportHandler : IRequestHandler<ImportConcurExpensesCommand, IntegrationFlowResult>
{
    private readonly ExpenseImportIntegrationService _service;

    public ExpenseImportHandler(ExpenseImportIntegrationService service)
    {
        _service = service;
    }

    public Task<IntegrationFlowResult> Handle(ImportConcurExpensesCommand request, CancellationToken cancellationToken)
        => _service.ExecuteAsync(request.JobId, cancellationToken);
}
