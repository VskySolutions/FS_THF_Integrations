using System.Text.Json;
using IntegrationHub.Api.Models.Customers;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Customers;
using IntegrationHub.Application.Abstractions.Email;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Customer Management workflow API. Any authenticated user may list/create/submit requests;
/// enrichment requires <c>customers.review</c> and Step 2 + approval actions require
/// <c>customers.approve</c>. Fully tenant-scoped, with a Super Admin <c>?tenantId=</c> override.
/// </summary>
[ApiController]
[Authorize]
[Route("/api/customers")]
[Produces("application/json")]
[Tags("Customers")]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ApiErrorResponse>(StatusCodes.Status500InternalServerError)]
public sealed class CustomersController : ControllerBase
{
    private static readonly string[] AllowedDocumentExtensions =
        { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt", ".png", ".jpg", ".jpeg" };

    private readonly ICustomerRequestRepository _requests;
    private readonly ICustomerAuditRepository _audit;
    private readonly ICustomerDocumentRepository _documents;
    private readonly ITenantRepository _tenants;
    private readonly IAddressRepository _addresses;
    private readonly ICustomerApprovalService _approval;
    private readonly ICustomerDuplicateChecker _duplicates;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWebHostEnvironment _environment;
    private readonly IEmailNotificationService _emailNotifications;

    public CustomersController(
        ICustomerRequestRepository requests,
        ICustomerAuditRepository audit,
        ICustomerDocumentRepository documents,
        ITenantRepository tenants,
        IAddressRepository addresses,
        ICustomerApprovalService approval,
        ICustomerDuplicateChecker duplicates,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IWebHostEnvironment environment,
        IEmailNotificationService emailNotifications)
    {
        _requests = requests;
        _audit = audit;
        _documents = documents;
        _tenants = tenants;
        _addresses = addresses;
        _approval = approval;
        _duplicates = duplicates;
        _users = users;
        _unitOfWork = unitOfWork;
        _environment = environment;
        _emailNotifications = emailNotifications;
    }

    // ---- List ----

    [HttpGet]
    [ProducesResponseType<ApiResponse<IEnumerable<CustomerSummaryResponse>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] Guid? submittedById,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        // Super Admins may scope to any tenant; others are pinned to their active tenant by the ambient filter.
        Guid? scopeTenant = null;
        if (User.IsSuperAdmin() && tenantId is { } tid)
        {
            scopeTenant = tid;
        }

        CustomerRequestStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CustomerRequestStatus>(status, ignoreCase: true, out var parsed))
        {
            statusFilter = parsed;
        }

        // Drafts are visible only to their creator.
        var (items, total) = await _requests.ListAsync(
            search, scopeTenant, statusFilter, submittedById, fromUtc, toUtc, User.GetUserId(), Math.Max(1, page), Math.Clamp(limit, 1, 100), cancellationToken);

        var data = items.Select(c => new CustomerSummaryResponse(
            c.Id, c.CustomerRequestNumber, c.CompanyName, c.LegalName, c.Status.ToString(),
            c.SubmittedById, c.TenantId, c.Tenant?.Name, c.MaconomyCustomerNumber, c.CreatedOnUtc, c.UpdatedOnUtc));

