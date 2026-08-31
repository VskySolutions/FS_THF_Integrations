namespace EmsPortal.Application.Common;

/// <summary>
/// The order a list was asked for: the column the reader clicked, and which way.
///
/// <para>
/// One value rather than two loose parameters, because they are never meaningful apart — a direction with
/// no column is nothing, and every list endpoint would otherwise grow the same pair. <see cref="SortBy"/>
/// is the UI's column name, checked against that list's <see cref="SortMap{T}"/>; an unknown one falls
/// back to the list's default order rather than failing the request.
/// </para>
/// </summary>
/// <param name="SortBy">The column name sent by the page, or null to take the list's default.</param>
/// <param name="Descending">True for Z→A / newest-first, which is what a list opens on.</param>
public readonly record struct SortRequest(string? SortBy, bool Descending)
{
    /// <summary>No preference stated — the list orders itself the way it normally does.</summary>
    public static SortRequest Default => new(null, true);
}
