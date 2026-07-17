using IntegrationHub.Application.Abstractions.Customers;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.UniversalFeatures;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Application.Customers;

/// <summary>
/// Default <see cref="ICustomerApprovalService"/> implementation. Encapsulates the
/// customer-request approval workflow state machine.
/// </summary>
public sealed class CustomerApprovalService : ICustomerApprovalService
{
    private readonly ICustomerAuditRepository _audit;
    private readonly IActivityEventWriter _activity;
    private readonly IUnitOfWork _unitOfWork;

    public CustomerApprovalService(
        ICustomerAuditRepository audit,
        IActivityEventWriter activity,
        IUnitOfWork unitOfWork)
    {
        _audit = audit;
        _activity = activity;
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

        var previousStatus = request.Status;
        request.CurrentApprovalStage++;
        var isFinal = request.CurrentApprovalStage >= request.RequiredApprovalStages;

        if (isFinal)
        {
            request.ApprovedById = actorId;
            request.ApprovedOnUtc = DateTime.UtcNow;
            // Final approval is the terminal success state of the workflow.
            request.Status = CustomerRequestStatus.Approved;
            await AppendAuditAsync(request, CustomerAuditActionType.Approved, actorId, actorName,
                "Final approval granted.", cancellationToken);
        }
        else
        {
            request.Status = CustomerRequestStatus.PartiallyApproved;
            await AppendAuditAsync(request, CustomerAuditActionType.Approved, actorId, actorName,
                $"Stage {request.CurrentApprovalStage} of {request.RequiredApprovalStages} approved.", cancellationToken);
        }

        await WriteStatusChangeAsync(request, previousStatus, actorId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(CustomerRequest request, string reason, Guid? actorId, string? actorName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new CustomerWorkflowException("A rejection reason is required.");
        }

        var previousStatus = request.Status;
        request.Status = CustomerRequestStatus.Rejected;
        request.RejectionReason = reason.Trim();
        await AppendAuditAsync(request, CustomerAuditActionType.Rejected, actorId, actorName, reason.Trim(), cancellationToken);
        await WriteStatusChangeAsync(request, previousStatus, actorId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RevertToReviewerAsync(CustomerRequest request, string? notes, Guid? actorId, string? actorName, CancellationToken cancellationToken = default)
    {
        if (request.Status is not (CustomerRequestStatus.PendingApproval or CustomerRequestStatus.PartiallyApproved))
        {
            throw new CustomerWorkflowException("Only a request awaiting approval can be reverted to the reviewer.", $"Current status: {request.Status}.");
        }

        var previousStatus = request.Status;
        request.Status = CustomerRequestStatus.UnderReview;
        request.CurrentApprovalStage = 0;
        await AppendAuditAsync(request, CustomerAuditActionType.RevertedToReviewer, actorId, actorName,
            string.IsNullOrWhiteSpace(notes) ? "Reverted to reviewer." : notes.Trim(), cancellationToken);
        await WriteStatusChangeAsync(request, previousStatus, actorId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ReturnAsync(CustomerRequest request, string notes, IReadOnlyList<string> fields, Guid? actorId, string? actorName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new CustomerWorkflowException("Correction notes are required when returning a request.");
        }

        var previousStatus = request.Status;
        request.Status = CustomerRequestStatus.Returned;
        request.ReturnNotes = notes.Trim();
        request.UnlockedFields = fields is { Count: > 0 }
            ? System.Text.Json.JsonSerializer.Serialize(fields)
            : null;
        // A returned request restarts its approval chain on resubmission.
        request.CurrentApprovalStage = 0;
        await AppendAuditAsync(request, CustomerAuditActionType.Returned, actorId, actorName,
            notes.Trim(), cancellationToken, fields is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(fields) : null);
        await WriteStatusChangeAsync(request, previousStatus, actorId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Records a customer-request status change on the Universal Features activity timeline.</summary>
    private Task WriteStatusChangeAsync(CustomerRequest request, CustomerRequestStatus previousStatus, Guid? actorId, CancellationToken cancellationToken)
    {
        if (previousStatus == request.Status)
        {
            return Task.CompletedTask;
        }

        return _activity.WriteAsync(new CreateActivityEventDto(
            EntityType.CustomerRequest, request.Id, ActivityEventTypes.StatusChanged,
            previousStatus.ToString(), request.Status.ToString(), actorId), cancellationToken);
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
