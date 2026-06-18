using IntegrationHub.Application.Abstractions.Customers;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Customers;

/// <summary>
/// Default <see cref="ICustomerApprovalService"/> implementation. Encapsulates the workflow
/// state machine and is the single place final approval triggers a Maconomy sync.
/// </summary>
public sealed class CustomerApprovalService : ICustomerApprovalService
{
    private readonly ICustomerAuditRepository _audit;
    private readonly ICustomerSyncDispatcher _syncDispatcher;
    private readonly IUnitOfWork _unitOfWork;

    public CustomerApprovalService(
        ICustomerAuditRepository audit,
        ICustomerSyncDispatcher syncDispatcher,
        IUnitOfWork unitOfWork)
    {
        _audit = audit;
        _syncDispatcher = syncDispatcher;
        _unitOfWork = unitOfWork;
    }

    public IReadOnlyList<string> GetMissingMandatoryStep2Fields(CustomerRequest request)
    {
        var missing = new List<string>();
        void Require(string field, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add(field);
            }
        }

        Require("Tax Number", request.TaxNumber);
        Require("Registration Number", request.RegistrationNumber);
        Require("Business Unit", request.BusinessUnit);
        Require("Currency", request.Currency);
        Require("Payment Terms", request.PaymentTerms);
        return missing;
    }

    public async Task ApproveAsync(CustomerRequest request, Guid? actorId, string? actorName, CancellationToken cancellationToken = default)
    {
        if (request.Status is not (CustomerRequestStatus.PendingApproval or CustomerRequestStatus.PartiallyApproved))
        {
            throw new CustomerWorkflowException("The request is not awaiting approval.", $"Current status: {request.Status}.");
        }

        var missing = GetMissingMandatoryStep2Fields(request);
        if (missing.Count > 0)
        {
            throw new CustomerWorkflowException(
                "All mandatory Step 2 fields must be completed before approval.",
                $"Missing: {string.Join(", ", missing)}.");
        }

        request.CurrentApprovalStage++;
        var isFinal = request.CurrentApprovalStage >= request.RequiredApprovalStages;

        if (isFinal)
        {
            request.ApprovedById = actorId;
            request.ApprovedOnUtc = DateTime.UtcNow;
            // Approved → immediately queued for sync (REQ-CUS-010.1).
            request.Status = CustomerRequestStatus.SyncInProgress;
            await AppendAuditAsync(request, CustomerAuditActionType.Approved, actorId, actorName,
                "Final approval granted; Maconomy sync enqueued.", cancellationToken);
        }
        else
        {
            request.Status = CustomerRequestStatus.PartiallyApproved;
            await AppendAuditAsync(request, CustomerAuditActionType.Approved, actorId, actorName,
                $"Stage {request.CurrentApprovalStage} of {request.RequiredApprovalStages} approved.", cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Enqueue only after the SyncInProgress state is committed so the job sees a consistent row.
        if (isFinal)
        {
            _syncDispatcher.Enqueue(request.Id, request.TenantId);
        }
    }

    public async Task RejectAsync(CustomerRequest request, string reason, Guid? actorId, string? actorName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new CustomerWorkflowException("A rejection reason is required.");
        }
        if (request.Status is CustomerRequestStatus.Synced or CustomerRequestStatus.SyncInProgress)
        {
            throw new CustomerWorkflowException("A synced or in-progress request cannot be rejected.", $"Current status: {request.Status}.");
        }

        request.Status = CustomerRequestStatus.Rejected;
        request.RejectionReason = reason.Trim();
        await AppendAuditAsync(request, CustomerAuditActionType.Rejected, actorId, actorName, reason.Trim(), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ReturnAsync(CustomerRequest request, string notes, IReadOnlyList<string> fields, Guid? actorId, string? actorName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new CustomerWorkflowException("Correction notes are required when returning a request.");
        }

        request.Status = CustomerRequestStatus.Returned;
        request.ReturnNotes = notes.Trim();
        request.UnlockedFields = fields is { Count: > 0 }
            ? System.Text.Json.JsonSerializer.Serialize(fields)
            : null;
        // A returned request restarts its approval chain on resubmission.
        request.CurrentApprovalStage = 0;
        await AppendAuditAsync(request, CustomerAuditActionType.Returned, actorId, actorName,
            notes.Trim(), cancellationToken, fields is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(fields) : null);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Task AppendAuditAsync(
        CustomerRequest request,
        CustomerAuditActionType action,
        Guid? actorId,
        string? actorName,
        string? notes,
        CancellationToken cancellationToken,
        string? fieldsAffected = null)
        => _audit.AddAsync(new CustomerAuditEntry
        {
            Id = Guid.NewGuid(),
            CustomerRequestId = request.Id,
            TenantId = request.TenantId,
            ActionType = action,
            PerformedById = actorId,
            PerformedBy = actorName,
            PerformedOnUtc = DateTime.UtcNow,
            Notes = notes,
            FieldsAffected = fieldsAffected,
        }, cancellationToken);
}
