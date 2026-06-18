using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using IntegrationHub.Api.Controllers;
using IntegrationHub.Api.Models.Customers;
using IntegrationHub.Api.Security;
using IntegrationHub.Application.Abstractions.Customers;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Shared.Contracts;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace IntegrationHub.UnitTests;

// WO-66: CustomersController workflow + scoping + masking.
public class CustomerControllerTests
{
    private readonly Mock<ICustomerRequestRepository> _requests = new();
    private readonly Mock<ICustomerAuditRepository> _audit = new();
    private readonly Mock<ICustomerDocumentRepository> _documents = new();
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<ICustomerApprovalService> _approval = new();
    private readonly Mock<ICustomerDuplicateChecker> _duplicates = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IWebHostEnvironment> _environment = new();

    private CustomersController Create() => new(
        _requests.Object, _audit.Object, _documents.Object, _tenants.Object,
        _approval.Object, _duplicates.Object, _unitOfWork.Object, _environment.Object);

    /// <summary>Builds a controller with the given identity claims (subject + role + optional tenant + explicit permissions).</summary>
    private CustomersController CreateWithUser(Guid userId, string role, Guid? tenantId = null, params string[] permissions)
    {
        var controller = Create();
        var claims = new List<Claim>
        {
            new(ClaimTypeNames.Subject, userId.ToString()),
            new(ClaimTypeNames.Role, role),
        };
        if (tenantId is { } t)
        {
            claims.Add(new Claim(ClaimTypeNames.ActiveTenantId, t.ToString()));
        }
        foreach (var permission in permissions)
        {
            claims.Add(new Claim(ClaimTypeNames.Permission, permission));
        }

        var identity = new ClaimsIdentity(claims, "test", ClaimTypeNames.Subject, ClaimTypeNames.Role);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
        return controller;
    }

