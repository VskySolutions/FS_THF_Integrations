using global::Hangfire;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.Tenancy;
using IntegrationHub.Application.Concur;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Connectors;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Jobs;

/// <summary>
/// Base for the recurring Concur import jobs. <see cref="RunForJobAsync"/> executes an
/// already-created job (API-triggered path); <see cref="RunRecurringAsync"/> is the
/// scheduled entry point that fans out across active tenants, creating a job and
/// dispatching the MediatR command for each. Lives in Infrastructure so the API can
/// enqueue it and the Worker can execute it.
/// </summary>
public abstract class ConcurImportJobBase
{
    private readonly IMediator _mediator;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenantContext;
    private readonly IIntegrationJobRepository _jobs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _logger;

    protected ConcurImportJobBase(
        IMediator mediator,
        ITenantRepository tenants,
        ITenantContext tenantContext,
        IIntegrationJobRepository jobs,
        IUnitOfWork unitOfWork,
        ILogger logger)
    {
        _mediator = mediator;
        _tenants = tenants;
        _tenantContext = tenantContext;
        _jobs = jobs;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    protected abstract string InterfaceName { get; }

    protected abstract IRequest<IntegrationFlowResult> CreateCommand(Guid jobId);

    /// <summary>Runs the flow for an existing job (tenant already reconstructed by the Hangfire filter).</summary>
    public Task RunForJobAsync(Guid jobId, CancellationToken cancellationToken)
        => _mediator.Send(CreateCommand(jobId), cancellationToken);

    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task RunRecurringAsync(CancellationToken cancellationToken)
    {
        var tenants = await _tenants.ListAsync(cancellationToken);
        foreach (var tenant in tenants.Where(t => t.Status == TenantStatus.Active))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _tenantContext.Set(tenant.Id, tenant.Identifier);

            var job = new IntegrationJob
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                InterfaceName = InterfaceName,
                Direction = IntegrationDirection.Inbound,
                SourceSystem = SystemName.Concur,
                TargetSystem = SystemName.Maconomy,
                Status = IntegrationJobStatus.Created,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _jobs.AddAsync(job, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Scheduled {Interface} job {JobId} for tenant {Tenant}", InterfaceName, job.Id, tenant.Identifier);
            await _mediator.Send(CreateCommand(job.Id), cancellationToken);
        }
    }
}

public sealed class ExpenseImportJob : ConcurImportJobBase
{
    public const string Name = "ExpenseImportJob";

    public ExpenseImportJob(IMediator mediator, ITenantRepository tenants, ITenantContext tenantContext, IIntegrationJobRepository jobs, IUnitOfWork unitOfWork, ILogger<ExpenseImportJob> logger)
        : base(mediator, tenants, tenantContext, jobs, unitOfWork, logger) { }

    protected override string InterfaceName => "ExpenseImport";

    protected override IRequest<IntegrationFlowResult> CreateCommand(Guid jobId) => new ImportConcurExpensesCommand(jobId);
}

public sealed class InvoiceImportJob : ConcurImportJobBase
{
    public const string Name = "InvoiceImportJob";

    public InvoiceImportJob(IMediator mediator, ITenantRepository tenants, ITenantContext tenantContext, IIntegrationJobRepository jobs, IUnitOfWork unitOfWork, ILogger<InvoiceImportJob> logger)
        : base(mediator, tenants, tenantContext, jobs, unitOfWork, logger) { }

    protected override string InterfaceName => "InvoiceImport";

    protected override IRequest<IntegrationFlowResult> CreateCommand(Guid jobId) => new ImportVendorInvoicesCommand(jobId);
}

public sealed class VendorPaymentImportJob : ConcurImportJobBase
{
    public const string Name = "VendorPaymentImportJob";

    public VendorPaymentImportJob(IMediator mediator, ITenantRepository tenants, ITenantContext tenantContext, IIntegrationJobRepository jobs, IUnitOfWork unitOfWork, ILogger<VendorPaymentImportJob> logger)
        : base(mediator, tenants, tenantContext, jobs, unitOfWork, logger) { }

    protected override string InterfaceName => "VendorPaymentImport";

    protected override IRequest<IntegrationFlowResult> CreateCommand(Guid jobId) => new ImportVendorPaymentsCommand(jobId);
}
