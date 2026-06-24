using FluentAssertions;
using IntegrationHub.Application.Abstractions.Customers;
using IntegrationHub.Application.Abstractions.Persistence;
using IntegrationHub.Application.Abstractions.UniversalFeatures;
using IntegrationHub.Application.Customers;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using Moq;

namespace IntegrationHub.UnitTests;

// WO-66: Customer Management approval state machine.
public class CustomerApprovalServiceTests
{
    private readonly Mock<ICustomerAuditRepository> _audit = new();
    private readonly Mock<ICustomerSyncDispatcher> _dispatcher = new();
    private readonly Mock<IActivityEventWriter> _activity = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private CustomerApprovalService Create() => new(_audit.Object, _dispatcher.Object, _activity.Object, _unitOfWork.Object);

    /// <summary>A request awaiting approval with all mandatory Step 2 fields completed.</summary>
    private static CustomerRequest CompleteRequest(int requiredStages = 1, int currentStage = 0)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = CustomerRequestStatus.PendingApproval,
            CompanyName = "Acme",
            LegalName = "Acme Inc",
            EmailAddress = "a@acme.com",
            Address = new Address { CountryName = "US", AddressLine1 = "1 St" },
            TaxNumber = "TAX-1",
            RegistrationNumber = "REG-1",
            BusinessUnit = "BU-1",
            Currency = "USD",
            PaymentTerms = "Net30",
            RequiredApprovalStages = requiredStages,
            CurrentApprovalStage = currentStage,
        };

    [Fact]
    public void GetMissingMandatoryStep2Fields_returns_all_missing_when_blank()
    {
        var request = new CustomerRequest();

        var missing = Create().GetMissingMandatoryStep2Fields(request);

        missing.Should().BeEquivalentTo(
            new[] { "Tax Number", "Registration Number", "Business Unit", "Currency", "Payment Terms" });
    }

    [Fact]
    public void GetMissingMandatoryStep2Fields_returns_empty_when_all_present()
    {
        var missing = Create().GetMissingMandatoryStep2Fields(CompleteRequest());

        missing.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveAsync_with_incomplete_mandatory_step2_throws_and_does_not_enqueue()
    {
        var request = CompleteRequest();
        request.Currency = null; // make Step 2 incomplete

        var act = () => Create().ApproveAsync(request, Guid.NewGuid(), "Approver", default);

        await act.Should().ThrowAsync<CustomerWorkflowException>();
        _dispatcher.Verify(d => d.Enqueue(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAsync_when_not_awaiting_approval_throws()
    {
        var request = CompleteRequest();
        request.Status = CustomerRequestStatus.Draft;

        var act = () => Create().ApproveAsync(request, Guid.NewGuid(), "Approver", default);

        await act.Should().ThrowAsync<CustomerWorkflowException>();
        _dispatcher.Verify(d => d.Enqueue(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAsync_on_final_stage_sets_sync_in_progress_and_enqueues_once()
    {
        var request = CompleteRequest(requiredStages: 1);
        var actorId = Guid.NewGuid();

        await Create().ApproveAsync(request, actorId, "Approver", default);

        request.Status.Should().Be(CustomerRequestStatus.SyncInProgress);
        request.ApprovedById.Should().Be(actorId);
        request.ApprovedOnUtc.Should().NotBeNull();
        request.CurrentApprovalStage.Should().Be(1);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Enqueue(request.Id, request.TenantId), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_multi_stage_first_approval_is_partial_and_does_not_enqueue()
    {
        var request = CompleteRequest(requiredStages: 2);

        await Create().ApproveAsync(request, Guid.NewGuid(), "Approver", default);

        request.Status.Should().Be(CustomerRequestStatus.PartiallyApproved);
        request.CurrentApprovalStage.Should().Be(1);
        request.ApprovedById.Should().BeNull();
        _dispatcher.Verify(d => d.Enqueue(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ApproveAsync_multi_stage_second_approval_finalises_and_enqueues()
    {
        var request = CompleteRequest(requiredStages: 2);
        var service = Create();

        await service.ApproveAsync(request, Guid.NewGuid(), "Approver1", default);
        // Step 2 data persists across stages (the request object carries it through).
        request.TaxNumber.Should().Be("TAX-1");
        request.Status.Should().Be(CustomerRequestStatus.PartiallyApproved);

        await service.ApproveAsync(request, Guid.NewGuid(), "Approver2", default);

        request.CurrentApprovalStage.Should().Be(2);
        request.Status.Should().Be(CustomerRequestStatus.SyncInProgress);
        _dispatcher.Verify(d => d.Enqueue(request.Id, request.TenantId), Times.Once);
    }

    [Fact]
    public async Task RejectAsync_with_empty_reason_throws()
    {
        var request = CompleteRequest();

        var act = () => Create().RejectAsync(request, "   ", Guid.NewGuid(), "Approver", default);

        await act.Should().ThrowAsync<CustomerWorkflowException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RejectAsync_with_reason_sets_rejected_and_records_reason()
    {
        var request = CompleteRequest();

        await Create().RejectAsync(request, "  Incomplete docs  ", Guid.NewGuid(), "Approver", default);

        request.Status.Should().Be(CustomerRequestStatus.Rejected);
        request.RejectionReason.Should().Be("Incomplete docs");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Enqueue(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ReturnAsync_with_empty_notes_throws()
    {
        var request = CompleteRequest();

        var act = () => Create().ReturnAsync(request, " ", Array.Empty<string>(), Guid.NewGuid(), "Approver", default);

        await act.Should().ThrowAsync<CustomerWorkflowException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReturnAsync_with_notes_sets_returned_serialises_fields_and_resets_stage()
    {
        var request = CompleteRequest(requiredStages: 2, currentStage: 1);
        var fields = new[] { "CompanyName", "EmailAddress" };

        await Create().ReturnAsync(request, "  Please fix the address.  ", fields, Guid.NewGuid(), "Approver", default);

        request.Status.Should().Be(CustomerRequestStatus.Returned);
        request.ReturnNotes.Should().Be("Please fix the address.");
        request.CurrentApprovalStage.Should().Be(0);
        request.UnlockedFields.Should().NotBeNull();
        System.Text.Json.JsonSerializer.Deserialize<List<string>>(request.UnlockedFields!)
            .Should().BeEquivalentTo(fields);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Enqueue(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }
}