    private static CustomerRequest Request(Guid tenantId, CustomerRequestStatus status = CustomerRequestStatus.Draft)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Status = status,
            CompanyName = "Acme",
            LegalName = "Acme Inc",
            EmailAddress = "a@acme.com",
            Country = "US",
            AddressLine1 = "1 St",
            TaxNumber = "TAX-1",
            RegistrationNumber = "REG-1",
            BusinessUnit = "BU-1",
            Currency = "USD",
            PaymentTerms = "Net30",
        };

    // ---- List scoping ----

    [Fact]
    public async Task List_for_normal_user_does_not_pass_a_scope_tenant()
    {
        var tenantId = Guid.NewGuid();
        _requests.Setup(r => r.ListAsync(
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CustomerRequestStatus?>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<CustomerRequest>(), 0));

        var controller = CreateWithUser(Guid.NewGuid(), Roles.Operator, tenantId);
        await controller.List(tenantId: Guid.NewGuid(), null, null, null, null, null, 1, 20, default);

        // Non-super-admins are pinned by the ambient filter; the controller passes scopeTenant = null.
        _requests.Verify(r => r.ListAsync(
            null, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_for_super_admin_passes_requested_tenant_through()
    {
        var target = Guid.NewGuid();
        _requests.Setup(r => r.ListAsync(
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CustomerRequestStatus?>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<CustomerRequest>(), 0));

        var controller = CreateWithUser(Guid.NewGuid(), Roles.SuperAdmin);
        await controller.List(tenantId: target, null, null, null, null, null, 1, 20, default);

        _requests.Verify(r => r.ListAsync(
            null, target, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Create ----

    [Fact]
    public async Task Create_returns_201_and_stamps_active_tenant()
    {
        var tenantId = Guid.NewGuid();
        CustomerRequest? captured = null;
        _requests.Setup(r => r.AddAsync(It.IsAny<CustomerRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CustomerRequest, CancellationToken>((c, _) => captured = c);

        var controller = CreateWithUser(Guid.NewGuid(), Roles.Operator, tenantId);
        var result = await controller.Create(new CreateCustomerRequest
        {
            LegalName = "Acme Inc", CompanyName = "Acme", EmailAddress = "a@acme.com", Country = "US", AddressLine1 = "1 St",
        }, default);

        result.Should().BeOfType<CreatedAtActionResult>().Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(tenantId);
        captured.Status.Should().Be(CustomerRequestStatus.Draft);
    }

    [Fact]
    public async Task Super_admin_create_with_body_tenant_targets_that_tenant()
    {
        var activeTenant = Guid.NewGuid();
        var target = Guid.NewGuid();
        _tenants.Setup(t => t.GetByIdAsync(target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant { Id = target, Name = "T", Identifier = "t", Status = TenantStatus.Active });
        CustomerRequest? captured = null;
        _requests.Setup(r => r.AddAsync(It.IsAny<CustomerRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CustomerRequest, CancellationToken>((c, _) => captured = c);

        var controller = CreateWithUser(Guid.NewGuid(), Roles.SuperAdmin, activeTenant);
        var result = await controller.Create(new CreateCustomerRequest
        {
            TenantId = target,
            LegalName = "Acme Inc", CompanyName = "Acme", EmailAddress = "a@acme.com", Country = "US", AddressLine1 = "1 St",
        }, default);

        result.Should().BeOfType<CreatedAtActionResult>();
        captured!.TenantId.Should().Be(target);
        _tenants.Verify(t => t.GetByIdAsync(target, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Submit ----

    [Fact]
    public async Task Submit_with_unacknowledged_duplicates_returns_not_submitted_and_no_number()
    {
        var tenantId = Guid.NewGuid();
        var request = Request(tenantId, CustomerRequestStatus.Draft);
        _requests.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _duplicates.Setup(d => d.CheckStep1Async(tenantId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomerDuplicateMatch>
            {
                new(Guid.NewGuid(), "CUS-2026-000001", "Acme", new[] { "Company Name" }),
            });

        var controller = CreateWithUser(Guid.NewGuid(), Roles.Operator, tenantId);
        var result = await controller.Submit(request.Id, new SubmitCustomerRequest { DuplicateAcknowledged = false }, default);

        var data = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<SubmitCustomerResponse>>().Subject.Data!;
        data.Submitted.Should().BeFalse();
        data.Duplicates.Should().HaveCount(1);
        request.Status.Should().Be(CustomerRequestStatus.Draft); // unchanged
        request.CustomerRequestNumber.Should().BeNull();
    }

    [Fact]
    public async Task Submit_acknowledged_assigns_number_and_sets_submitted()
    {
        var tenantId = Guid.NewGuid();
        var request = Request(tenantId, CustomerRequestStatus.Draft);
        _requests.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _duplicates.Setup(d => d.CheckStep1Async(tenantId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CustomerDuplicateMatch>
            {
                new(Guid.NewGuid(), "CUS-2026-000001", "Acme", new[] { "Company Name" }),
            });
        _requests.Setup(r => r.CountForYearAsync(tenantId, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(41);

        var userId = Guid.NewGuid();
        var controller = CreateWithUser(userId, Roles.Operator, tenantId);
        var result = await controller.Submit(request.Id, new SubmitCustomerRequest { DuplicateAcknowledged = true }, default);

        var data = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<SubmitCustomerResponse>>().Subject.Data!;
        data.Submitted.Should().BeTrue();
        request.Status.Should().Be(CustomerRequestStatus.Submitted);
        request.CustomerRequestNumber.Should().Be($"CUS-{DateTime.UtcNow.Year}-000042");
        request.SubmittedById.Should().Be(userId);
    }

    [Fact]
    public async Task Submit_with_no_duplicates_assigns_number()
    {
        var tenantId = Guid.NewGuid();
        var request = Request(tenantId, CustomerRequestStatus.Draft);
        _requests.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _duplicates.Setup(d => d.CheckStep1Async(tenantId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CustomerDuplicateMatch>());
        _requests.Setup(r => r.CountForYearAsync(tenantId, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var controller = CreateWithUser(Guid.NewGuid(), Roles.Operator, tenantId);
        var result = await controller.Submit(request.Id, new SubmitCustomerRequest(), default);

        result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<SubmitCustomerResponse>>().Which.Data!.Submitted.Should().BeTrue();
        request.CustomerRequestNumber.Should().Be($"CUS-{DateTime.UtcNow.Year}-000001");
    }

    // ---- Detail masking ----

    [Fact]
    public async Task Detail_masks_step2_for_caller_without_approve_permission()
    {
        var tenantId = Guid.NewGuid();
        var request = Request(tenantId, CustomerRequestStatus.PendingApproval);
        _requests.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _audit.Setup(a => a.ListByCustomerAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CustomerAuditEntry>());

        // Operator has neither an explicit customers.approve claim nor it in its system-role set.
        var controller = CreateWithUser(Guid.NewGuid(), Roles.Operator, tenantId);
        var result = await controller.Get(request.Id, default);

        var detail = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<CustomerDetailResponse>>().Subject.Data!;
        detail.Step2.Should().BeNull();
        detail.Actions.CanViewStep2.Should().BeFalse();
    }

    [Fact]
    public async Task Detail_includes_step2_for_caller_with_approve_permission()
    {
        var tenantId = Guid.NewGuid();
        var request = Request(tenantId, CustomerRequestStatus.PendingApproval);
        _requests.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _audit.Setup(a => a.ListByCustomerAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CustomerAuditEntry>());
        _approval.Setup(a => a.GetMissingMandatoryStep2Fields(request)).Returns(Array.Empty<string>());

        var controller = CreateWithUser(Guid.NewGuid(), Roles.Operator, tenantId, Permissions.CustomersApprove);
        var result = await controller.Get(request.Id, default);

        var detail = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<ApiResponse<CustomerDetailResponse>>().Subject.Data!;
        detail.Step2.Should().NotBeNull();
        detail.Step2!.TaxNumber.Should().Be("TAX-1");
        detail.Actions.CanViewStep2.Should().BeTrue();
    }

    // ---- Workflow transitions (happy path) ----

    [Fact]
    public async Task Enrich_sets_under_review_status()
    {
        var tenantId = Guid.NewGuid();
        var request = Request(tenantId, CustomerRequestStatus.Submitted);
        _requests.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var controller = CreateWithUser(Guid.NewGuid(), Roles.TenantAdmin, tenantId, Permissions.CustomersReview);
        var result = await controller.Enrich(request.Id, new EnrichCustomerRequest { Territory = "EMEA" }, default);

        result.Should().BeOfType<OkObjectResult>();
        request.Status.Should().Be(CustomerRequestStatus.UnderReview);
        request.Territory.Should().Be("EMEA");
    }

    [Fact]
    public async Task SendForApproval_sets_pending_approval_status()
    {
        var tenantId = Guid.NewGuid();
        var request = Request(tenantId, CustomerRequestStatus.UnderReview);
        _requests.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var controller = CreateWithUser(Guid.NewGuid(), Roles.TenantAdmin, tenantId, Permissions.CustomersReview);
        var result = await controller.SendForApproval(request.Id, default);

        result.Should().BeOfType<OkObjectResult>();
        request.Status.Should().Be(CustomerRequestStatus.PendingApproval);
    }

    [Fact]
    public async Task Approve_workflow_exception_is_bad_request()
    {
        var tenantId = Guid.NewGuid();
        var request = Request(tenantId, CustomerRequestStatus.PendingApproval);
        _requests.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _duplicates.Setup(d => d.CheckTaxNumberAsync(tenantId, request.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CustomerDuplicateMatch>());
        _approval.Setup(a => a.ApproveAsync(request, It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CustomerWorkflowException("missing fields"));

        var controller = CreateWithUser(Guid.NewGuid(), Roles.TenantAdmin, tenantId, Permissions.CustomersApprove);
        var result = await controller.Approve(request.Id, new ApproveCustomerRequest { Step2 = new Step2Fields { TaxNumber = "TAX-1" } }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ---- Permission gating attributes ----

    [Theory]
    [InlineData(nameof(CustomersController.Enrich), Permissions.CustomersReview)]
    [InlineData(nameof(CustomersController.SendForApproval), Permissions.CustomersReview)]
    [InlineData(nameof(CustomersController.SaveStep2), Permissions.CustomersApprove)]
    [InlineData(nameof(CustomersController.Approve), Permissions.CustomersApprove)]
    [InlineData(nameof(CustomersController.Reject), Permissions.CustomersApprove)]
    [InlineData(nameof(CustomersController.Return), Permissions.CustomersApprove)]
    [InlineData(nameof(CustomersController.RetrySync), Permissions.CustomersApprove)]
    [InlineData(nameof(CustomersController.Reopen), Permissions.CustomersApprove)]
    public void Workflow_endpoints_require_expected_permission(string methodName, string expectedPermission)
    {
        var method = typeof(CustomersController).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull($"{methodName} should exist");

        var attribute = method!.GetCustomAttribute<RequirePermissionAttribute>();
        attribute.Should().NotBeNull($"{methodName} should be gated by [RequirePermission]");
        attribute!.Policy.Should().EndWith(expectedPermission);
    }
}
