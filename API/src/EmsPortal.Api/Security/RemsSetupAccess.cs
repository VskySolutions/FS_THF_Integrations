using System.Security.Claims;
using EmsPortal.Domain.Entities;
using EmsPortal.Shared.Security;

namespace EmsPortal.Api.Security;

/// <summary>
/// Who may read and who may WORK a request's engagement setup — the CSE/industry group on its form and
/// every field of the engagement itself.
/// <para>
/// This is a record-level rule, not a permission one, because the setup changes hands during the
/// lifecycle: the initiator fills it before the client is ever contacted, the reviewing Admin takes it
/// over once the client has answered, and a send-back hands it straight back. A permission cannot express
/// "whoever it is with right now", and gating on <c>rems.engagements.manage</c> alone would both lock the
/// initiator out of their own request and let any Admin work one another Admin had claimed.
/// </para>
/// <para>
/// Enforced on the server rather than by hiding fields: the form is a URL, reachable from either list or
/// a pasted link.
/// </para>
/// </summary>
internal static class RemsSetupAccess
{
    /// <summary>
    /// A Super Admin or Tenant Admin, who are exempt from the stage rules so an assignment can be worked
    /// around in an emergency. The ordinary remedy is to re-point the reviewing admin, which both lists
    /// already offer.
    /// </summary>
    public static bool IsElevated(ClaimsPrincipal user)
        => user.IsSuperAdmin() || user.GetRoles().Any(r => string.Equals(r, Roles.TenantAdmin, StringComparison.Ordinal));

    /// <summary>
    /// Whose request this is: the person who raised it, or the principal they raised it for. A delegate
    /// preparing a request for a shareholder produces the shareholder's work, so both hold it.
    /// </summary>
    public static bool IsInitiator(REMS rems, Guid me)
        => rems.CreatedById == me || rems.OnBehalfOfUserId == me;

    /// <summary>Everyone named on the request: its initiator, the CSE on it, and the admin reviewing it.</summary>
    public static bool IsParticipant(REMS rems, Guid me)
        => IsInitiator(rems, me) || rems.CSEId == me || rems.AdminAssignedToId == me;

    /// <summary>
    /// May READ the setup. Everyone named on the request may, whatever stage it is in — the initiator does
    /// not stop being able to see their own request the moment the Admin picks the review up — and so may
    /// any REMS Admin, whose EMS Review queue lists these requests whether or not each one is theirs to
    /// work. Reading is not working: writing is <see cref="CanWork"/>, and it is far narrower.
    /// </summary>
    public static bool CanRead(ClaimsPrincipal user, REMS rems, Guid me)
        => IsElevated(user)
            || IsParticipant(rems, me)
            || user.HasPermission(Permissions.RemsEngagementsManage);

    /// <summary>
    /// May WRITE the setup: whoever the request is with at this stage. Before the client has answered (and
    /// during either rework state) that is the initiator and the CSE working it with them; once the client
    /// has answered it is the Admin named on the request, and only them.
    /// <para>
    /// Says nothing about the engagement being locked for approval — that is the engagement's own status,
    /// checked separately where it applies.
    /// </para>
    /// </summary>
    public static bool CanWork(ClaimsPrincipal user, REMS rems, Guid me)
    {
        if (IsElevated(user))
        {
            return true;
        }

        if (RemsRequestStatuses.IsWithInitiator(rems.Status))
        {
            return IsInitiator(rems, me) || rems.CSEId == me;
        }

        return rems.AdminAssignedToId is { } admin && admin == me;
    }

    /// <summary>The refusal that goes with a failed <see cref="CanWork"/>, worded for the stage it failed at.</summary>
    public static string WorkDeniedReason(REMS rems)
        => RemsRequestStatuses.IsWithInitiator(rems.Status)
            ? "This request is with the person who raised it; only they (or the CSE named on it) can work its engagement setup."
            : rems.AdminAssignedToId is null
                ? "This request has no reviewing admin. Name one before working its engagement setup."
                : "This request is being reviewed by another admin; only they can work its engagement setup.";
}
