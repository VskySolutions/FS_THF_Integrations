using EmsPortal.Application.Abstractions.Persistence;
using EmsPortal.Domain.Entities;
using EmsPortal.Domain.Enums;

namespace EmsPortal.Api.Models.Rems;

/// <summary>
/// Projects the loaded REMS engagement-workspace graph (WO-114) into its read models. Pure mapping: the
/// caller loads the client/entities/engagements/details and resolves the referenced user ids to names.
/// </summary>
internal static class RemsWorkspaceMapper
{
    public static RemsUserRef? UserRef(Guid? id, IReadOnlyDictionary<Guid, string> names)
        => id is { } uid ? new RemsUserRef(uid, names.TryGetValue(uid, out var n) ? n : string.Empty) : null;

    /// <summary>
    /// The request's EMS-form state as the UI reads it: the form's own status, and whether the client has
    /// submitted / is still being waited on. Shared so the request detail and the approver's review packet
    /// can never disagree about what a given form state means.
    /// </summary>
    public static (string EmsFormState, string? ClientSubmissionState) FormState(RemsFormStateInfo? form)
    {
        if (form is null)
        {
            return ("NotStarted", null);
        }

        // Draft and Saved both mean the same thing to a reader: nothing has gone to the client yet, so
        // both report as Not started and this column changes only once the form is actually Sent.
        //
        // "Saved" is an artefact of how the page WRITES, not a step in the workflow. The request page
        // saves itself, so the form row is minted the instant a CSE and an entity type are both chosen --
        // the column flipped to "Saved" while the admin was still filling the tab in. Before the page
        // auto-saved, building the form was a deliberate step and this stayed on Not started until it
        // happened. The state being reported is the CLIENT form's, and the client has not been written
        // to until it is sent.
        var ems = form.FormStatus switch
        {
            null or RemsFormStatus.Draft or RemsFormStatus.Saved => "NotStarted",
            var status => status.ToString(),
        };
        var submission = form.HasSubmission || form.FormSubmittedOnUtc is not null
            ? "Submitted"
            : form.FormSentOnUtc is not null ? "AwaitingCustomer" : null;
        return (ems, submission);
    }

    public static RemsAddressView? Address(Address? address)
        => address is null
            ? null
            : new RemsAddressView(
                address.Id, address.AddressLine1, address.AddressLine2, address.CityName, address.StateName,
                address.StateCode, address.PostalCode, address.CountryCode, address.CountryName,
                // Who the post is addressed to, carried on the address itself. Null on a physical or
                // mailing row; filled on a billing one, where the intake form asks for both halves.
                address.Suffix, address.FirstName, address.LastName, address.Email, address.PhoneNumber);

    public static RemsEngagementView Engagement(
        REMSEngagement engagement,
        REMSEngagementAuditDetail? audit,
        REMSEngagementGovernmentDetail? government,
        REMSEngagementTaxDetail? tax,
        IReadOnlyDictionary<Guid, string> names)
    {
        var marketing = engagement.MarketingMethods.Where(m => !m.Deleted).Select(m => m.MarketingMethodId).ToList();
        var commission = engagement.CommissionSplits
            .Where(s => !s.Deleted)
            .Select(s => new RemsCommissionSplitView(s.Id, UserRef(s.EmployeeId, names)!, s.CommissionPercentage))
            .ToList();

        var auditView = audit is null
            ? null
            : new RemsAuditDetailView(
                audit.Id, audit.ClientAcceptanceFormMediaId, audit.ClientAcceptanceFormMedia?.OriginalFileName,
                audit.ClientFiscalYearEnd, audit.AdminFeesApply, audit.AdminFeesAmount);
        var govView = government is null
            ? null
            : new RemsGovernmentDetailView(
                government.Id, government.ContractNumber, government.FloridaOnePercentStateFeeApplies,
                government.ContractStartDate, government.ContractEndDate, government.OriginalTerm, government.RenewalTerms,
                government.PurchaseOrderStartDate, government.PurchaseOrderEndDate,
                government.PurchaseOrderNumber, government.PurchaseOrderAmount,
                government.PurchaseOrderMediaId, government.PurchaseOrderMedia?.OriginalFileName,
                government.PersonnelLevel?.Value, government.BillRatePerHour);
        var taxView = tax is null
            ? null
            : new RemsTaxDetailView(
                tax.Id, tax.FiscalYearEnd, tax.OriginalDueDate, tax.FirstExtensionDueDate, tax.CalculatedDueDates,
                tax.TaxForms.Where(f => !f.Deleted).Select(f => f.TaxFormId).ToList());

        return new RemsEngagementView(
            engagement.Id,
            engagement.Department?.Value,
            engagement.SubServiceLine?.Value,
            engagement.SubIndustry?.Value,
            UserRef(engagement.DepartmentDirectorId, names),
            UserRef(engagement.EngagementExecutiveId, names),
            UserRef(engagement.BillingManagerId, names),
            engagement.FirstYearFeeEstimate,
            engagement.EngagementFee,
            engagement.RealizationPercentage,
            engagement.BillingPeriod?.Value,
            engagement.BillingProcessDescription,
            engagement.Status.ToString(),
            marketing,
            commission,
            auditView,
            govView,
            taxView);
    }

