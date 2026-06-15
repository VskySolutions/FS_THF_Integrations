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
    private readonly IJobScheduleConfigurationRepository _schedules;
    private readonly ILogger _logger;

    protected ConcurImportJobBase(
        IMediator mediator,
        ITenantRepository tenants,
        ITenantContext tenantContext,
        IIntegrationJobRepository jobs,
        IUnitOfWork unitOfWork,
        IJobScheduleConfigurationRepository schedules,
        ILogger logger)
    {
        _mediator = mediator;
        _tenants = tenants;
        _tenantContext = tenantContext;
        _jobs = jobs;
        _unitOfWork = unitOfWork;
        _schedules = schedules;
        _logger = logger;
    }

    protected abstract string InterfaceName { get; }

    /// <summary>The recurring job name (matches the schedule's JobName, e.g. "ExpenseImportJob").</summary>
    protected abstract string JobName { get; }

    protected abstract IRequest<IntegrationFlowResult> CreateCommand(Guid jobId);

    /// <summary>Runs the flow for an existing job (tenant already reconstructed by the Hangfire filter).</summary>
    public Task RunForJobAsync(Guid jobId, CancellationToken cancellationToken)
        => _mediator.Send(CreateCommand(jobId), cancellationToken);

    /// <summary>Creates and dispatches an import job for a single tenant (per-tenant schedule).</summary>
    public async Task RunForTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null || tenant.Status != TenantStatus.Active)
        {
            _logger.LogWarning("Skipping {Job} for tenant {Tenant}: not found or inactive", JobName, tenantId);
            return;
        }

        await DispatchForTenantAsync(tenant, cancellationToken);
    }

    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task RunRecurringAsync(CancellationToken cancellationToken)
    {
        var tenants = await _tenants.ListAsync(cancellationToken);
        foreach (var tenant in tenants.Where(t => t.Status == TenantStatus.Active))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A tenant with its own active per-tenant schedule is driven by that schedule — skip here
            // so the global fan-out doesn't double-run it.
            var ownSchedule = await _schedules.GetAsync(JobName, tenant.Id, cancellationToken);
            if (ownSchedule is { IsActive: true })
            {
                continue;
            }

            await DispatchForTenantAsync(tenant, cancellationToken);
        }
    }

    private async Task DispatchForTenantAsync(Tenant tenant, CancellationToken cancellationToken)
    {
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
        };
        await _jobs.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Scheduled {Interface} job {JobId} for tenant {Tenant}", InterfaceName, job.Id, tenant.Identifier);
        await _mediator.Send(CreateCommand(job.Id), cancellationToken);
    }
}

public sealed class ExpenseImportJob : ConcurImportJobBase
{
    public const string Name = "ExpenseImportJob";

    public ExpenseImportJob(IMediator mediator, ITenantRepository tenants, ITenantContext tenantContext, IIntegrationJobRepository jobs, IUnitOfWork unitOfWork, IJobScheduleConfigurationRepository schedules, ILogger<ExpenseImportJob> logger)
        : base(mediator, tenants, tenantContext, jobs, unitOfWork, schedules, logger) { }

    protected override string InterfaceName => "ExpenseImport";

    protected override string JobName => Name;

    protected override IRequest<IntegrationFlowResult> CreateCommand(Guid jobId) => new ImportConcurExpensesCommand(jobId);
}

public sealed class InvoiceImportJob : ConcurImportJobBase
{
    public const string Name = "InvoiceImportJob";

    public InvoiceImportJob(IMediator mediator, ITenantRepository tenants, ITenantContext tenantContext, IIntegrationJobRepository jobs, IUnitOfWork unitOfWork, IJobScheduleConfigurationRepository schedules, ILogger<InvoiceImportJob> logger)
        : base(mediator, tenants, tenantContext, jobs, unitOfWork, schedules, logger) { }

    protected override string InterfaceName => "InvoiceImport";

    protected override string JobName => Name;

    protected override IRequest<IntegrationFlowResult> CreateCommand(Guid jobId) => new ImportVendorInvoicesCommand(jobId);
}

public sealed class VendorPaymentImportJob : ConcurImportJobBase
{
    public const string Name = "VendorPaymentImportJob";

    public VendorPaymentImportJob(IMediator mediator, ITenantRepository tenants, ITenantContext tenantContext, IIntegrationJobRepository jobs, IUnitOfWork unitOfWork, IJobScheduleConfigurationRepository schedules, ILogger<VendorPaymentImportJob> logger)
        : base(mediator, tenants, tenantContext, jobs, unitOfWork, schedules, logger) { }

    protected override string InterfaceName => "VendorPaymentImport";

    protected override string JobName => Name;

    protected override IRequest<IntegrationFlowResult> CreateCommand(Guid jobId) => new ImportVendorPaymentsCommand(jobId);
}
