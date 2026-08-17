namespace EmsPortal.Domain.Entities;

/// <summary>
/// One return of a request from the Admin to its initiator for Engagement Setup rework, with the reason
/// the admin gave.
/// <para>
/// Its own rows rather than a column on the request, because the loop repeats: the admin can return the
/// same request as many times as the setup still needs work, and each pass has its own reason worth
/// keeping. The audit columns on <see cref="AuditableEntity"/> already carry who returned it and when, so
/// only the reason and the resolution are stored here.
/// </para>
/// <para>
/// This covers the ADMIN's send-back only. A round that fails on approver declines is recorded on the
/// round and its tasks — each decliner's own reason sits on their task — so it needs nothing here.
/// </para>
/// </summary>
public class REMSSendBack : AuditableEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Owning tenant (tenant-scoped).</summary>
    public Guid TenantId { get; set; }

    /// <summary>The request that was returned.</summary>
    public Guid REMSId { get; set; }

    /// <summary>Why the admin sent it back. Required — a return without a reason is not actionable.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Who the admin addressed the rework to — the initiator or the CSE named on the request. Both can
    /// already WORK a returned request (see <c>RemsSetupAccess.CanWork</c>), so this does not grant access:
    /// it records whose job the admin decided it was, which is what the returned request's banner says and
    /// what the notification is worded around. Null on returns made before the admin was asked to choose.
    /// </summary>
    public Guid? ReturnedToUserId { get; set; }

    /// <summary>
    /// When the initiator sent the revised setup back to the admin, or null while it is still with them.
    /// At most one unresolved row per request.
    /// </summary>
    public DateTime? ResolvedOnUtc { get; set; }

    // ---- Navigations ----
    public REMS? Rems { get; set; }
}