    /// <summary>
    /// The workspace for a request: its client, that client's entities, and the ONE engagement the request
    /// carries. The entities are listed because the client's business details belong to them; the
    /// engagement belongs to the REQUEST and is reported once alongside them.
    /// <para>
    /// <paramref name="client"/> is null until the intake form comes back. The engagement is filled before
    /// that, so the workspace has to describe a request that has one and no client yet.
    /// </para>
    /// </summary>
    public static RemsEngagementWorkspace Workspace(
        REMS rems,
        REMSClient? client,
        REMSEngagement? engagement,
        IReadOnlyDictionary<Guid, REMSEngagementAuditDetail> auditByEngagement,
        IReadOnlyDictionary<Guid, REMSEngagementGovernmentDetail> governmentByEngagement,
        IReadOnlyDictionary<Guid, REMSEngagementTaxDetail> taxByEngagement,
        IReadOnlyDictionary<Guid, string> names,
        string? industryGroup,
        IReadOnlyList<RemsAdditionalEntityView> additionalEntities,
        IReadOnlyList<RemsDepartmentDirectorView> departmentDirectors)
    {
        // Billing addresses are the main entity's, so they ride along with that entity's addresses below
        // rather than being read off the client. There may be more than one.
        var clientView = client is null
            ? null
            : new RemsClientView(
                client.Id, client.Name, client.Email, client.MobileNumber,
                // The view carries the CODE, read off the referenced option item. The wire keeps codes even
                // though the column is now a foreign key: every screen branches on them, and none of them
                // has any use for an item id.
                client.ReferralSource?.Value,
                client.BillingContactName, client.BillingEmail);

        RemsEngagementView? engagementView = null;
        if (engagement is not null)
        {
            auditByEngagement.TryGetValue(engagement.Id, out var audit);
            governmentByEngagement.TryGetValue(engagement.Id, out var gov);
            taxByEngagement.TryGetValue(engagement.Id, out var tax);
            engagementView = Engagement(engagement, audit, gov, tax, names);
        }

        var entities = (client?.Entities ?? Enumerable.Empty<REMSEntity>())
            .Where(e => !e.Deleted)
            .OrderByDescending(e => e.IsMainEntity)
            .ThenBy(e => e.Name)
            .Select(e =>
            {
                var addresses = e.Addresses
                    .Where(a => !a.Deleted)
                    .Select(a => new RemsEntityAddressView(a.Id, a.AddressType.ToString(), Address(a.Address)!))
                    .ToList();

                var contacts = e.Contacts
                    .Where(c => !c.Deleted)
                    .Select(c => new RemsEntityContactView(
                        c.Id, c.ContactRole, c.IsRequired,
                        c.Person?.DisplayName, c.Person?.PrimaryEmail, c.Person?.MobileNumber, c.Person?.Suffix))
                    .ToList();

                return new RemsEntityView(e.Id, e.Name, e.EIN, e.IsMainEntity, addresses, contacts);
            })
            .ToList();

        return new RemsEngagementWorkspace(
            rems.Id, rems.REMSNumber, rems.Status!.Value, clientView, entities, engagementView,
            industryGroup, additionalEntities, departmentDirectors);
    }
}
