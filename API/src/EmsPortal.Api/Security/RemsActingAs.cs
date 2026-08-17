using EmsPortal.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace EmsPortal.Api.Security;

/// <summary>
/// Resolves the "acting as" claim a delegate sends with a request, and decides whether they may.
/// <para>
/// The delegate chooses whose hat they are wearing and says so per request, via the
/// <c>X-Rems-On-Behalf-Of</c> header, rather than the server inferring it from whoever happens to have
/// delegated to them. That is Concur's model and it is the safer one: someone holding several delegations
/// would otherwise have their actions attributed by guesswork, and every action would carry an ambiguity
/// no audit trail could later resolve.
/// </para>
/// <para>
/// The header is a CLAIM, never a grant. Nothing trusts it until it has been checked against a live
/// delegation here, so sending someone else's id gets you exactly the access you already had.
/// </para>
/// </summary>
public static class RemsActingAs
{
    public const string HeaderName = "X-Rems-On-Behalf-Of";

    /// <summary>What a delegate may do in the seat they claimed, or null when they are acting as themselves.</summary>
    public sealed record Seat(Guid PrincipalUserId, bool CanPrepare, bool CanSend);

    /// <summary>
    /// The principal this call is being made for, or null when the caller is acting as themselves — which
    /// covers no header, an unparseable one, the caller's own id, and a header naming someone who has not
    /// delegated to them or whose delegation has lapsed. All four collapse to the same safe answer.
    /// </summary>
    public static async Task<Seat?> ResolveAsync(
        ControllerBase controller,
        IRemsDelegationRepository delegations,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        if (!controller.Request.Headers.TryGetValue(HeaderName, out var raw)
            || !Guid.TryParse(raw.ToString(), out var principalId)
            || principalId == callerId)
        {
            return null;
        }

        var grant = await delegations.GetAsync(principalId, callerId, cancellationToken);
        if (grant is null || !grant.IsActiveOn(DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            return null;
        }

        return new Seat(principalId, grant.CanPrepare, grant.CanSend);
    }
}
