namespace EmsPortal.Api.Models.Rems;

/// <summary>
/// The option-set KEYS the REMS feature stores references to, in one place.
///
/// <para>
/// Every one of these is a list whose chosen item is recorded on a REMS row as a foreign key to
/// <c>OptionSetItem.Id</c>. The key is how the tenant's own copy of the list is found (see
/// <c>GetEffectiveSetAsync</c>), so it is what a write resolves a CODE against on the way in.
/// </para>
/// <para>
/// Three of them read one way on screen and another here — the key keeps the older name because every
/// tenant's copy of the list is filed under it: <c>IndustryGroup</c> is "Entity Type",
/// <c>SubIndustry</c> is "Industry", <c>SubServiceLine</c> is "Service Line".
/// </para>
/// </summary>
public static class RemsOptionSetKeys
{
    public const string Type = "REMS.Type";
    public const string Status = "REMS.Status";
    public const string ReferralSource = "REMS.ReferralSource";
    public const string IndustryGroup = "REMS.IndustryGroup";
    public const string Department = "REMS.Department";
    public const string SubServiceLine = "REMS.SubServiceLine";
    public const string SubIndustry = "REMS.SubIndustry";
    public const string BillingPeriod = "REMS.BillingPeriod";
    public const string PersonnelLevel = "REMS.PersonnelLevel";

    /// <summary>
    /// How far a client's RELATED client has got — the status on every row of the Related Entities list.
    /// Set by hand; the server writes only the <c>not_initiated</c> default. See
    /// <c>RemsRelatedEntityStatuses</c>.
    /// </summary>
    public const string RelatedEntityStatus = "REMS.RelatedEntityStatus";

    /// <summary>Referenced by item ID already — the grouped marketing list and the tax-form checklist.</summary>
    public const string MarketingMethods = "REMSMarketing_MarketingMethods.MarketingMethodId";
    public const string TaxForm = "REMS.TaxForm";
}
