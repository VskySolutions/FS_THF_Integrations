using EmsPortal.Domain.Entities;

namespace EmsPortal.Api.Models.Rems;

/// <summary>
/// Projects the loaded REMS engagement-workspace graph (WO-114) into its read models. Pure mapping: the
/// caller loads the client/entities/engagements/details and resolves the referenced user ids to names.
/// </summary>
internal static class RemsWorkspaceMapper
{
    public static RemsUserRef? UserRef(Guid? id, IReadOnlyDictionary<Guid, string> names)
        => id is { } uid ? new RemsUserRef(uid, names.TryGetValue(uid, out var n) ? n : string.Empty) : null;

    public static RemsAddressView? Address(Address? address)
        => address is null
            ? null
            : new RemsAddressView(
                address.Id, address.AddressLine1, address.AddressLine2, address.CityName, address.StateName,
                address.StateCode, address.PostalCode, address.CountryCode, address.CountryName);

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

        var auditView = audit is null ? null : new RemsAuditDetailView(audit.Id, audit.ClientAcceptanceFormMediaId);
        var govView = government is null
            ? null
            : new RemsGovernmentDetailView(
                government.Id, government.ContractNumber, government.FloridaOnePercentStateFeeApplies,
                government.ContractStartDate, government.ContractEndDate, government.OriginalTerm, government.RenewalTerms,
                government.PurchaseOrderStartDate, government.PurchaseOrderEndDate);
        var taxView = tax is null
            ? null
            : new RemsTaxDetailView(
                tax.Id, tax.FiscalYearEnd, tax.CalculatedDueDates,
                tax.TaxForms.Where(f => !f.Deleted).Select(f => f.TaxFormId).ToList());

        return new RemsEngagementView(
            engagement.Id,
            engagement.Department,
            engagement.ServiceLine,
            UserRef(engagement.DepartmentDirectorId, names),
            UserRef(engagement.EngagementExecutiveId, names),
            UserRef(engagement.BillingManagerId, names),
            engagement.FirstYearFeeEstimate,
            engagement.RealizationPercentage,
            engagement.Status.ToString(),
            marketing,
            commission,
            auditView,
            govView,
            taxView);
    }

    public static RemsEngagementWorkspace Workspace(
        REMS rems,
        REMSClient client,
        IReadOnlyDictionary<Guid, REMSEngagement> engagementsByEntity,
        IReadOnlyDictionary<Guid, REMSEngagementAuditDetail> auditByEngagement,
        IReadOnlyDictionary<Guid, REMSEngagementGovernmentDetail> governmentByEngagement,
        IReadOnlyDictionary<Guid, REMSEngagementTaxDetail> taxByEngagement,
        IReadOnlyDictionary<Guid, string> names,
        IReadOnlyList<RemsDepartmentDirectorView> departmentDirectors)
    {
        var clientView = new RemsClientView(
            client.Id, client.Name, client.Email, client.MobileNumber, client.ReferralSource,
            client.BillingContactName, client.BillingEmail, Address(client.BillingAddress));

        var entities = client.Entities
            .Where(e => !e.Deleted)
            .OrderByDescending(e => e.IsMainEntity)
            .ThenBy(e => e.Name)
            .Select(e =>
            {
                engagementsByEntity.TryGetValue(e.Id, out var engagement);
                RemsEngagementView? engagementView = null;
                if (engagement is not null)
                {
                    auditByEngagement.TryGetValue(engagement.Id, out var audit);
                    governmentByEngagement.TryGetValue(engagement.Id, out var gov);
                    taxByEngagement.TryGetValue(engagement.Id, out var tax);
                    engagementView = Engagement(engagement, audit, gov, tax, names);
                }

                var addresses = e.Addresses
                    .Where(a => !a.Deleted)
                    .Select(a => new RemsEntityAddressView(a.Id, a.AddressType.ToString(), Address(a.Address)!))
                    .ToList();

                var contacts = e.Contacts
                    .Where(c => !c.Deleted)
                    .Select(c => new RemsEntityContactView(
                        c.Id, c.ContactRole, c.IsRequired,
                        c.Person?.DisplayName, c.Person?.PrimaryEmail, c.Person?.MobileNumber))
                    .ToList();

                return new RemsEntityView(e.Id, e.Name, e.EIN, e.IsMainEntity, addresses, contacts, engagementView);
            })
            .ToList();

        return new RemsEngagementWorkspace(
            rems.Id, rems.REMSNumber, rems.Status, clientView, entities, departmentDirectors);
    }
}
