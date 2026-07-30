namespace EmsPortal.Domain.Entities;

/// <summary>
/// The well-known <see cref="REMS.Status"/> option-set codes used by the request lifecycle (WO-111).
/// <see cref="REMS.Status"/> stores the option item's string VALUE, so these mirror the seeded
/// <c>REMS.Status</c> option list (see <c>DefaultOptionSets</c>). Only the codes the backend branches on
/// are named here; the remaining downstream codes (sent, awaiting_customer, …) are set by later WOs.
/// </summary>
public static class RemsRequestStatuses
{
    /// <summary>A saved, not-yet-submitted request. Visible only to its creator (AC-REMS-004.11).</summary>
    public const string Draft = "draft";

    /// <summary>Submitted to the Admin Pool. The Admin Pool is every request with a status other than <see cref="Draft"/>.</summary>
    public const string Submitted = "submitted";

    /// <summary>The customer completed and submitted their EMS onboarding form (set on public submit, REMS WO-113).</summary>
    public const string CustomerSubmitted = "customer_submitted";
}
