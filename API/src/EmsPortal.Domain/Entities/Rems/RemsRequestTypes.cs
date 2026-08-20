namespace EmsPortal.Domain.Entities;

/// <summary>
/// The <see cref="REMS.Type"/> option-set codes — how a referral relates to THF's records (WO-111).
/// <see cref="REMS.Type"/> stores the option item's string VALUE, so these mirror the seeded
/// <c>REMS.Type</c> option list (see <c>DefaultOptionSets</c>).
/// <para>
/// The LABELS are the tenant's to rename in Administration → Option Sets; these codes are not. Anything
/// that branches on the answer branches on the code, which is why they are named here rather than
/// spelled out at each call site.
/// </para>
/// </summary>
public static class RemsRequestTypes
{
    /// <summary>A client THF has no record of.</summary>
    public const string BrandNewClient = "brand_new_client";

    /// <summary>
    /// A new engagement for a client THF already has, seeded as "New Engagement, Existing Client". It was
    /// two values — "New Engagement" and "Existing Client" — until they were merged, since every new
    /// engagement for an existing client is both; this code is the one that survived the merge (see
    /// <c>MergeRemsExistingClientTypes</c>).
    /// <para>
    /// It absorbed a third answer afterwards, "Subsidiary / Child of Existing Client"
    /// (<c>RetireRemsSubsidiaryType</c>). A child of a client on file is an engagement for a client we
    /// already have, and what actually made a request a subsidiary was the PARENT CLIENT named on it —
    /// which is now asked on this answer. Naming a parent says the referral is a child; a separate code
    /// only asked the partner to say so twice.
    /// </para>
    /// </summary>
    public const string ExistingClient = "existing_client";

    /// <summary>The codes that mean an existing client is referenced.</summary>
    public static readonly IReadOnlyList<string> ExistingClientTypes = new[]
    {
        ExistingClient,
    };

    /// <summary>Every offered code, in display order.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        BrandNewClient, ExistingClient,
    };
}
