using System.Security.Claims;
using EmsPortal.Application.Abstractions.Persistence;
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
    /// around in an emergency. The ordinary remedy is for the holding admin to hand the request back, after
    /// which any admin may pick it up again.
    /// </summary>
    public static bool IsElevated(ClaimsPrincipal user)
        => user.IsSuperAdmin() || user.GetRoles().Any(r => string.Equals(r, Roles.TenantAdmin, StringComparison.Ordinal));

    /// <summary>
    /// A REMS Admin (or a Super Admin): the operational role that runs the firm's pipeline. Distinct from
    /// <see cref="IsElevated"/>, which is the platform's own administrators — this one is a job, not a
    /// power, and it buys exactly two things: seeing every request in the tenant
    /// (<c>RemsRepository.ApplyVisibility</c>) and finishing a DRAFT somebody else left behind
    /// (<see cref="CanWork"/>). It does NOT let one admin work a request another has picked up.
    /// </summary>
    public static bool IsRemsAdmin(ClaimsPrincipal user)
        => user.IsSuperAdmin() || user.GetRoles().Any(r => string.Equals(r, Roles.Admin, StringComparison.Ordinal));

    /// <summary>
    /// Whose request this is: the person who raised it, or the principal they raised it for. A delegate
    /// preparing a request for a shareholder produces the shareholder's work, so both hold it.
    /// </summary>
    public static bool IsInitiator(REMS rems, Guid me)
        => rems.CreatedById == me || rems.OnBehalfOfUserId == me;

    /// <summary>
    /// WHOSE work this request is, for the purpose of asking what they have delegated: the principal it
    /// was raised for where a delegate raised it, and the person who raised it otherwise. One of the two
    /// people <see cref="IsInitiator"/> accepts — the one whose delegations decide anything.
    /// </summary>
    public static Guid? PrincipalOf(REMS rems) => rems.OnBehalfOfUserId ?? rems.CreatedById;

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
    /// has answered it is the Admin who picked the request up, and only them — which is why a request
    /// nobody has picked up is nobody's to work until somebody does.
    /// <para>
    /// Two exceptions to "whoever it is with", and both are a REMS Admin's:
    /// </para>
    /// <para>
    /// A REMS Admin may work any DRAFT, whoever raised it. A draft has not been sent to anybody, so there
    /// is no handover to cut across — and an admin who can now SEE a colleague's unfinished referral needs
    /// to be able to finish and send it, which is the whole point of their seeing it.
    /// </para>
    /// <para>
    /// A REMS Admin may also work a request in either REWORK state — returned by the admin, or sent back
    /// after the approvers declined it. A send-back asks the initiator for the changes; it does not take
    /// the request off the admin's desk, and it is the admin who routes it onward once it comes back. The
    /// old rule read the rework states as plain initiator stages and locked the admin out of a request
    /// they had been reviewing minutes earlier, over a correction they were the ones who asked for.
    /// </para>
    /// <para>
    /// Neither exception reaches the ADMIN stages: once the client has answered, only the admin holding the
    /// request may work it, and no admin can take one another admin is holding.
    /// </para>
    /// <para>
    /// One narrowing sits on the CSE. Before the client answers they work the request alongside the
    /// initiator, as they always have. Once it has been RETURNED for rework it is the initiator's work that
    /// is outstanding, and the CSE may only take it on when the initiator has cover arranged — at least
    /// one REMS delegation in force, which <paramref name="initiatorHasCover"/> carries. An initiator who
    /// has delegated nothing gets their own rework back, which is also why the send-back dialog stops
    /// offering the CSE as a target for them.
    /// </para>
    /// <para>
    /// Says nothing about the engagement being locked for approval — that is the engagement's own status,
    /// checked separately where it applies.
    /// </para>
    /// </summary>
    /// <param name="initiatorHasCover">
    /// Whether <see cref="PrincipalOf"/> has any REMS delegation in force today
    /// (<c>IRemsDelegationRepository.HasActiveDelegateAsync</c>). Passed in rather than looked up here so
    /// this stays a pure rule; a caller with no CSE in play may pass false without querying.
    /// </param>
    public static bool CanWork(ClaimsPrincipal user, REMS rems, Guid me, bool initiatorHasCover)
    {
        if (IsElevated(user))
        {
            return true;
        }

        if (RemsRequestStatuses.IsWithInitiator(rems.Status!.Value))
        {
            return IsInitiator(rems, me)
                || (rems.CSEId == me && (initiatorHasCover || !RemsRequestStatuses.IsRework(rems.Status!.Value)))
                || (IsRemsAdmin(user) && (rems.Status!.Value == RemsRequestStatuses.Draft || RemsRequestStatuses.IsRework(rems.Status!.Value)));
        }

        return rems.AdminAssignedToId is { } admin && admin == me;
    }

    /// <summary>
    /// Whether this request's initiator has any REMS delegation in force today — "have they arranged
    /// cover?". The one fact both the send-back target rule and <see cref="CanWork"/> turn on, defined
    /// once: a second spelling of "in force today" would be a second answer.
    /// </summary>
    public static async Task<bool> InitiatorHasCoverAsync(
        IRemsDelegationRepository delegations, REMS rems, CancellationToken cancellationToken)
        => PrincipalOf(rems) is { } principal
            && await delegations.HasActiveDelegateAsync(
                principal, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);

    /// <summary>
    /// The cover flag for a <see cref="CanWork"/> call, skipping the lookup wherever it cannot change the
    /// answer. It only ever can for the CSE on a request in rework — a small fraction of the calls that
    /// guard engagement writes, which run on every field save.
    /// </summary>
    public static Task<bool> CoverForWorkAsync(
        IRemsDelegationRepository delegations, REMS rems, Guid me, CancellationToken cancellationToken)
        => rems.CSEId == me && RemsRequestStatuses.IsRework(rems.Status!.Value)
            ? InitiatorHasCoverAsync(delegations, rems, cancellationToken)
            : Task.FromResult(false);

    /// <summary>The refusal that goes with a failed <see cref="CanWork"/>, worded for the stage it failed at.</summary>
    public static string WorkDeniedReason(REMS rems)
        => RemsRequestStatuses.IsRework(rems.Status!.Value)
            ? "This request has been returned to the person who raised it. Only they, or a REMS Admin, can work its engagement setup — the CSE can take it on only where the initiator has named a REMS delegate."
            : RemsRequestStatuses.IsWithInitiator(rems.Status!.Value)
            ? "This request is with the person who raised it; only they (or the CSE named on it), or a REMS Admin, can work its engagement setup."
            : rems.AdminAssignedToId is null
                ? "This request is waiting for pickup. Pick it up from EMS Review to work its engagement setup."
                : "This request is being reviewed by another admin; only they can work its engagement setup.";
}