        return Ok(ApiResponseFactory.Paginated(data, "Customers retrieved.", page, limit, total));
    }

    // ---- Detail ----

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<CustomerDetailResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }

        var auditTrail = await _audit.ListByCustomerAsync(id, cancellationToken);
        // Resolve the actor ids to display names for the change-history "performed by" column.
        var actorNames = await _users.GetFullNamesAsync(
            auditTrail.Where(a => a.PerformedById.HasValue).Select(a => a.PerformedById!.Value), cancellationToken);
        return Ok(ApiResponseFactory.Success(ToDetail(request, auditTrail, actorNames), "Customer request retrieved."));
    }

    // ---- Create (Draft) ----

    [HttpPost]
    [RequirePermission(Permissions.CustomersDataEntry)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest body, CancellationToken cancellationToken)
    {
        var (tenantId, error) = await ResolveTargetTenantAsync(body.TenantId, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var request = new CustomerRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Status = CustomerRequestStatus.Draft,
            // The Customer Request Number is assigned at creation (so even Drafts carry a reference).
            CustomerRequestNumber = await NextCustomerRequestNumberAsync(tenantId, cancellationToken),
            LegalName = body.LegalName.Trim(),
            CompanyName = body.CompanyName.Trim(),
            ContactPerson = body.ContactPerson?.Trim(),
            EmailAddress = body.EmailAddress.Trim(),
            PhoneNumber = body.PhoneNumber?.Trim(),
            Website = body.Website?.Trim(),
        };

        // The address lives in the shared Address table, linked via AddressId.
        var address = new Address { Id = Guid.NewGuid() };
        ApplyAddress(address, body.Country, body.StateProvince, body.City, body.AddressLine1, body.AddressLine2, body.PostalCode);
        await _addresses.AddAsync(address, cancellationToken);
        request.AddressId = address.Id;
        request.Address = address;

        await _requests.AddAsync(request, cancellationToken);
        await AppendAuditAsync(request, CustomerAuditActionType.Created, "Draft created.", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = request.Id },
            ApiResponseFactory.Success(new { customerId = request.Id }, "Customer draft created."));
    }

    // ---- Update (Draft / Returned) ----

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CustomersDataEntry)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest body, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }
        if (request.Status is not (CustomerRequestStatus.Draft or CustomerRequestStatus.Returned))
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Only Draft or Returned requests can be edited.", request.Status.ToString()));
        }

        request.LegalName = body.LegalName.Trim();
        request.CompanyName = body.CompanyName.Trim();
        request.ContactPerson = body.ContactPerson?.Trim();
        request.EmailAddress = body.EmailAddress.Trim();
        request.PhoneNumber = body.PhoneNumber?.Trim();
        request.Website = body.Website?.Trim();
        await UpsertAddressAsync(request, body.Country, body.StateProvince, body.City, body.AddressLine1, body.AddressLine2, body.PostalCode, cancellationToken);
        _requests.Update(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { customerId = request.Id }, "Customer request updated."));
    }

    // ---- Submit ----

    [HttpPost("{id:guid}/submit")]
    [RequirePermission(Permissions.CustomersDataEntry)]
    [ProducesResponseType<ApiResponse<SubmitCustomerResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitCustomerRequest body, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }
        if (request.Status is not (CustomerRequestStatus.Draft or CustomerRequestStatus.Returned))
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Only Draft or Returned requests can be submitted.", request.Status.ToString()));
        }

        // Advisory duplicate check on Step 1 fields (REQ-CUS-003).
        var dupes = await _duplicates.CheckStep1Async(request.TenantId, request, cancellationToken);
        if (dupes.Count > 0 && !body.DuplicateAcknowledged)
        {
            return Ok(ApiResponseFactory.Success(
                new SubmitCustomerResponse(false, request.CustomerRequestNumber, request.Status.ToString(), MapDuplicates(dupes)),
                "Potential duplicates found."));
        }

        if (dupes.Count > 0)
        {
            await AppendAuditAsync(request, CustomerAuditActionType.DuplicateAcknowledged,
                $"Acknowledged {dupes.Count} potential Step 1 duplicate(s).", cancellationToken);
        }

        // The number is assigned at creation; assign one here only as a fallback for any legacy draft
        // created before that change.
        if (string.IsNullOrEmpty(request.CustomerRequestNumber))
        {
            request.CustomerRequestNumber = await NextCustomerRequestNumberAsync(request.TenantId, cancellationToken);
        }

        // Stamp the submitter on the first submission.
        if (request.SubmittedOnUtc is null)
        {
            request.SubmittedById = User.GetUserId();
            request.SubmittedOnUtc = DateTime.UtcNow;
        }

        request.Status = CustomerRequestStatus.Submitted;
        request.UnlockedFields = null;
        request.CurrentApprovalStage = 0;
        _requests.Update(request);
        await AppendAuditAsync(request, CustomerAuditActionType.Submitted, "Submitted for review.", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyTenantAdminsAsync(request, EmailTemplateKey.CustomerSubmitted, null, cancellationToken);

        return Ok(ApiResponseFactory.Success(
            new SubmitCustomerResponse(true, request.CustomerRequestNumber, request.Status.ToString(), Array.Empty<CustomerDuplicateMatchResponse>()),
            "Customer request submitted."));
    }

    // ---- Enrich (customers.review) ----

    [HttpPost("{id:guid}/enrich")]
    [RequirePermission(Permissions.CustomersReview)]
    public async Task<IActionResult> Enrich(Guid id, [FromBody] EnrichCustomerRequest body, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }
        if (request.Status is not (CustomerRequestStatus.Submitted or CustomerRequestStatus.UnderReview))
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Only Submitted or Under Review requests can be enriched.", request.Status.ToString()));
        }

        request.InternalCustomerCategory = body.InternalCustomerCategory?.Trim();
        request.Territory = body.Territory?.Trim();
        request.PracticeArea = body.PracticeArea?.Trim();
        request.SalesRepresentative = body.SalesRepresentative?.Trim();
        request.EnrichmentPaymentTerms = body.EnrichmentPaymentTerms?.Trim();
        request.CreditTerms = body.CreditTerms?.Trim();
        request.CustomerType = body.CustomerType?.Trim();
        request.BusinessSegment = body.BusinessSegment?.Trim();
        request.RiskCategory = body.RiskCategory?.Trim();
        request.Status = CustomerRequestStatus.UnderReview;
        _requests.Update(request);
        await AppendAuditAsync(request, CustomerAuditActionType.Enriched, "Enrichment saved.", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { customerId = request.Id, status = request.Status.ToString() }, "Enrichment saved."));
    }

    // ---- Send for approval (customers.review) ----

    [HttpPost("{id:guid}/send-for-approval")]
    [RequirePermission(Permissions.CustomersReview)]
    public async Task<IActionResult> SendForApproval(Guid id, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }
        if (request.Status is not (CustomerRequestStatus.Submitted or CustomerRequestStatus.UnderReview))
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Only a reviewed request can be sent for approval.", request.Status.ToString()));
        }

        // The mandatory Step 2 Maconomy fields must be completed before the approver receives it.
        var missing = _approval.GetMissingMandatoryStep2Fields(request);
        if (missing.Count > 0)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed,
                "Complete the mandatory Maconomy fields before sending for approval.",
                $"Missing: {string.Join(", ", missing)}."));
        }

        request.Status = CustomerRequestStatus.PendingApproval;
        _requests.Update(request);
        await AppendAuditAsync(request, CustomerAuditActionType.SentForApproval, "Sent for approval.", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyTenantAdminsAsync(request, EmailTemplateKey.CustomerSentForApproval, null, cancellationToken);

        return Ok(ApiResponseFactory.Success(new { customerId = request.Id, status = request.Status.ToString() }, "Sent for approval."));
    }

    // ---- Save Step 2 without approving (customers.approve) ----

    [HttpPost("{id:guid}/step2")]
    public async Task<IActionResult> SaveStep2(Guid id, [FromBody] Step2Fields body, CancellationToken cancellationToken)
    {
        // Step 2 is filled by the reviewer (review stage) and may be amended by the approver (approve stage).
        var canReview = User.HasPermission(Permissions.CustomersReview);
        var canApprove = User.HasPermission(Permissions.CustomersApprove);
        if (!canReview && !canApprove)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("You cannot edit Step 2 fields."));
        }

        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }

        var reviewerStage = request.Status is CustomerRequestStatus.Submitted or CustomerRequestStatus.UnderReview;
        var approverStage = request.Status is CustomerRequestStatus.PendingApproval or CustomerRequestStatus.PartiallyApproved;
        var allowed = (reviewerStage && canReview) || (approverStage && canApprove);
        if (!allowed)
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed,
                "Step 2 can only be edited by the reviewer (under review) or the approver (awaiting approval).", request.Status.ToString()));
        }

        ApplyStep2(request, body);
        _requests.Update(request);
        await AppendAuditAsync(request, CustomerAuditActionType.Step2Saved, "Step 2 Maconomy fields saved.", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { customerId = request.Id }, "Step 2 fields saved."));
    }

    // ---- Approve (customers.approve) ----

    [HttpPost("{id:guid}/approve")]
    [RequirePermission(Permissions.CustomersApprove)]
    [ProducesResponseType<ApiResponse<ApproveCustomerResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveCustomerRequest body, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }

        ApplyStep2(request, body.Step2);

        // Step 2 Tax Number duplicate check (REQ-CUS-007.7) — advisory, before approval proceeds.
        if (!string.IsNullOrWhiteSpace(request.TaxNumber))
        {
            var taxDupes = await _duplicates.CheckTaxNumberAsync(request.TenantId, request.Id, request.TaxNumber!, cancellationToken);
            if (taxDupes.Count > 0 && !body.DuplicateAcknowledged)
            {
                _requests.Update(request); // persist the entered Step 2 so it isn't lost
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Ok(ApiResponseFactory.Success(
                    new ApproveCustomerResponse(false, request.Status.ToString(), MapDuplicates(taxDupes)),
                    "Potential Tax Number duplicates found."));
            }
            if (taxDupes.Count > 0)
            {
                await AppendAuditAsync(request, CustomerAuditActionType.DuplicateAcknowledged,
                    $"Acknowledged {taxDupes.Count} potential Tax Number duplicate(s).", cancellationToken);
            }
        }

        _requests.Update(request);
        try
        {
            await _approval.ApproveAsync(request, User.GetUserId(), ActorName(), cancellationToken);
        }
        catch (CustomerWorkflowException ex)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, ex.Message, ex.Details));
        }

        // Notify the submitter only on the final approval (the request is queued for sync).
        if (request.Status == CustomerRequestStatus.SyncInProgress)
        {
            await NotifySubmitterAsync(request, EmailTemplateKey.CustomerApproved,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["ApproverName"] = await CallerNameAsync(cancellationToken) ?? "an approver" },
                cancellationToken);
        }

        return Ok(ApiResponseFactory.Success(
            new ApproveCustomerResponse(true, request.Status.ToString(), Array.Empty<CustomerDuplicateMatchResponse>()),
            "Customer request approved."));
    }

    // ---- Revert to reviewer (customers.approve): send an awaiting-approval request back to the reviewer ----

    [HttpPost("{id:guid}/revert-to-reviewer")]
    [RequirePermission(Permissions.CustomersApprove)]
    public async Task<IActionResult> RevertToReviewer(Guid id, [FromBody] RevertToReviewerRequest body, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }

        try
        {
            await _approval.RevertToReviewerAsync(request, body.Notes, User.GetUserId(), ActorName(), cancellationToken);
        }
        catch (CustomerWorkflowException ex)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, ex.Message, ex.Details));
        }

        return Ok(ApiResponseFactory.Success(new { customerId = request.Id, status = request.Status.ToString() }, "Customer request reverted to the reviewer."));
    }

    // ---- Reject (customers.approve): reject a request with a mandatory reason ----

    [HttpPost("{id:guid}/reject")]
    [RequirePermission(Permissions.CustomersApprove)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectCustomerRequest body, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }

        try
        {
            await _approval.RejectAsync(request, body.Reason, User.GetUserId(), ActorName(), cancellationToken);
        }
        catch (CustomerWorkflowException ex)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, ex.Message, ex.Details));
        }

        await NotifySubmitterAsync(request, EmailTemplateKey.CustomerRejected,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Notes"] = body.Reason,
                ["ApproverName"] = await CallerNameAsync(cancellationToken) ?? "an approver",
            }, cancellationToken);

        return Ok(ApiResponseFactory.Success(new { customerId = request.Id, status = request.Status.ToString() }, "Customer request rejected."));
    }

    // ---- Return for corrections (customers.review): send a request under review back to data entry ----

    [HttpPost("{id:guid}/return")]
    [RequirePermission(Permissions.CustomersReview)]
    public async Task<IActionResult> Return(Guid id, [FromBody] ReturnCustomerRequest body, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }
        if (request.Status is not (CustomerRequestStatus.Submitted or CustomerRequestStatus.UnderReview))
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed,
                "Only a Submitted or Under Review request can be returned to data entry.", request.Status.ToString()));
        }

        try
        {
            await _approval.ReturnAsync(request, body.Notes, body.Fields, User.GetUserId(), ActorName(), cancellationToken);
        }
        catch (CustomerWorkflowException ex)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, ex.Message, ex.Details));
        }

        await NotifySubmitterAsync(request, EmailTemplateKey.CustomerReturned,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["Notes"] = body.Notes },
            cancellationToken);

        return Ok(ApiResponseFactory.Success(new { customerId = request.Id, status = request.Status.ToString() }, "Customer request returned to data entry."));
    }

    // ---- Retry sync (customers.approve / admin) ----

    [HttpPost("{id:guid}/retry-sync")]
    [RequirePermission(Permissions.CustomersApprove)]
    public async Task<IActionResult> RetrySync(Guid id, [FromServices] ICustomerSyncDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }
        if (request.Status != CustomerRequestStatus.Failed)
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Only a Failed sync can be retried.", request.Status.ToString()));
        }

        request.Status = CustomerRequestStatus.SyncInProgress;
        request.LastSyncError = null;
        _requests.Update(request);
        await AppendAuditAsync(request, CustomerAuditActionType.RetrySyncRequested, "Manual sync retry requested.", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        dispatcher.Enqueue(request.Id, request.TenantId);
        return Ok(ApiResponseFactory.Success(new { customerId = request.Id, status = request.Status.ToString() }, "Sync retry enqueued."));
    }

    // ---- Reopen a rejected request (customers.approve / admin) ----

    [HttpPost("{id:guid}/reopen")]
    [RequirePermission(Permissions.CustomersApprove)]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }
        if (request.Status != CustomerRequestStatus.Rejected)
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Only a Rejected request can be reopened.", request.Status.ToString()));
        }

        // Preserve the original rejection reason; unlock Step 1 and move to Returned (REQ-CUS-015).
        request.Status = CustomerRequestStatus.Returned;
        request.CurrentApprovalStage = 0;
        await AppendAuditAsync(request, CustomerAuditActionType.Reopened,
            $"Reopened from Rejected. Original reason: {request.RejectionReason}", cancellationToken);
        _requests.Update(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(new { customerId = request.Id, status = request.Status.ToString() }, "Customer request reopened."));
    }

    // ---- Delete (Draft only) ----

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.CustomersDataEntry)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }
        if (request.Status != CustomerRequestStatus.Draft)
        {
            return Conflict(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "Only Draft requests can be deleted.", request.Status.ToString()));
        }

        _requests.Remove(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { customerId = id }, "Customer draft deleted."));
    }

    // ---- Documents ----

    [HttpGet("{id:guid}/documents")]
    public async Task<IActionResult> ListDocuments(Guid id, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }
        var docs = await _documents.ListByCustomerAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success(docs.Select(ToDocument), "Documents retrieved."));
    }

    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponseFactory.Error(ApiErrorCodes.ValidationFailed, "A file is required.", "file"));
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedDocumentExtensions.Contains(extension))
        {
            return BadRequest(ApiResponseFactory.Error(
                ApiErrorCodes.ValidationFailed, "Unsupported file type.", $"Allowed: {string.Join(", ", AllowedDocumentExtensions)}"));
        }

        var folder = Path.Combine(_environment.ContentRootPath, "App_Data", "customer-documents", request.TenantId.ToString(), id.ToString());
        Directory.CreateDirectory(folder);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var storedPath = Path.Combine(folder, storedName);
        await using (var stream = System.IO.File.Create(storedPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var document = new CustomerDocument
        {
            Id = Guid.NewGuid(),
            CustomerRequestId = id,
            TenantId = request.TenantId,
            FileName = Path.GetFileName(file.FileName),
            StoredPath = storedPath,
            MimeType = file.ContentType,
            FileSizeBytes = file.Length,
            UploadedById = User.GetUserId(),
            UploadedOnUtc = DateTime.UtcNow,
        };
        await _documents.AddAsync(document, cancellationToken);
        await AppendAuditAsync(request, CustomerAuditActionType.DocumentUploaded, $"Uploaded '{document.FileName}'.", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success(ToDocument(document), "Document uploaded."));
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }
        var document = await _documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null || document.CustomerRequestId != id)
        {
            return NotFound(ApiResponseFactory.NotFound("Document not found."));
        }
        if (!System.IO.File.Exists(document.StoredPath))
        {
            return NotFound(ApiResponseFactory.NotFound("Document content is no longer available."));
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(document.StoredPath, cancellationToken);
        return File(bytes, document.MimeType ?? "application/octet-stream", document.FileName);
    }

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(ApiResponseFactory.NotFound("Customer request not found."));
        }
        var document = await _documents.GetByIdAsync(documentId, cancellationToken);
        if (document is null || document.CustomerRequestId != id)
        {
            return NotFound(ApiResponseFactory.NotFound("Document not found."));
        }

        _documents.Remove(document);
        await AppendAuditAsync(request, CustomerAuditActionType.DocumentRemoved, $"Removed '{document.FileName}'.", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(new { documentId }, "Document removed."));
    }

    // ---- Helpers ----

    /// <summary>Loads a request the caller may access: Super Admins see any tenant; others are tenant-scoped.</summary>
    private async Task<CustomerRequest?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var request = User.IsSuperAdmin()
            ? await _requests.GetByIdUnscopedAsync(id, cancellationToken)
            : await _requests.GetByIdAsync(id, cancellationToken);

        // A Draft is private to its creator — hide it from everyone else (treat as not found).
        if (request is { Status: CustomerRequestStatus.Draft } && request.CreatedById != User.GetUserId())
        {
            return null;
        }

        return request;
    }

    /// <summary>Creates the linked Address on first edit, otherwise updates it in place (shared Address table).</summary>
    private async Task UpsertAddressAsync(
        CustomerRequest request, string country, string? state, string? city,
        string addressLine1, string? addressLine2, string? postalCode, CancellationToken cancellationToken)
    {
        var address = request.AddressId is { } addressId
            ? await _addresses.GetByIdAsync(addressId, cancellationToken)
            : null;

        var isNew = address is null;
        address ??= new Address { Id = Guid.NewGuid() };
        ApplyAddress(address, country, state, city, addressLine1, addressLine2, postalCode);

        if (isNew)
        {
            await _addresses.AddAsync(address, cancellationToken);
            request.AddressId = address.Id;
            request.Address = address;
        }
        else
        {
            _addresses.Update(address);
        }
    }

    /// <summary>Maps the Customer Request's Step 1 location fields onto a shared <see cref="Address"/> record.</summary>
    private static void ApplyAddress(
        Address address, string country, string? state, string? city,
        string addressLine1, string? addressLine2, string? postalCode)
    {
        address.AddressType = AddressType.Office;
        address.CountryName = country.Trim();
        address.StateName = state?.Trim();
        address.CityName = city?.Trim();
        address.AddressLine1 = addressLine1.Trim();
        address.AddressLine2 = addressLine2?.Trim();
        address.PostalCode = postalCode?.Trim();
    }

    /// <summary>Next per-tenant, per-year Customer Request Number (e.g. CUS-2026-000042), assigned at creation.</summary>
    private async Task<string> NextCustomerRequestNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var seq = await _requests.CountForYearAsync(tenantId, year, cancellationToken) + 1;
        return $"CUS-{year}-{seq:D6}";
    }

    /// <summary>Resolves and validates the target tenant for a create: a Super Admin's chosen tenant, otherwise the active tenant.</summary>
    private async Task<(Guid TenantId, IActionResult? Error)> ResolveTargetTenantAsync(Guid? requested, CancellationToken cancellationToken)
    {
        var active = User.GetActiveTenantId();
        if (User.IsSuperAdmin() && requested is { } target && target != active)
        {
            var tenant = await _tenants.GetByIdAsync(target, cancellationToken);
            if (tenant is null)
            {
                return (Guid.Empty, NotFound(ApiResponseFactory.Error(ApiErrorCodes.TenantNotFound, "Tenant not found.", target.ToString())));
            }
            if (tenant.Status != TenantStatus.Active)
            {
                return (Guid.Empty, BadRequest(ApiResponseFactory.Error(ApiErrorCodes.TenantInactive, "The tenant is not active.", target.ToString())));
            }
            return (target, null);
        }

        return active is { } a
            ? (a, null)
            : (Guid.Empty, StatusCode(StatusCodes.Status403Forbidden, ApiResponseFactory.Forbidden("No active tenant for the caller.")));
    }

    private static void ApplyStep2(CustomerRequest request, Step2Fields step2)
    {
        request.TaxNumber = step2.TaxNumber?.Trim();
        request.RegistrationNumber = step2.RegistrationNumber?.Trim();
        request.BusinessUnit = step2.BusinessUnit?.Trim();
        request.Currency = step2.Currency?.Trim();
        request.CustomerGroup = step2.CustomerGroup?.Trim();
        request.PaymentTerms = step2.PaymentTerms?.Trim();
        request.CreditLimit = step2.CreditLimit;
        request.Industry = step2.Industry?.Trim();
        request.InvoiceLanguage = step2.InvoiceLanguage?.Trim();
        request.BillingEmail = step2.BillingEmail?.Trim();
    }

    private CustomerDetailResponse ToDetail(CustomerRequest c, IReadOnlyList<CustomerAuditEntry> auditTrail, IReadOnlyDictionary<Guid, string> actorNames)
    {
        var canApprove = User.HasPermission(Permissions.CustomersApprove);
        var canReview = User.HasPermission(Permissions.CustomersReview);
        var canDataEntry = User.HasPermission(Permissions.CustomersDataEntry);
        var isAdmin = User.IsSuperAdmin() || canApprove;
        var unlocked = ParseUnlocked(c.UnlockedFields);
        var editable = c.Status is CustomerRequestStatus.Draft or CustomerRequestStatus.Returned;
        var reviewStage = c.Status is CustomerRequestStatus.Submitted or CustomerRequestStatus.UnderReview;
        var approveStage = c.Status is CustomerRequestStatus.PendingApproval or CustomerRequestStatus.PartiallyApproved;
        // Step 2 is owned by the reviewer (review stage) and amendable by the approver (approve stage).
        var canSeeStep2 = canReview || canApprove;

        return new CustomerDetailResponse
        {
            Id = c.Id,
            CustomerRequestNumber = c.CustomerRequestNumber,
            Status = c.Status.ToString(),
            TenantId = c.TenantId,
            TenantName = c.Tenant?.Name,
            LegalName = c.LegalName,
            CompanyName = c.CompanyName,
            ContactPerson = c.ContactPerson,
            EmailAddress = c.EmailAddress,
            PhoneNumber = c.PhoneNumber,
            Website = c.Website,
            Country = c.Address?.CountryName ?? string.Empty,
            StateProvince = c.Address?.StateName,
            City = c.Address?.CityName,
            AddressLine1 = c.Address?.AddressLine1 ?? string.Empty,
            AddressLine2 = c.Address?.AddressLine2,
            PostalCode = c.Address?.PostalCode,
            InternalCustomerCategory = c.InternalCustomerCategory,
            Territory = c.Territory,
            PracticeArea = c.PracticeArea,
            SalesRepresentative = c.SalesRepresentative,
            EnrichmentPaymentTerms = c.EnrichmentPaymentTerms,
            CreditTerms = c.CreditTerms,
            CustomerType = c.CustomerType,
            BusinessSegment = c.BusinessSegment,
            RiskCategory = c.RiskCategory,
            // Step 2 is visible to the reviewer and the approver (both work it); hidden from others.
            Step2 = canSeeStep2 ? new Step2Fields
            {
                TaxNumber = c.TaxNumber,
                RegistrationNumber = c.RegistrationNumber,
                BusinessUnit = c.BusinessUnit,
                Currency = c.Currency,
                CustomerGroup = c.CustomerGroup,
                PaymentTerms = c.PaymentTerms,
                CreditLimit = c.CreditLimit,
                Industry = c.Industry,
                InvoiceLanguage = c.InvoiceLanguage,
                BillingEmail = c.BillingEmail,
            } : null,
            MaconomyCustomerNumber = c.MaconomyCustomerNumber,
            SubmittedById = c.SubmittedById,
            SubmittedOnUtc = c.SubmittedOnUtc,
            ApprovedById = c.ApprovedById,
            ApprovedOnUtc = c.ApprovedOnUtc,
            CurrentApprovalStage = c.CurrentApprovalStage,
            RequiredApprovalStages = c.RequiredApprovalStages,
            RejectionReason = c.RejectionReason,
            ReturnNotes = c.ReturnNotes,
            UnlockedFields = unlocked,
            LastSyncError = c.LastSyncError,
            CreatedOnUtc = c.CreatedOnUtc,
            UpdatedOnUtc = c.UpdatedOnUtc,
            MissingStep2Fields = canSeeStep2 ? _approval.GetMissingMandatoryStep2Fields(c) : Array.Empty<string>(),
            AuditTrail = auditTrail.Select(a => new CustomerAuditEntryResponse(
                a.Id, a.ActionType.ToString(), a.PerformedById,
                // Prefer the resolved full name; fall back to the stored value.
                a.PerformedById is { } pid && actorNames.TryGetValue(pid, out var name) ? name : a.PerformedBy,
                a.PerformedOnUtc, a.Notes, a.FieldsAffected)).ToList(),
            Documents = c.Documents.Where(d => !d.Deleted).Select(ToDocument).ToList(),
            Actions = new CustomerActions
            {
                // Data entry: create / edit Step 1 / submit / delete drafts.
                CanEdit = editable && canDataEntry,
                CanDelete = canDataEntry && c.Status == CustomerRequestStatus.Draft,
                CanSubmit = editable && canDataEntry,
                // Reviewer: enrich, fill Step 2, send for approval, or return to data entry.
                CanEnrich = canReview && reviewStage,
                CanSendForApproval = canReview && reviewStage,
                CanReturn = canReview && reviewStage,
                CanViewStep2 = canSeeStep2,
                CanEditStep2 = (canReview && reviewStage) || (canApprove && approveStage),
                // Approver: approve, reject, or revert to the reviewer.
                CanApprove = canApprove && approveStage,
                CanReject = canApprove && approveStage,
                CanRevertToReviewer = canApprove && approveStage,
                CanRetrySync = isAdmin && c.Status == CustomerRequestStatus.Failed,
                CanReopen = isAdmin && c.Status == CustomerRequestStatus.Rejected,
            },
        };
    }

    private static CustomerDocumentResponse ToDocument(CustomerDocument d)
        => new(d.Id, d.FileName, d.MimeType, d.FileSizeBytes, d.UploadedById, d.UploadedOnUtc);

    private static IReadOnlyList<CustomerDuplicateMatchResponse> MapDuplicates(IReadOnlyList<CustomerDuplicateMatch> dupes)
        => dupes.Select(d => new CustomerDuplicateMatchResponse(d.Id, d.CustomerRequestNumber, d.CompanyName, d.MatchedFields)).ToList();

    private static IReadOnlyList<string> ParseUnlocked(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private string? ActorName() => User.Identity?.Name;

    // ---- Workflow notifications (best-effort; the dispatcher never throws) ----

    private static string CustomerNameOf(CustomerRequest r)
        => string.IsNullOrWhiteSpace(r.CompanyName) ? r.LegalName : r.CompanyName;

    /// <summary>Display name of the request's submitter, or null when unknown.</summary>
    private async Task<string?> SubmitterNameAsync(CustomerRequest request, CancellationToken cancellationToken)
    {
        if (request.SubmittedById is not { } sid)
        {
            return null;
        }
        var names = await _users.GetFullNamesAsync(new[] { sid }, cancellationToken);
        return names.TryGetValue(sid, out var name) ? name : null;
    }

    /// <summary>Display name of the acting caller, or null when unknown.</summary>
    private async Task<string?> CallerNameAsync(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } uid)
        {
            return null;
        }
        var names = await _users.GetFullNamesAsync(new[] { uid }, cancellationToken);
        return names.TryGetValue(uid, out var name) ? name : null;
    }

    /// <summary>Emails the request's submitter the given template (no-op when there is no submitter/email).</summary>
    private async Task NotifySubmitterAsync(CustomerRequest request, EmailTemplateKey key, IReadOnlyDictionary<string, string?>? extra, CancellationToken cancellationToken)
    {
        if (request.SubmittedById is not { } sid)
        {
            return;
        }
        var submitter = await _users.GetByIdAsync(sid, cancellationToken);
        if (submitter is null || string.IsNullOrWhiteSpace(submitter.Email))
        {
            return;
        }

        var model = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CustomerName"] = CustomerNameOf(request),
            ["CustomerRequestNumber"] = request.CustomerRequestNumber,
            ["SubmitterName"] = submitter.DisplayName,
        };
        if (extra is not null)
        {
            foreach (var kv in extra)
            {
                model[kv.Key] = kv.Value;
            }
        }

        await _emailNotifications.SendAsync(request.TenantId, key, submitter.Email, model, cancellationToken);
    }

    /// <summary>Emails every active Tenant Admin of the request's tenant the given "action needed" template.</summary>
    private async Task NotifyTenantAdminsAsync(CustomerRequest request, EmailTemplateKey key, IReadOnlyDictionary<string, string?>? extra, CancellationToken cancellationToken)
    {
        var (admins, _) = await _users.ListAsync(
            request.TenantId, search: null, isActive: true, name: null, email: null, phone: null,
            role: Roles.TenantAdmin, group: null, page: 1, limit: 100, cancellationToken);
        if (admins is null || admins.Count == 0)
        {
            return;
        }

        var model = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CustomerName"] = CustomerNameOf(request),
            ["CustomerRequestNumber"] = request.CustomerRequestNumber,
            ["SubmitterName"] = await SubmitterNameAsync(request, cancellationToken),
        };
        if (extra is not null)
        {
            foreach (var kv in extra)
            {
                model[kv.Key] = kv.Value;
            }
        }

        foreach (var admin in admins)
        {
            if (!string.IsNullOrWhiteSpace(admin.Email))
            {
                await _emailNotifications.SendAsync(request.TenantId, key, admin.Email, model, cancellationToken);
            }
        }
    }

    private Task AppendAuditAsync(CustomerRequest request, CustomerAuditActionType action, string notes, CancellationToken cancellationToken)
        => _audit.AddAsync(new CustomerAuditEntry
        {
            Id = Guid.NewGuid(),
            CustomerRequestId = request.Id,
            TenantId = request.TenantId,
            ActionType = action,
            PerformedById = User.GetUserId(),
            PerformedBy = ActorName(),
            PerformedOnUtc = DateTime.UtcNow,
            Notes = notes,
        }, cancellationToken);
}
